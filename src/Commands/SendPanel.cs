using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;

namespace AGC_Entbannungssystem.Commands;

public sealed class SendPanel : ApplicationCommandsModule
{
    [ApplicationCommandRequirePermissions(Permissions.Administrator)]
    [SlashCommand("sendpanel", "Sendet das Panel für den Entbannungsantrag.")]
    public static async Task SendNotePanel(InteractionContext ctx)
    {
        var embed1 = new DiscordEmbedBuilder();
        // defer
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().WithContent("Panel gesendet!")).AsEphemeral());
        embed1.WithAuthor(ctx.Guild.Name, iconUrl: ctx.Guild.IconUrl);
        embed1.WithDescription(
            "Hey! Willkommen auf dem Entbannungsserver vom ``Anime & Gaming Café``. Hier kannst du einen Entbannungsantrag stellen, wenn du auf dem Hauptserver gebannt wurdest. Drücke dazu einfach auf den Button unten und folge den Anweisungen. \n\n" +
            "📝 Bitte eröffne für alle Anliegen ein Ticket. Bitte sende keine Direktnachrichten oder FAs an Teammitglieder. Dies kann zu einer Ablehnung deines Antrags führen!");
        var button = new DiscordButtonComponent(ButtonStyle.Primary, "open_appealticket", "Ticket erstellen",
            emoji: new DiscordComponentEmoji("📝"));
        var mb = new DiscordMessageBuilder();
        mb.AddComponents(button);
        mb.WithEmbed(embed1);
        await ctx.Channel.SendMessageAsync(mb);
    }

}