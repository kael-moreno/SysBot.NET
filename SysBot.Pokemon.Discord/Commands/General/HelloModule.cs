using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class HelloModule : ModuleBase<SocketCommandContext>
{
    [Command("hello")]
    [Alias("hi")]
    [Summary("Say hello to the bot and get a response.")]
    public async Task PingAsync()
    {
        var str = SysCordSettings.Settings.HelloResponse;
        var msg = string.Format(str, Context.User.Mention);

        var embed = CreateEmbedBuilder("Yow!", $"Miss me, {Context.User.Mention}?");
        await ReplyAsync(embed: embed).ConfigureAwait(false);
    }

    [Command("unli")]
    [Summary("Unli trade repeated")]
    public async Task RestartBotAsync(string tids)
    {
        PokeTradeBotLZA.UnliTID = tids;
        await ReplyAsync($"```Unli TID set to {tids}.```").ConfigureAwait(false);
    }

    [Command("randomcode")]
    [Alias("code")]
    [Summary("Set Random Trade Code")]
    public async Task SetRandomTradeCode(int code)
    {
        PokeTradeBotLZA.RandomTradeCode = code;
        await ReplyAsync($"```Random Trade Code set to {code}.```").ConfigureAwait(false);
    }

    private Embed CreateEmbedBuilder(string title, string description)
    {
        var embedBuilder = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription($"{description}")
            .WithImageUrl("https://static0.srcdn.com/wordpress/wp-content/uploads/2022/11/pokemon-sword-shield-raihan-lose.jpg")
            .WithColor(Color.Magenta);
        return embedBuilder.Build();
    }
}
