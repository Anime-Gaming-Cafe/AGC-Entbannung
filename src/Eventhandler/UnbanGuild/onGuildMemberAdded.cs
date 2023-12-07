#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using Npgsql;

#endregion

namespace AGC_Entbannungssystem.Eventhandler.UnbanGuild;

[EventHandler]
public class onGuildMemberAdded : ApplicationCommandsModule
{
    [Event]
    public async Task GuildMemberAdded(DiscordClient client, GuildMemberAddEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            var cons = Helperfunctions.DbString();
            await using var con = new NpgsqlConnection(cons);
            await con.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM antragssperre WHERE user_id = @userid", con);
            cmd.Parameters.AddWithValue("userid", (long)e.Member.Id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (reader.HasRows)
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

            await con.CloseAsync();
        });


        await Task.CompletedTask;
    }
}