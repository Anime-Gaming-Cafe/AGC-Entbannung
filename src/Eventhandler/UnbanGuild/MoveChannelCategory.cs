#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using Microsoft.Extensions.Logging;

#endregion

namespace AGC_Entbannungssystem.Eventhandler.UnbanGuild;

[EventHandler]
public class MoveChannelCategory : ApplicationCommandsModule
{
    [Event]
    public async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                List<string> moveKeywords = new()
                {
                    "accept",
                    "acceptsafety",
                    "deny",
                    "deny2",
                    "deny13",
                    "deny24",
                    "bannsystem",
                    "noai2"
                };

                if (e.Guild?.Id != GlobalProperties.UnbanServerId) return;
                if (!e.Channel.Name.StartsWith("antrag-") || e.Channel.Name.EndsWith("-geschlossen")) return;
                if (e.Message.Author.IsBot) return;

                ulong moderatorRoleId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanGuildTeamRoleId"));
                DiscordRole moderatorRole = e.Guild.GetRole(moderatorRoleId);
                var member = await e.Guild.GetMemberAsync(e.Message.Author.Id);
                if (!member.Roles.Contains(moderatorRole)) return;

                var content = e.Message.Content?.ToLower() ?? string.Empty;

                if (content == "accept" || content == "acceptsafety")
                {
                    try
                    {
                        var antragsnummer = e.Channel.Name.Split('-').LastOrDefault();
                        if (!string.IsNullOrEmpty(antragsnummer) && antragsnummer.All(char.IsDigit))
                        {
                            var userId = await Helperfunctions.GetTicketUserIdAsync(e.Channel);
                            if (userId != null)
                            {
                                var targetUser = await client.GetUserAsync(userId.Value);
                                var reason = content == "acceptsafety"
                                    ? "Account gehackt - entbannt nach Antrag"
                                    : "Antrag positiv abgestimmt";
                                await Helperfunctions.TryAddAntragsverlaufAsync(
                                    client,
                                    entbannt: true,
                                    modUser: e.Message.Author,
                                    antragsnummer: antragsnummer,
                                    targetUser: targetUser,
                                    grund: reason);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        client.Logger.LogError(ex, "Auto-Antragshistorie nach accept/acceptsafety fehlgeschlagen");
                    }
                }

                var ucat = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "BearbeitetCategoryId"));
                if (e.Channel.ParentId == ucat) return;

                if (moveKeywords.Contains(content))
                {
                    DiscordChannel newcat = e.Guild.GetChannel(ucat);
                    await e.Channel.ModifyAsync(x => x.Parent = newcat);
                }
            }
            catch (Exception ex)
            {
                client.Logger.LogError(ex, "Error in MoveChannelCategory handler");
            }
        });

        await Task.CompletedTask;
    }
}
