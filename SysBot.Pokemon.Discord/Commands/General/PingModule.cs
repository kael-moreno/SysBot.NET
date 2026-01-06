using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class PingModule : ModuleBase<SocketCommandContext>
{
    [Command("ping")]
    [Summary("Makes the bot respond, indicating that it is running.")]
    public async Task PingAsync()
    {
        var embed = CreateEmbedBuilder("Pong!", $"{Context.User.Mention}, buhay ako!");

        await ReplyAsync(embed: embed).ConfigureAwait(false);
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
