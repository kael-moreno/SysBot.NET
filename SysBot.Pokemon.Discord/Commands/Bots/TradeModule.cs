using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Queues new Link Code trades")]
public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("tradeList")]
    [Alias("tl")]
    [Summary("Prints the users in the trade queues.")]
    [RequireSudo]
    public async Task GetTradeListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.LinkTrade);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "Pending Trades";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("Eto yung mga nasa queue:", embed: embed.Build()).ConfigureAwait(false);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Makes the bot trade you the provided Pokémon file.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task TradeAsyncAttach([Summary("Trade Code")] int code)
    {
        var sig = Context.User.GetFavor();
        return TradeAsyncAttach(code, sig, Context.User);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Makes the bot trade you a Pokémon converted from the provided Showdown Set.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeAsync([Summary("Trade Code")] int code, [Summary("Showdown Set")][Remainder] string content)
    {
        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var template = AutoLegalityWrapper.GetTemplate(set);
        if (set.InvalidLines.Count != 0 || set.Species is 0)
        {
            var sb = new StringBuilder(128);
            sb.AppendLine("Di ko ma-gets yan. Check mo error sa baba.");
            var invalidlines = set.InvalidLines;
            if (invalidlines.Count != 0)
            {
                var localization = BattleTemplateParseErrorLocalization.Get();
                sb.AppendLine("\n**Invalid lines detected:**");
                foreach (var line in invalidlines)
                {
                    var error = line.Humanize(localization);
                    sb.AppendLine(error);
                }
                sb.AppendLine("\n");
            }
            if (set.Species is 0)
                sb.AppendLine("Parang walang ganyan beh! Check mo spelling!");

            var msg = sb.ToString();
            var embed = CreateEmbedBuilder("Hmm, mali!!!", msg);
            await ReplyAsync(embed: embed).ConfigureAwait(false);
            return;
        }

        try
        {
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            var la = new LegalityAnalysis(pkm);
            var spec = GameInfo.Strings.Species[template.Species];
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
            if (pkm is not T pk || !la.Valid)
            {
                var reason = result switch
                {
                    "Timeout" => $"That {spec} set took too long to generate.",
                    "VersionMismatch" => "Request refused: PKHeX and Auto-Legality Mod version mismatch.",
                    _ => $"I wasn't able to create a {spec} from that set.",
                };
                var imsg = $"Oops! {reason}";
                if (result == "Failed")
                    imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                await ReplyAsync(imsg).ConfigureAwait(false);
                return;
            }
            pk.ResetPartyStats();

            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            var msg = $"Di ko ma-gets yan. Check mo error sa baba. :\n```{string.Join("\n", set.GetSetLines())}```";
            var embed = CreateEmbedBuilder("Hmm, mali!!!", msg);
            await ReplyAsync(embed: embed).ConfigureAwait(false);
        }
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Makes the bot trade you a Pokémon converted from the provided Showdown Set.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task TradeAsync([Summary("Showdown Set")][Remainder] string content)
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsync(code, content);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Makes the bot trade you the attached file.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task TradeAsyncAttach()
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsyncAttach(code);
    }

    [Command("banTrade")]
    [Alias("bt")]
    [RequireSudo]
    public async Task BanTradeAsync([Summary("Online ID")] ulong nnid, string comment)
    {
        SysCordSettings.HubConfig.TradeAbuse.BannedIDs.AddIfNew([GetReference(nnid, comment)]);
        await ReplyAsync("Done.").ConfigureAwait(false);
    }

    private RemoteControlAccess GetReference(ulong id, string comment) => new()
    {
        ID = id,
        Name = id.ToString(),
        Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss} ({comment})",
    };

    [Command("tradeUser")]
    [Alias("tu", "tradeOther")]
    [Summary("Makes the bot trade the mentioned user the attached file.")]
    [RequireSudo]
    public async Task TradeAsyncAttachUser([Summary("Trade Code")] int code, [Remainder] string _)
    {
        if (Context.Message.MentionedUsers.Count > 1)
        {
            await ReplyAsync("Too many mentions. Queue one user at a time.").ConfigureAwait(false);
            return;
        }

        if (Context.Message.MentionedUsers.Count == 0)
        {
            await ReplyAsync("A user must be mentioned in order to do this.").ConfigureAwait(false);
            return;
        }

        var usr = Context.Message.MentionedUsers.ElementAt(0);
        var sig = usr.GetFavor();
        await TradeAsyncAttach(code, sig, usr).ConfigureAwait(false);
    }

    [Command("tradeUser")]
    [Alias("tu", "tradeOther")]
    [Summary("Makes the bot trade the mentioned user the attached file.")]
    [RequireSudo]
    public Task TradeAsyncAttachUser([Remainder] string _)
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsyncAttachUser(code, _);
    }

    private async Task TradeAsyncAttach(int code, RequestSignificance sig, SocketUser usr)
    {
        var attachment = Context.Message.Attachments.FirstOrDefault();
        if (attachment == null)
        {
            var embed = CreateEmbedBuilder("Hmm, mali!!!", "Wala kang attachment na binigay!");
            await ReplyAsync(embed: embed).ConfigureAwait(false);
            return;
        }

        var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
        var pk = GetRequest(att);
        if (pk == null)
        {
            var embed = CreateEmbedBuilder("Hmm, mali!!!", "Di ko ma-gets yan. Siguraduhin mong tama yang file na binigay mo!");
            await ReplyAsync(embed: embed).ConfigureAwait(false);
            return;
        }

        await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr).ConfigureAwait(false);
    }

    private static T? GetRequest(Download<PKM> dl)
    {
        if (!dl.Success)
            return null;
        return dl.Data switch
        {
            null => null,
            T pk => pk,
            _ => EntityConverter.ConvertToType(dl.Data, typeof(T), out _) as T,
        };
    }

    private async Task AddTradeToQueueAsync(int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr)
    {
        var la = new LegalityAnalysis(pk);
        if (!la.Valid)
        {
            // Disallow trading illegal Pokémon.
            var embed = CreateEmbedBuilder("Hmm, mali!!!", $"Hoyyyy!!! Illegal yang {typeof(T).Name} na binigay mo!!!");
            await ReplyAsync(embed: embed).ConfigureAwait(false);
            return;
        }

        var enc = la.EncounterOriginal;
        if (!pk.CanBeTraded(enc))
        {
            // Disallow anything that cannot be traded from the game (e.g. Fusions).
            var embed = CreateEmbedBuilder("Hmm, mali!!!", $"Hindi pwedeng itrade yang binigay mo!!!");
            await ReplyAsync(embed: embed).ConfigureAwait(false);
            return;
        }
        var cfg = Info.Hub.Config.Trade;
        if (cfg.DisallowNonNatives && (enc.Context != pk.Context || pk.GO))
        {
            // Allow the owner to prevent trading entities that require a HOME Tracker even if the file has one already.
            var embed = CreateEmbedBuilder("Hmm, mali!!!", $"Hindi pwedeng i-trade yang {typeof(T).Name} na binigay mo!");
            await ReplyAsync($"{typeof(T).Name} attachment is not native, and cannot be traded!").ConfigureAwait(false);
            return;
        }
        if (cfg.DisallowTracked && pk is IHomeTrack { HasTracker: true })
        {
            // Allow the owner to prevent trading entities that already have a HOME Tracker.
            var embed = CreateEmbedBuilder("Hmm, mali!!!", $"May HOME TRACKER na yang {typeof(T).Name} na binigay mo! Di pwede i-trade.");
            await ReplyAsync($"{typeof(T).Name} attachment is tracked by HOME, and cannot be traded!").ConfigureAwait(false);
            return;
        }

        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr).ConfigureAwait(false);
    }

    private Embed CreateEmbedBuilder(string title, string description)
    {
        var embedBuilder = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription($"{description}")
            .WithThumbnailUrl("https://i.pinimg.com/564x/a2/44/0e/a2440e48f1c34d9f2fd66d4a4342df80.jpg")
            .WithColor(Color.Magenta);
        return embedBuilder.Build();
    }
}
