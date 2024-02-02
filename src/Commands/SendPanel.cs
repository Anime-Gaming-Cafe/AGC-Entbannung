#region

using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;

#endregion

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
            new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().WithContent("Panel gesendet!"))
                .AsEphemeral());
        embed1.WithAuthor(ctx.Guild.Name, iconUrl: ctx.Guild.IconUrl);
        embed1.WithDescription(
            "Hey! Willkommen auf dem Entbannungsserver vom ``Anime & Gaming Café``. Hier kannst du einen Entbannungsantrag stellen, wenn du auf dem Hauptserver gebannt wurdest. Drücke dazu einfach auf den Button unten und folge den Anweisungen. \n\n" +
            "📝 Bitte eröffne für alle Anliegen ein Ticket. Bitte sende keine Direktnachrichten oder FAs an Teammitglieder. Dies kann zu einer Ablehnung deines Antrags führen! \n\n⚠️**Bitte lies unsere Anforderungen für den Antrag genau durch. Ein nichtlesen der Hinweiße kann zur __direkten Ablehnung__ führen.**");
        var button = new DiscordButtonComponent(ButtonStyle.Primary, "open_appealticketinfo",
            "Entbannungsantrag erstellen",
            emoji: new DiscordComponentEmoji("📝"));
        var mb = new DiscordMessageBuilder();
        mb.AddComponents(button);
        mb.WithEmbed(embed1);
        await ctx.Channel.SendMessageAsync(mb);
    }

    [ApplicationCommandRequirePermissions(Permissions.Administrator)]
    [SlashCommand("sendsperrepanel", "Sendet das Sperre Panel für den Entbannungsantrag.")]
    public static async Task SendSperrePanel(InteractionContext ctx)
    {
        var embed1 = new DiscordEmbedBuilder();
        // defer
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().WithContent("Panel gesendet!"))
                .AsEphemeral());
        embed1.Title = "Antragssperre!";
        embed1.WithColor(DiscordColor.Red);
        embed1.WithDescription(
            "Du bist derzeit für Anträge wegen deinem letzten Antrag gesperrt. Du kannst erst wieder einen Antrag stellen, wenn deine Sperre abgelaufen ist. Bitte kontaktiere in der Zwischenzeit kein Teammitglied. \n\n" +
            "📝 Du kannst hier nachschauen, wie lange deine Sperre noch anhält.");
        var button = new DiscordButtonComponent(ButtonStyle.Primary, "open_sperrinfo",
            "Wie lange bin ich gesperrt?",
            emoji: new DiscordComponentEmoji("📝"));
        var mb = new DiscordMessageBuilder();
        mb.AddComponents(button);
        mb.WithEmbed(embed1);
        await ctx.Channel.SendMessageAsync(mb);
    }
}