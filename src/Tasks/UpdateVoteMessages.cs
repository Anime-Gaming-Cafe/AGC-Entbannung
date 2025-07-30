using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Microsoft.EntityFrameworkCore;

namespace AGC_Entbannungssystem.Tasks;

public static class UpdateVoteMessages
{
    public static async Task Run(DiscordClient client)
    {
        while (true)
        {
            try
            {
                await using var context = AgcDbContextFactory.CreateDbContext();
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                var activeVotes = await context.Abstimmungen
                    .Where(a => a.ExpiresAt > currentTime)
                    .Select(a => new { a.ChannelId, a.MessageId, a.ExpiresAt, a.PositiveVotes, a.NegativeVotes })
                    .ToListAsync();

                foreach (var vote in activeVotes)
                {
                    await UpdateSingleVoteMessage(client, vote.ChannelId, (ulong)vote.MessageId, 
                        vote.ExpiresAt, vote.PositiveVotes, vote.NegativeVotes);
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
            
            DiscordChannel antragchannel = await client.GetChannelAsync((ulong)antragchannelid);

            int teamMemberCount = await Helperfunctions.GetTeamMemberCount(client);
            int color = Helperfunctions.getVoteColor(pvotes, nvotes);
            DiscordEmbed vembed = MessageGenerator.getVoteEmbedInRunning(antragchannel, expiresAt, nvotes, pvotes, color, teamMemberCount);

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
            await using var context = AgcDbContextFactory.CreateDbContext();
            var vote = await context.Abstimmungen
                .FirstOrDefaultAsync(a => a.MessageId == (long)messageId);

            if (vote != null)
            {
                Console.WriteLine($"{vote.ChannelId}: {messageId}");
                Console.WriteLine(
                    $"Updating vote message {messageId} in channel {vote.ChannelId} with expires at {vote.ExpiresAt}, pvotes: {vote.PositiveVotes}, nvotes: {vote.NegativeVotes}");
                await UpdateSingleVoteMessage(client, vote.ChannelId, messageId, vote.ExpiresAt, vote.PositiveVotes, vote.NegativeVotes);
            }
        }
        catch (Exception ex)
        {
            //
        }
    }
}