#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.Entities;
using DisCatSharp.EventArgs;
using Microsoft.EntityFrameworkCore;

#endregion

namespace AGC_Entbannungssystem.Eventhandler.UnbanGuild;

public class onGuildMemberAdded : ApplicationCommandsModule
{
    public async Task GuildMemberAdded(DiscordClient client, GuildMemberAddEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await using var context = AgcDbContextFactory.CreateDbContext();

            var sperre = await context.Antragssperren
                .FirstOrDefaultAsync(s => s.UserId == (long)e.Member.Id);

            if (sperre != null)
            {
                try
                {
                    ulong roleid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreRoleId"));
                    DiscordRole role = e.Guild.GetRole(roleid);
                    await e.Member.GrantRoleAsync(role, "Nutzer ist gesperrt.");
                }
                catch (Exception exception)
                {
                    await ErrorReporting.SendErrorToDev(client, e.Member, exception);
                }
            }
        });

        await Task.CompletedTask;
    }
}