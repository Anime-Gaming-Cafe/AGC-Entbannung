using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Npgsql;

namespace AGC_Entbannungssystem.Tasks;

public static class UpdateVoteMessages
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

                await using var cmd =
                    new NpgsqlCommand("SELECT * FROM abstimmungen WHERE expires_at > @currenttime", conn);
                cmd.Parameters.AddWithValue("currenttime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    long antragchannelid = reader.GetInt64(0);
                    ulong messageId = (ulong)reader.GetInt64(1);
                    long expiresAt = reader.GetInt64(2);
                    int pvotes = reader.GetInt32(4);
                    int nvotes = reader.GetInt32(5);

                    await UpdateSingleVoteMessage(client, antragchannelid, messageId, expiresAt, pvotes, nvotes);
                }
            }
            catch (Exception ex)
            {
                //
            }

            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }

    public static async Task UpdateSingleVoteMessage(DiscordClient client, long antragchannelid, ulong messageId,
        long expiresAt, int pvotes, int nvotes)
    {
        try
        {
            ulong votechannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));
            DiscordChannel votechannel = await client.GetChannelAsync(votechannelid);
            DiscordMessage message = await votechannel.GetMessageAsync(messageId);

            int color = Helperfunctions.getVoteColor(pvotes, nvotes);
            DiscordEmbed vembed = MessageGenerator.getVoteEmbedInRunning(votechannel, expiresAt, nvotes, pvotes, color);

            var votebuttons = new List<DiscordButtonComponent>
            {
                new(ButtonStyle.Secondary, "vote_yes_" + antragchannelid, "👍 Ja"),
                new(ButtonStyle.Secondary, "vote_no_" + antragchannelid, "👎 Nein")
            };

            DiscordMessageBuilder builder = new DiscordMessageBuilder()
                .WithContent(Helperfunctions.getTeamPing())
                .AddComponents(votebuttons)
                .AddEmbed(vembed);

            await message.ModifyAsync(builder);
        }
        catch (Exception ex)
        {
            //
        }
    }

    public static async Task UpdateSingleVoteMessage(DiscordClient client, ulong messageId)
    {
        try
        {
            string dbstring = Helperfunctions.DbString();
            await using var conn = new NpgsqlConnection(dbstring);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM abstimmungen WHERE message_id = @msgid", conn);
            cmd.Parameters.AddWithValue("msgid", (long)messageId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                long antragchannelid = reader.GetInt64(0);
                long expiresAt = reader.GetInt64(2);
                int pvotes = reader.GetInt32(4);
                int nvotes = reader.GetInt32(5);
                Console.WriteLine($"{antragchannelid}: {messageId}");
                Console.WriteLine($"Updating vote message {messageId} in channel {antragchannelid} with expires at {expiresAt}, pvotes: {pvotes}, nvotes: {nvotes}");
                await UpdateSingleVoteMessage(client, antragchannelid, messageId, expiresAt, pvotes, nvotes);
            }
        }
        catch (Exception ex)
        {
            //
        }

    }
}