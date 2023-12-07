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

namespace AGC_Entbannungssystem.Eventhandler;

[EventHandler]
public sealed class onTeamRoleChange : ApplicationCommandsModule
{
    [Event]
    public static async Task GuildMemberUpdated(DiscordClient client, GuildMemberUpdateEventArgs args)
    {
        if (CurrentApplicationData.isReady == false) return;
        if (args.Guild.Id != GlobalProperties.MainGuildId) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var ignoredroles = BotConfigurator.GetConfig("MainConfig", "SyncIgnoredRoleList");

                var split = ignoredroles.Split(", ");
                List<ulong> ignoredroleslist = new();
                foreach (var item in split)
                {
                    ignoredroleslist.Add(ulong.Parse(item));
                }

                bool hasIgnoredRole = false;
                foreach (var role in args.RolesAfter)
                {
                    if (ignoredroleslist.Contains(role.Id))
                    {
                        hasIgnoredRole = true;
                    }
                }

                if (hasIgnoredRole) return;
                if (args.RolesAfter.Any(x => x.Id == GlobalProperties.MainGuildTeamRoleId))
                {
                    DiscordGuild unbanGuild = await client.GetGuildAsync(GlobalProperties.UnbanServerId);
                    DiscordRole unbanTeamRole = unbanGuild.GetRole(GlobalProperties.UnbanServerTeamRoleId);
                    await unbanGuild.Members[args.Member.Id]
                        .GrantRoleAsync(unbanTeamRole, "Teamrolle auf dem Hauptserver erhalten.");
                }
                else if (args.RolesBefore.Any(x => x.Id == GlobalProperties.MainGuildTeamRoleId))
                {
                    DiscordGuild unbanGuild = await client.GetGuildAsync(GlobalProperties.UnbanServerId);
                    DiscordRole unbanTeamRole = unbanGuild.GetRole(GlobalProperties.UnbanServerTeamRoleId);
                    if (!unbanGuild.Members.ContainsKey(args.Member.Id)) return;
                    await unbanGuild.Members[args.Member.Id]
                        .RevokeRoleAsync(unbanTeamRole, "Teamrolle auf dem Hauptserver verloren.");
                }
            }
            catch (Exception err)
            {
                client.Logger.LogError(err, "Error in onTeamRoleChange");
                await ErrorReporting.SendErrorToDev(client, args.Member, err);
            }
        });
    }
}