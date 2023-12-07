#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Exceptions;
using Npgsql;

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
                var dbstring = Helperfunctions.DbString();
                await using var conn = new NpgsqlConnection(dbstring);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand();
                cmd.Connection = conn;
                cmd.CommandText = "SELECT * FROM antragssperre WHERE expires_at < @endtime";
                cmd.Parameters.AddWithValue("endtime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var userid = (ulong)reader.GetInt64(0);

                    var guild = await client.GetGuildAsync(
                        ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanServerId")));

                    var roleid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "BlockRoleId"));
                    try
                    {
                        var member = await guild.GetMemberAsync(userid);
                        var role = guild.GetRole(roleid);
                        await member.RevokeRoleAsync(role, "Sperre abgelaufen.");
                    }
                    catch (NotFoundException e)
                    {
                        // ignored
                    }

                    await using var conn2 = new NpgsqlConnection(dbstring);
                    await conn2.OpenAsync();
                    await using var cmd2 = new NpgsqlCommand();
                    cmd2.Connection = conn2;
                    cmd2.CommandText = "DELETE FROM antragssperre WHERE user_id = @userid";
                    cmd2.Parameters.AddWithValue("userid", (long)userid);
                    await cmd2.ExecuteNonQueryAsync();
                    await conn2.CloseAsync();
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                await conn.CloseAsync();
            }
            catch (Exception e)
            {
                await ErrorReporting.SendErrorToDev(client, CurrentApplicationData.BotApplication, e);
            }

            await Task.Delay(TimeSpan.FromMinutes(2));
        }
    }
}