using DisCatSharp;
using DisCatSharp.Entities;
using Microsoft.Extensions.Logging;

namespace AGC_Entbannungssystem.Tasks;

public static class CheckTeamRole
{
    public static async Task Run(DiscordClient client)
    {
        try
        {
            DiscordGuild mainGuild = await client.GetGuildAsync(GlobalProperties.MainGuildId);
            DiscordGuild unbanGuild = await client.GetGuildAsync(GlobalProperties.UnbanServerId);
            DiscordRole unbanTeamRole = unbanGuild.GetRole(GlobalProperties.UnbanServerTeamRoleId);

            foreach (var member in mainGuild.Members.Values)
            {
                if (unbanGuild.Members.TryGetValue(member.Id, out var unbanGuildMember))
                {
                    bool hasTeamRoleInMainGuild = member.Roles.Any(x => x.Id == GlobalProperties.MainGuildTeamRoleId);
                    bool hasTeamRoleInUnbanGuild = unbanGuildMember.Roles.Any(x => x.Id == unbanTeamRole.Id);

                    if (hasTeamRoleInMainGuild && !hasTeamRoleInUnbanGuild)
                    {
                        await unbanGuildMember.GrantRoleAsync(unbanTeamRole, "Team role obtained in the main guild.");
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
        }
    }
}