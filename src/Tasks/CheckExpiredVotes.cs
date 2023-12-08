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
                var dbstring = Helperfunctions.DbString();
                await using var conn = new NpgsqlConnection(dbstring);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand();
                cmd.Connection = conn;
                cmd.CommandText = "SELECT * FROM abstimmungen WHERE expires_at < @endtime";
                cmd.Parameters.AddWithValue("endtime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await using var reader = await cmd.ExecuteReaderAsync();
                // get all expired votes
                while (await reader.ReadAsync())
                {
                    long dbcid = reader.GetInt64(0);
                    var messageid = (ulong)reader.GetInt64(1);
                    var channelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));

                    // get messages from id in vote channel
                    var votechannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));
                    var votechannel = await client.GetChannelAsync(votechannelid);
                    var message = await votechannel.GetMessageAsync(messageid);
                    var emb = new DiscordEmbedBuilder();

                    var reactions = message.Reactions;
                    int positiveVotes = 0;
                    int negativeVotes = 0;
                    foreach (var reaction in reactions)
                    {
                        if (reaction.Emoji.Name == "👍")
                        {
                            positiveVotes = reaction.Count - 1;
                        }
                        else if (reaction.Emoji.Name == "👎")
                        {
                            negativeVotes = reaction.Count - 1;
                        }
                    }

                    // get channel from id
                    var channel = await client.GetChannelAsync(channelid);
                    DiscordChannel? antragc = null;
                    try
                    {
                        antragc = await client.GetChannelAsync((ulong)dbcid);
                    }
                    catch (Exception e)
                    {
                        await ErrorReporting.SendErrorToDev(client, client.CurrentUser, e);
                    }


                    // get message from id
                    var msg = await channel.GetMessageAsync(messageid);
                    // remove all reactions from message
                    await msg.DeleteAllReactionsAsync();


                    var resultString = $"**Ergebnis der Abstimmung für {antragc?.Name} ({antragc?.Mention}):**\n" +
                                       $"**{positiveVotes}** Stimmen für **Ja**\n" +
                                       $"**{negativeVotes}** Stimmen für **Nein**\n" +
                                       $"**{positiveVotes + negativeVotes}** Stimmen insgesamt\n\n";


                    DiscordColor embedColor;

                    string antragStatus;
                    if (positiveVotes > negativeVotes)
                    {
                        antragStatus = "Abstimmung ist positiv";
                        embedColor = DiscordColor.Green;
                    }
                    else if (positiveVotes < negativeVotes)
                    {
                        antragStatus = "Abstimmung ist negativ";
                        embedColor = DiscordColor.Red;
                    }
                    else
                    {
                        antragStatus = "Abstimmung ist unentschieden";
                        embedColor = DiscordColor.Yellow;
                    }

                    emb.WithTitle("Abstimmung beendet");
                    emb.WithDescription(resultString + antragStatus);
                    emb.WithColor(embedColor);
                    emb.WithTimestamp(DateTimeOffset.UtcNow);
                    await msg.RespondAsync($"<@&{BotConfigurator.GetConfig("MainConfig", "UnbanGuildTeamRoleId")}>",emb);
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