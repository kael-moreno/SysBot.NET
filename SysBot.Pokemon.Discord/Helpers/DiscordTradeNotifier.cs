using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Linq;

namespace SysBot.Pokemon.Discord;

public class DiscordTradeNotifier<T>(T Data, PokeTradeTrainerInfo Info, int Code, SocketUser Trader)
    : IPokeTradeNotifier<T>
    where T : PKM, new()
{
    private T Data { get; } = Data;
    private PokeTradeTrainerInfo Info { get; } = Info;
    private int Code { get; } = Code;
    private SocketUser Trader { get; } = Trader;
    public Action<PokeRoutineExecutor<T>>? OnFinish { private get; set; }
    public readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;

    public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var receive = Data.Species == 0 ? "Game na!" : $"Ready na ({Data.Nickname}) mo!";
        var builder = CreateEmbedBuilder(title: $"{receive}",
            description: $"Eto na Trade Code mo beh: **{Code:0000 0000}**.",
            color: Color.Gold);
        Trader.SendMessageAsync(embed: builder.Build()).ConfigureAwait(false);
    }

    public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var name = Info.TrainerName;
        var trainer = string.IsNullOrEmpty(name) ? string.Empty : $", {name}";
        var builder = CreateEmbedBuilder(title: $"Trade Code: {Code:0000 0000}",
            description: $"Searching na ko{trainer}! Ayan na trade code natin.",
            color: Color.Gold)
            .AddField("IGN", routine.InGameName, true);
        Trader.SendMessageAsync(embed: builder.Build()).ConfigureAwait(false);
    }

    public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
    {
        OnFinish?.Invoke(routine);
        Trader.SendMessageAsync($"Na-cancel trade natin hays: {msg}").ConfigureAwait(false);
    }

    public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
    {
        OnFinish?.Invoke(routine);
        var tradedToUser = Data.Species;

        var message = tradedToUser != 0 ? $"Tapos na! Nabigay ko na {(Species)tradedToUser} mo! Alis na ko, bye!" : "Yey! Alis na ko, bye!";

        var builder = CreateEmbedBuilder(title: $"Yey! Trade finished!",
            description: $"{message}",
            color: Color.Green);

        Trader.SendMessageAsync(embed: builder.Build()).ConfigureAwait(false);
        if (result.Species != 0 && Hub.Config.Discord.ReturnPKMs)
            Trader.SendPKMAsync(result, "Eto binigay mo, just in case need mo hehehe...").ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
    {
        Trader.SendMessageAsync(message).ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
    {
        if (message.ExtraInfo is SeedSearchResult r)
        {
            SendNotificationZ3(r);
            return;
        }

        var msg = message.Summary;
        if (message.Details.Count > 0)
            msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
        Trader.SendMessageAsync(msg).ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
    {
        if (result.Species != 0 && (Hub.Config.Discord.ReturnPKMs || info.Type == PokeTradeType.Dump))
            Trader.SendPKMAsync(result, message).ConfigureAwait(false);
    }

    private void SendNotificationZ3(SeedSearchResult r)
    {
        var lines = r.ToString();

        var embed = new EmbedBuilder { Color = Color.LighterGrey };
        embed.AddField(x =>
        {
            x.Name = $"Seed: {r.Seed:X16}";
            x.Value = lines;
            x.IsInline = false;
        });
        var msg = $"Here are the details for `{r.Seed:X16}`:";
        Trader.SendMessageAsync(msg, embed: embed.Build()).ConfigureAwait(false);
    }

    private EmbedBuilder CreateEmbedBuilder(string title, string description, Color color)
    {
        return new EmbedBuilder()
            .WithTitle($"{title}")
            .WithDescription($"{description}")
            .WithColor(color);
    }
}
