using AGC_Entbannungssystem.Helpers;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using DisCatSharp.Exceptions;

namespace AGC_Entbannungssystem.Eventhandler.UnbanGuild;

[EventHandler]
public class onComponentInteraction : ApplicationCommandsModule
{
    [Event]
    public async Task ComponentInteractionCreated(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            Console.WriteLine("Component Interaction created!");
            string cid = e.Interaction.Data.CustomId;
            if (cid == "open_appealticketinfo")
            {
                // check if user isbanned
                DiscordGuild mainGuild = await client.GetGuildAsync(GlobalProperties.MainGuildId);
                bool isBanned = false;
                try
                {
                    Console.WriteLine("Checking if user is banned...");
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
                    embed.WithDescription("Es ist ein Fehler aufgetreten. Bitte versuche es später erneut. Der Fehler wurde automatisch an den Entwickler weitergeleitet.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral());
                    await ErrorReporting.SendErrorToDev(client, e.User, exception);
                }
                
                if (!isBanned)
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Nicht gebannt!");
                    embed.WithDescription("Wie es scheint, bist du nicht auf AGC gebannt. Diese Überprüfung ist automatisiert. Du kannst also keinen Entbannungsantrag stellen.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral());
                }
            }
            return Task.CompletedTask;
        });
    }


}