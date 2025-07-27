#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using Microsoft.Extensions.Logging;
using Npgsql;

#endregion

namespace AGC_Entbannungssystem.Tasks;

public class CheckExpiredVotes
{
    public static async Task Run(DiscordClient client)
    {
        while (true)
        {
            try
            {
                string dbstring = Helperfunctions.DbString();
                await using var conn = new NpgsqlConnection(dbstring);
                await conn.OpenAsync();
                await using NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.Connection = conn;
                cmd.CommandText = "SELECT * FROM abstimmungen WHERE expires_at < @endtime";
                cmd.Parameters.AddWithValue("endtime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
                // get all expired votes
                while (await reader.ReadAsync())
                {
                    long dbcid = reader.GetInt64(0);
                    DiscordChannel antragschannel = await client.GetChannelAsync((ulong)dbcid);
                    var messageid = (ulong)reader.GetInt64(1);
                    // get messages from id in vote channel
                    ulong votechannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));
                    DiscordChannel votechannel = await client.GetChannelAsync(votechannelid);
                    DiscordMessage message = await votechannel.GetMessageAsync(messageid);
                    var nowtimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var pvotes = reader.GetInt32(4);
                    var nvotes = reader.GetInt32(5);
                    var colorInt = Helperfunctions.getVoteColor(pvotes, nvotes);
                    DiscordEmbed emb = MessageGenerator.getVoteEmbedFinished(antragschannel, nowtimestamp,
                        nvotes, pvotes, colorInt);
                    try
                    {
                        // delete message
                        await message.DeleteAsync("Abstimmung abgelaufen.");
                    }
                    catch (Exception e)
                    {
                        client.Logger.LogError(e, "Error deleting expired vote message");
                    }

                    DiscordMessageBuilder builder = new DiscordMessageBuilder()
                        .WithContent(Helperfunctions.getTeamPing())
                        .AddEmbed(emb);
                    await votechannel.SendMessageAsync(builder);
                }

                await reader.CloseAsync();
                await conn.CloseAsync();
                // delete all expired votes from db
                await using var conn2 = new NpgsqlConnection(dbstring);
                await conn2.OpenAsync();
                await using var cmd2 = new NpgsqlCommand();
                cmd2.Connection = conn2;
                cmd2.CommandText = "DELETE FROM abstimmungen WHERE expires_at < @endtime";
                cmd2.Parameters.AddWithValue("endtime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await cmd2.ExecuteNonQueryAsync();
                // abstimmungen_teamler delete voteid
                await using var cmd3 = new NpgsqlCommand();
                cmd3.Connection = conn2;
                // voteid is channelid+messageid
                cmd3.CommandText = "DELETE FROM abstimmungen_teamler WHERE vote_id IN " +
                                   "(SELECT channel_id || '_' || message_id FROM abstimmungen WHERE expires_at < @endtime)";
                cmd3.Parameters.AddWithValue("endtime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await cmd3.ExecuteNonQueryAsync();
                await conn2.CloseAsync();
            }
            catch (Exception err)
            {
                client.Logger.LogError(err, "Error in checkExpiredVotes");
                await ErrorReporting.SendErrorToDev(client, client.CurrentUser, err);
            }

            await Task.Delay(TimeSpan.FromMinutes(2));
        }
    }
}