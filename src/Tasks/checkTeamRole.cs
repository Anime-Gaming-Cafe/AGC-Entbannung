#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using Microsoft.Extensions.Logging;

#endregion

namespace AGC_Entbannungssystem.Tasks;

public static class CheckTeamRole
{
    public static async Task Run(DiscordClient client)
    {
        while (true)
        {
            try
            {
                DiscordGuild mainGuild = await client.GetGuildAsync(GlobalProperties.MainGuildId);
                DiscordGuild unbanGuild = await client.GetGuildAsync(GlobalProperties.UnbanServerId);
                DiscordRole unbanTeamRole = unbanGuild.GetRole(GlobalProperties.UnbanServerTeamRoleId);
                var ignoredroles = BotConfigurator.GetConfig("MainConfig", "SyncIgnoredRoleList");

                var split = ignoredroles.Split(", ");
                List<ulong> ignoredroleslist = new();
                foreach (var item in split)
                {
                    ignoredroleslist.Add(ulong.Parse(item));
                }
                
                foreach (var member in mainGuild.Members.Values)
                {
                    bool hasIgnoredRole = false;
                    foreach (var role in member.Roles)
                    {
                        if (ignoredroleslist.Contains(role.Id))
                        {
                            hasIgnoredRole = true;
                        }
                    }
                    if (hasIgnoredRole) continue;
                    
                    if (unbanGuild.Members.TryGetValue(member.Id, out var unbanGuildMember))
                    {
                        bool hasTeamRoleInMainGuild =
                            member.Roles.Any(x => x.Id == GlobalProperties.MainGuildTeamRoleId);
                        bool hasTeamRoleInUnbanGuild = unbanGuildMember.Roles.Any(x => x.Id == unbanTeamRole.Id);

                        if (hasTeamRoleInMainGuild && !hasTeamRoleInUnbanGuild)
                        {
                            await unbanGuildMember.GrantRoleAsync(unbanTeamRole,
                                "Team role obtained in the main guild.");
                        }
                        else if (!hasTeamRoleInMainGuild && hasTeamRoleInUnbanGuild)
                        {
                            await unbanGuildMember.RevokeRoleAsync(unbanTeamRole, "Team role lost in the main guild.");
                        }
                    }
                }
            }
            catch (Exception err)
            {
                client.Logger.LogError(err, "Error in CheckTeamRole");
                await ErrorReporting.SendErrorToDev(client, client.CurrentUser, err);
            }

            await Task.Delay(TimeSpan.FromMinutes(2));
        }
    }
}