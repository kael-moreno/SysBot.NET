using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static PKHeX.Core.IAesCryptographyProvider;

namespace SysBot.Pokemon.Discord;

// ReSharper disable once UnusedType.Global
public class BotModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("botStatus")]
    [Summary("Gets the status of the bots.")]
    [RequireSudo]
    public async Task GetStatusAsync()
    {
        var me = SysCord<T>.Runner;
        var sb = new StringBuilder();
        foreach (var bot in me.Bots)
        {
            if (bot.Bot is not PokeRoutineExecutorBase b)
                continue;
            sb.AppendLine(GetDetailedSummary(b));
        }
        if (sb.Length == 0)
        {
            await ReplyAsync("No bots configured.").ConfigureAwait(false);
            return;
        }
        await ReplyAsync(Format.Code(sb.ToString())).ConfigureAwait(false);
    }

    private static string GetDetailedSummary<TBot>(TBot z) where TBot: PokeRoutineExecutorBase
    {
        return $"- {z.Connection.Name} | {z.Connection.Label} - {z.Config.CurrentRoutineType} ~ {z.LastTime:hh:mm:ss} | {z.LastLogged}";
    }

    [Command("botStart")]
    [Summary("Starts a bot by IP address/port.")]
    [RequireSudo]
    public async Task StartBotAsync(string ip)
    {
        var bot = SysCord<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyAsync($"No bot has that IP address ({ip}).").ConfigureAwait(false);
            return;
        }

        bot.Start();
        await Context.Channel.EchoAndReply($"The bot at {ip} ({bot.Bot.Connection.Label}) has been commanded to Start.").ConfigureAwait(false);
    }

    [Command("botStop")]
    [Summary("Stops a bot by IP address/port.")]
    [RequireSudo]
    public async Task StopBotAsync(string ip)
    {
        var bot = SysCord<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyAsync($"No bot has that IP address ({ip}).").ConfigureAwait(false);
            return;
        }

        bot.Stop();
        await Context.Channel.EchoAndReply($"The bot at {ip} ({bot.Bot.Connection.Label}) has been commanded to Stop.").ConfigureAwait(false);
    }

    [Command("botIdle")]
    [Alias("botPause")]
    [Summary("Commands a bot to Idle by IP address/port.")]
    [RequireSudo]
    public async Task IdleBotAsync(string ip)
    {
        var bot = SysCord<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyAsync($"No bot has that IP address ({ip}).").ConfigureAwait(false);
            return;
        }

        bot.Pause();
        await Context.Channel.EchoAndReply($"The bot at {ip} ({bot.Bot.Connection.Label}) has been commanded to Idle.").ConfigureAwait(false);
    }

    [Command("botChange")]
    [Summary("Changes the routine of a bot (trades).")]
    [RequireSudo]
    public async Task ChangeTaskAsync(string ip, [Summary("Routine enum name")] PokeRoutineType task)
    {
        var bot = SysCord<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyAsync($"No bot has that IP address ({ip}).").ConfigureAwait(false);
            return;
        }

        bot.Bot.Config.Initialize(task);
        await Context.Channel.EchoAndReply($"The bot at {ip} ({bot.Bot.Connection.Label}) has been commanded to do {task} as its next task.").ConfigureAwait(false);
    }

    [Command("botRestart")]
    [Summary("Restarts the bot(s) by IP address(es), separated by commas.")]
    [RequireSudo]
    public async Task RestartBotAsync(string ipAddressesCommaSeparated)
    {
        var ips = ipAddressesCommaSeparated.Split(',');
        foreach (var ip in ips)
        {
            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"No bot has that IP address ({ip}).").ConfigureAwait(false);
                return;
            }

            var c = bot.Bot.Connection;
            c.Reset();
            bot.Start();
            await Context.Channel.EchoAndReply($"The bot at {ip} ({c.Label}) has been commanded to Restart.").ConfigureAwait(false);
        }
    }

    [Command("rebootpi")]
    [Alias("rebootrpi")]
    [Summary("RebootRPI")]
    [RequireSudo]
    public async Task RebootRPI()
    {
        var me = SysCord<T>.Runner;
        foreach (var bot in me.Bots)
        {
            if (bot.Bot is not PokeRoutineExecutorBase b)
                continue;

            bot.Stop();
            await Context.Channel.EchoAndReply($"The bot at ({bot.Bot.Connection.Label}) has been commanded to Stop.").ConfigureAwait(false);
        }
        await ReplyAsync($"Rebooting...").ConfigureAwait(false);
        System.Diagnostics.Process.Start(new ProcessStartInfo() { FileName = "sudo", Arguments = "reboot" });

    }

    [Command("unli")]
    [Summary("Unli trade repeated")]
    [RequireSudo]
    public async Task UnliTradeAsync(string tids)
    {
        PokeTradeBotLZA.UnliTID = tids;
        await ReplyAsync($"```Unli TID set to {tids}.```").ConfigureAwait(false);
    }

    private static string WorkingDirectory = Environment.CurrentDirectory = Path.GetDirectoryName(Environment.ProcessPath)!;
    private static string ConfigPath = Path.Combine(WorkingDirectory, "config.json");

    [Command("randomcode")]
    [Alias("code")]
    [Summary("Set Random Trade Code")]
    [RequireSudo]
    public async Task SetRandomTradeCode(int code)
    {
        SysCord<T>.Runner.Config.Distribution.TradeCode = code;

        var lines = File.ReadAllText(ConfigPath);
        var cfg = JsonSerializer.Deserialize(lines, ProgramConfigContext.Default.ProgramConfig) ?? new ProgramConfig();
        cfg.Hub.Distribution.TradeCode = code;

        var newConfigLines = JsonSerializer.Serialize(cfg, ProgramConfigContext.Default.ProgramConfig);
        File.WriteAllText(ConfigPath, newConfigLines);

        await ReplyAsync($"```Random Trade Code set to {code}.```").ConfigureAwait(false);
    }

    [Command("distributionoff")]
    [Alias("distrioff")]
    [Summary("Turn off distribution")]
    [RequireSudo]
    public async Task RandomDistributionOff()
    {
        SysCord<T>.Runner.Config.Distribution.DistributeWhileIdle = false;

        var lines = File.ReadAllText(ConfigPath);
        var cfg = JsonSerializer.Deserialize(lines, ProgramConfigContext.Default.ProgramConfig) ?? new ProgramConfig();
        cfg.Hub.Distribution.DistributeWhileIdle = false;

        var newConfigLines = JsonSerializer.Serialize(cfg, ProgramConfigContext.Default.ProgramConfig);
        File.WriteAllText(ConfigPath, newConfigLines);

        await ReplyAsync($"```Turned off Random Distribution.```").ConfigureAwait(false);
    }
}
