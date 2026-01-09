using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader)
    {
        if ((uint)code > MaxTradeCode)
        {
            await context.Channel.SendMessageAsync("Trade code should be 00000000-99999999!").ConfigureAwait(false);
            return;
        }

        try
        {
            // const string helper = "I've added you to the queue! I'll message you here when your trade is starting.";
            // IUserMessage test = await trader.SendMessageAsync(helper).ConfigureAwait(false);

            // Try adding
            var result = AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, out var msg);
            var embed = CreateEmbedMessage(result, trade, routine, type, trader);

            // Notify in channel
            // await context.Channel.SendMessageAsync(msg).ConfigureAwait(false);
            await context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            // Notify in PM to mirror what was said in the channel.
            // Only tell them a trade code if it was successful.
            if (result)
                msg += $"\nSend ko **Trade Code**, wait lang.";
            // await trader.SendMessageAsync($"{msg}").ConfigureAwait(false);

            // Clean Up
            if (result)
            {
                // Delete the user's join message for privacy
                if (!context.IsPrivate)
                    await context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
            }
            else
            {
                // Delete our "I'm adding you!", and send the same message that we sent to the general channel.
                // await test.DeleteAsync().ConfigureAwait(false);
            }
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User);
    }
    
    private static bool AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, out string msg)
    {
        var user = trader;
        var userID = user.Id;
        var name = user.Username;

        var trainer = new PokeTradeTrainerInfo(trainerName, userID);
        var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, user);
        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored);
        var trade = new TradeEntry<T>(detail, userID, type, name);

        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var added = Info.AddToTradeQueue(trade, userID, sig == RequestSignificance.Owner);
        
        

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            msg = "Nasa queue ka na beh! Maya na ulit!";
            return false;
        }

        var position = Info.CheckPosition(userID, type);

        var ticketID = "";
        if (TradeStartModule<T>.IsStartChannel(context.Channel.Id))
            ticketID = $", unique ID: {detail.ID}";

        var pokeName = "";
        if (t == PokeTradeType.Specific && pk.Species != 0)
            pokeName = $" Receiving: **{GameInfo.GetStrings("en").Species[pk.Species]}**.";
        msg = $"{user.Mention} - Na-add na kita sa {type} queue{ticketID}. Current Position: {position.Position}.{pokeName}";

        var botct = Info.Hub.Bots.Count;
        if (position.Position > botct)
        {
            var eta = Info.Hub.Config.Queues.EstimateDelay(position.Position, botct);
            msg += $" Estimated: {eta:F1} minutes.";
        }
        return true;
    }

    private static async Task HandleDiscordExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.DiscordCode)
        {
            case DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions:
            {
                // Check if the exception was raised due to missing "Send Messages" or "Manage Messages" permissions. Nag the bot owner if so.
                var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                if (!permissions.SendMessages)
                {
                    // Nag the owner in logs.
                    message = "You must grant me \"Send Messages\" permissions!";
                    Base.LogUtil.LogError(message, "QueueHelper");
                    return;
                }
                if (!permissions.ManageMessages)
                {
                    var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                    var owner = app.Owner.Id;
                    message = $"<@{owner}> You must grant me \"Manage Messages\" permissions!";
                }
            }
                break;
            case DiscordErrorCode.CannotSendMessageToUser:
            {
                // The user either has DMs turned off, or Discord thinks they do.
                message = context.User == trader ? "You must enable private messages in order to be queued!" : "The mentioned user must enable private messages in order for them to be queued!";
            }
                break;
            default:
            {
                // Send a generic error message.
                message = ex.DiscordCode != null ? $"Discord error {(int)ex.DiscordCode}: {ex.Reason}" : $"Http error {(int)ex.HttpCode}: {ex.Message}";
            }
                break;
        }
        await context.Channel.SendMessageAsync(message).ConfigureAwait(false);
    }

    private static Embed CreateEmbedMessage(bool result, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader)
    {
        string spriteUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/refs/heads/master/sprites/pokemon/other/home/";
        string raihanUrl = "https://i.pinimg.com/564x/a2/44/0e/a2440e48f1c34d9f2fd66d4a4342df80.jpg";
        var thumbnailUrl = raihanUrl;

        var userID = trader.Id;
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;

        var position = Info.CheckPosition(userID, routine);
        var otName = $" ({trade.OriginalTrainerName})";

        var embed = new EmbedBuilder()
            .WithTitle(result ? $"{trader.Username} - Nasa {routine} queue ka na!" : "Urgh, di ka ma-add sa queue hays.")
            .WithDescription(result ? "Wait mo lang turn mo beh! DM kita pag ikaw na." : "Di ka ma-add. Try ulit!")
            .WithColor(result ? Color.Green : Color.Red);
        // .WithFooter(footer => footer.Text = "LordGrim x Raihan Bot")

        if (type == PokeTradeType.Specific)
        {
            // Try to get PA9-specific 'IsAlpha' property via reflection in case T is PA9
            bool isAlpha = false;
            var tradeType = trade.GetType();
            if (tradeType != null)
            {
                var prop = tradeType.GetProperty("IsAlpha", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null && prop.PropertyType == typeof(bool))
                {
                    isAlpha = (bool)(prop.GetValue(trade) ?? false);
                }
            }

            var isShiny = trade.IsShiny;
            var ball = GameInfo.GetStrings("en").balllist[trade.Ball];
            var ivs = $"{trade.IV_ATK} Atk / {trade.IV_DEF} Def / " +
                $"{trade.IV_HP} Hp / {trade.IV_SPA} SpA / " +
                $"{trade.IV_SPD} SpD / {trade.IV_SPE} Spe";
            
            var requestContent = $"**Ball:** {ball}\n" +
                $"**IV Stats:** {ivs}";

            if (trade.EVTotal > 0)
            {
                var evs = $"{trade.EV_ATK} Atk / {trade.EV_DEF} Def / " +
                $"{trade.EV_HP} Hp / {trade.EV_SPA} SpA / " +
                $"{trade.EV_SPD} SpD / {trade.EV_SPE} Spe";
                requestContent += $"\n**EV Stats:** {evs}";
            }
       

            if (trade.HeldItem != 0)
                requestContent += $"\n**Held Item:** {GameInfo.GetStrings("en").Item[trade.HeldItem]}";

            if (trade.Moves.Length != 0)
            {
                List<string> movesList = new List<string>();
                foreach (var move in trade.Moves)
                {
                    movesList.Add(GameInfo.GetStrings("en").Move[move]);
                }
                var moves = string.Join(" / ", movesList);
                requestContent += $"\n**Moves:**\n{moves}";
            }

            var shinyPath = trade.IsShiny ? "shiny/" : "";
            var spritePath = $"{trade.Species}.png";

            var prefixReceiving = "";
            if (isShiny && isAlpha)
                prefixReceiving = "SHALPHA ";
            else if (isAlpha)
                prefixReceiving = "ALPHA ";
            else if (isShiny)
                prefixReceiving = "SHINY ";

            thumbnailUrl = $"{spriteUrl}{shinyPath}{spritePath}";
            embed
                .AddField("Trainer", $"{trader.Mention}{otName}", true)
                .AddField("Current Position", position.Position.ToString(), true)
                .AddField($"Requesting: {prefixReceiving}{GameInfo.GetStrings("en").Species[trade.Species]}", requestContent);

        }
        else
        {
            embed
                .AddField("Trainer", $"{trader.Mention}", true)
                .AddField("Current Position", position.Position.ToString(), true);
        }

        embed
            .WithThumbnailUrl($"{thumbnailUrl}");

        return embed.Build();
    }
}
