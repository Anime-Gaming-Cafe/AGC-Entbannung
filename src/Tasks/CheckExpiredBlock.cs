#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Exceptions;
using Microsoft.EntityFrameworkCore;

#endregion

namespace AGC_Entbannungssystem.Tasks;

public class CheckExpiredBlock
{
    public static async Task Run(DiscordClient client)
    {
        while (true)
        {
            try
            {
                using var context = AgcDbContextFactory.CreateDbContext();
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                var expiredBlocks = await context.Antragssperren
                    .Where(a => a.ExpiresAt < currentTime)
                    .ToListAsync();

                var guild = await client.GetGuildAsync(
                    ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanServerId")));
                var roleid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreRoleId"));
                var role = guild.GetRole(roleid);

                foreach (var block in expiredBlocks)
                {
                    try
                    {
                        var member = await guild.GetMemberAsync((ulong)block.UserId);
                        await member.RevokeRoleAsync(role, "Sperre abgelaufen.");
                    }
                    catch (NotFoundException e)
                    {
                        // ignored - user not in guild
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                // Remove expired blocks from database
                if (expiredBlocks.Any())
                {
                    context.Antragssperren.RemoveRange(expiredBlocks);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception e)
            {
                await ErrorReporting.SendErrorToDev(client, CurrentApplicationData.BotApplication, e);
            }

            await Task.Delay(TimeSpan.FromMinutes(2));
        }
    }
}