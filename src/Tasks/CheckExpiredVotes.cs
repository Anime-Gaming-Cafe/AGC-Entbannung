#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
                await using var context = AgcDbContextFactory.CreateDbContext();
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                var expiredVotes = await context.Abstimmungen
                    .Where(a => a.ExpiresAt < currentTime || a.EndPending)
                    .ToListAsync();
                
                // Process each expired vote
                foreach (var vote in expiredVotes)
                {
                    try
                    {
                        DiscordChannel antragschannel = await client.GetChannelAsync((ulong)vote.ChannelId);
                        var messageid = (ulong)vote.MessageId;
                        
                        // get messages from id in vote channel
                        ulong votechannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));
                        DiscordChannel votechannel = await client.GetChannelAsync(votechannelid);
                        DiscordMessage message = await votechannel.GetMessageAsync(messageid);
                        
                        var nowtimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var colorInt = Helperfunctions.getVoteColor(vote.PositiveVotes, vote.NegativeVotes);
                        int teamMemberCount = await Helperfunctions.GetTeamMemberCount(client);
                        DiscordEmbed emb = MessageGenerator.getVoteEmbedFinished(antragschannel, nowtimestamp,
                            vote.NegativeVotes, vote.PositiveVotes, colorInt, teamMemberCount);
                        
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
                    catch (Exception ex)
                    {
                        client.Logger.LogError(ex, "Error processing individual expired vote");
                    }
                }

                // Remove expired votes and their team member votes
                if (expiredVotes.Any())
                {
                    var expiredVoteIds = expiredVotes.Select(v => (v.ChannelId + v.MessageId).ToString()).ToList();
                    var teamlerVotesToDelete = await context.AbstimmungenTeamler
                        .Where(t => expiredVoteIds.Contains(t.VoteId))
                        .ToListAsync();
                    
                    context.AbstimmungenTeamler.RemoveRange(teamlerVotesToDelete);
                    context.Abstimmungen.RemoveRange(expiredVotes);
                    await context.SaveChangesAsync();
                }
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