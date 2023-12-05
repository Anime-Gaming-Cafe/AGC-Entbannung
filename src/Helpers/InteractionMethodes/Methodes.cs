using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using DisCatSharp.Exceptions;

namespace AGC_Entbannungssystem.Helpers;

public static class Methodes
{
    public static async Task PanelCheckBan(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        // check if user isbanned
        DiscordGuild mainGuild = await client.GetGuildAsync(GlobalProperties.MainGuildId);
        bool isBanned = false;
        try
        {
            await mainGuild.GetBanAsync(e.User.Id);
            isBanned = true;
        }
        catch (NotFoundException)
        {
            // ignored
            isBanned = false;
        }
        catch (Exception exception)
        {
            var embed = new DiscordEmbedBuilder();
            embed.WithTitle("Fehler!");
            embed.WithDescription(
                "Es ist ein Fehler aufgetreten. Bitte versuche es später erneut. Der Fehler wurde automatisch an den Entwickler weitergeleitet.");
            embed.WithColor(DiscordColor.Red);
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral());
            await ErrorReporting.SendErrorToDev(client, e.User, exception);
        }

        if (!isBanned)
        {
            var embed = new DiscordEmbedBuilder();
            embed.WithTitle("Nicht gebannt!");
            embed.WithDescription(
                "Wie es scheint, bist du nicht auf AGC gebannt. Diese Überprüfung ist automatisiert. Du kannst also keinen Entbannungsantrag stellen.");
            embed.WithColor(DiscordColor.Red);
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral());
        }
    }
}