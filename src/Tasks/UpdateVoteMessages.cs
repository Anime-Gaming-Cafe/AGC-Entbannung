using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Npgsql;

namespace AGC_Entbannungssystem.Tasks;

public class UpdateVoteMessages
{
    public static async Task Run(DiscordClient client)
    {
        // structure of abstimmungen table: channel_id, message_id, expires_at, created_by, pvotes, nvotes
        while (true)
        {
            try
            {
                string dbstring = Helperfunctions.DbString();
                await using var conn = new NpgsqlConnection(dbstring);
                await conn.OpenAsync();
                await using NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.Connection = conn;
                cmd.CommandText = "SELECT * FROM abstimmungen WHERE expires_at > @currenttime";
                cmd.Parameters.AddWithValue("currenttime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    long antragchannelid = reader.GetInt64(0);
                    var votemessageid = (ulong)reader.GetInt64(1);

                    // get messages from id in vote channel
                    ulong votechannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));
                    DiscordChannel votechannel = await client.GetChannelAsync(votechannelid);
                    DiscordMessage message = await votechannel.GetMessageAsync(votemessageid);

                    long expiresAt = reader.GetInt64(2);

                    var pvotes = reader.GetInt32(4);
                    var nvotes = reader.GetInt32(5);
                    int color = getVoteColor(pvotes, nvotes);

                    DiscordEmbed vembed = MessageGenerator.getVoteEmbedInRunning(votechannel, expiresAt, nvotes, pvotes, color);

                    var votebuttons = new List<DiscordButtonComponent>()
                    {
                        new(ButtonStyle.Secondary, "vote_yes_" + antragchannelid, "👍 Ja"),
                        new(ButtonStyle.Secondary, "vote_no_" + antragchannelid, "👎 Nein")
                    };
                    DiscordMessageBuilder builder = new DiscordMessageBuilder()
                        .WithContent(Helperfunctions.getTeamPing())
                        .AddComponents(votebuttons)
                        .AddEmbed(vembed);

                    await message.ModifyAsync(builder);
                    await conn.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                //
            }
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }

    private static int getVoteColor(int pvotes, int nvotes)
    {
        if (pvotes == 0 && nvotes == 0)
            return -1; // No votes → gray (default)
        if (pvotes == nvotes)
            return 2; // Tie → yellow
        if (pvotes > nvotes)
            return 1; // More positive votes → green

        return 0; // More negative votes → red
    }
    
}