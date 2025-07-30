#region

using AGC_Entbannungssystem.Entities;
using AGC_Entbannungssystem.Entities.Database;
using AGC_Entbannungssystem.Enums;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

#endregion

namespace AGC_Entbannungssystem.Helpers;

public static class Helperfunctions
{
    public static string GenerateCaseId()
    {
        var guid = Guid.NewGuid().ToString("N");
        return guid.Substring(0, 10);
    }

    public static string DbString()
    {
        string databasename = BotConfigurator.GetConfig("Database", "DatabaseName");
        string dbuser = BotConfigurator.GetConfig("Database", "DatabaseUser");
        string dbpassword = BotConfigurator.GetConfig("Database", "DatabasePassword");
        string dbhost = BotConfigurator.GetConfig("Database", "DatabaseHost");
        return $"Host={dbhost};Username={dbuser};Password={dbpassword};Database={databasename}";
    }

    public static string getTeamPing()
    {
        return $"<@&{BotConfigurator.GetConfig("MainConfig", "PingRoleId")}>";
    }

    public static async Task addVoteToAntrag(ComponentInteractionCreateEventArgs interaction, bool positiveVote)
    {
        var existingVote = await UserHasVoted(interaction);
        if (existingVote != null)
        {
            await interaction.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Du hast bereits abgestimmt! Entferne zuerst deine aktuelle Stimme.")
                    .AsEphemeral());
            return;
        }

        using var context = AgcDbContextFactory.CreateDbContext();
        
        var messageId = (long)interaction.Message.Id;
        var abstimmung = await context.Abstimmungen.FirstOrDefaultAsync(a => a.MessageId == messageId);
        
        if (abstimmung != null)
        {
            // Update vote count
            if (positiveVote)
                abstimmung.PositiveVotes++;
            else
                abstimmung.NegativeVotes++;

            // Add team member vote
            var teamlerVote = new AbstimmungTeamler
            {
                VoteId = (interaction.Channel.Id + interaction.Message.Id).ToString(),
                UserId = (long)interaction.User.Id,
                VoteValue = positiveVote ? 1 : 0
            };
            context.AbstimmungenTeamler.Add(teamlerVote);

            await context.SaveChangesAsync();
            
            await UpdateEndPendingStatus(CurrentApplicationData.Client, interaction.Message.Id, 
                abstimmung.PositiveVotes, abstimmung.NegativeVotes);
        }
    }


    public static async Task removeVoteFromAntrag(ComponentInteractionCreateEventArgs interaction)
    {
        var existingVote = await UserHasVoted(interaction);
        if (existingVote == null)
        {
            await interaction.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Du hast noch nicht abgestimmt!")
                    .AsEphemeral());
            return;
        }

        using var context = AgcDbContextFactory.CreateDbContext();
        
        var messageId = (long)interaction.Message.Id;
        var abstimmung = await context.Abstimmungen.FirstOrDefaultAsync(a => a.MessageId == messageId);
        
        if (abstimmung != null)
        {
            // Update vote count
            if (existingVote == VoteType.Positive)
                abstimmung.PositiveVotes--;
            else
                abstimmung.NegativeVotes--;

            // Remove team member vote
            var voteId = ((long)interaction.Channel.Id + (long)interaction.Message.Id).ToString();
            var teamlerVote = await context.AbstimmungenTeamler
                .FirstOrDefaultAsync(t => t.VoteId == voteId && t.UserId == (long)interaction.User.Id);
            
            if (teamlerVote != null)
            {
                context.AbstimmungenTeamler.Remove(teamlerVote);
            }

            await context.SaveChangesAsync();
            
            await UpdateEndPendingStatus(CurrentApplicationData.Client, interaction.Message.Id, 
                abstimmung.PositiveVotes, abstimmung.NegativeVotes);
        }
    }


    public static async Task<VoteType?> UserHasVoted(ComponentInteractionCreateEventArgs interaction)
    {
        using var context = AgcDbContextFactory.CreateDbContext();
        
        var voteId = ((long)interaction.Channel.Id + (long)interaction.Message.Id).ToString();
        var teamlerVote = await context.AbstimmungenTeamler
            .FirstOrDefaultAsync(t => t.VoteId == voteId && t.UserId == (long)interaction.User.Id);

        if (teamlerVote != null)
        {
            return teamlerVote.VoteValue == 1 ? VoteType.Positive : VoteType.Negative;
        }

        return null;
    }


    public static async Task<List<BannSystemReport?>?> GetBannsystemReports(DiscordUser user)
    {
        using HttpClient client = new();
        string apiKey = BotConfigurator.GetConfig("ModHQConfig", "API_Key");
        string apiUrl = BotConfigurator.GetConfig("ModHQConfig", "API_URL");

        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
        HttpResponseMessage response = await client.GetAsync($"{apiUrl}{user.Id}");
        if (response.IsSuccessStatusCode)
        {
            string json = await response.Content.ReadAsStringAsync();
            UserInfoApiResponse apiResponse = JsonConvert.DeserializeObject<UserInfoApiResponse>(json);
            List<BannSystemReport> data = apiResponse.reports;
            return data;
        }

        return null;
    }

    public static async Task<List<BannSystemReport?>?> BSReportToWarn(DiscordUser user)
    {
        try
        {
            var data = await GetBannsystemReports(user);

            return data.Select(warn => new BannSystemReport
            {
                reportId = warn.reportId,
                authorId = warn.authorId,
                reason = warn.reason,
                timestamp = warn.timestamp,
                active = warn.active
            }).ToList();
        }
        catch (Exception e)
        {
            // ignored
        }

        return new List<BannSystemReport>();
    }

    public static async Task<List<AntragshistorieDaten>> GetAntragshistorie(DiscordUser user)
    {
        using var context = AgcDbContextFactory.CreateDbContext();
        
        var antragsverlauf = await context.Antragsverlauf
            .Where(a => a.UserId == (long)user.Id)
            .ToListAsync();

        return antragsverlauf.Select(a => new AntragshistorieDaten
        {
            user_id = a.UserId,
            antragsnummer = a.Antragsnummer,
            unbanned = a.Unbanned,
            grund = a.Grund,
            mod_id = a.ModId,
            timestamp = a.Timestamp
        }).ToList();
    }

    public static async Task RegelPhase1(DiscordUser user)
    {
        var unixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        using var context = AgcDbContextFactory.CreateDbContext();
        
        var existingConfirmation = await context.RequirementConfirmations
            .FirstOrDefaultAsync(r => r.UserId == (long)user.Id);

        if (existingConfirmation != null)
        {
            existingConfirmation.Time = unixTimestamp;
        }
        else
        {
            var newConfirmation = new RequirementConfirmation
            {
                UserId = (long)user.Id,
                Time = unixTimestamp
            };
            context.RequirementConfirmations.Add(newConfirmation);
        }

        await context.SaveChangesAsync();
    }

    public static async Task<bool> RegelPhase2(DiscordInteraction interaction)
    {
        var user_id = interaction.User.Id;
        using var context = AgcDbContextFactory.CreateDbContext();
        
        var confirmation = await context.RequirementConfirmations
            .FirstOrDefaultAsync(r => r.UserId == (long)user_id);

        if (confirmation != null)
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            return now - confirmation.Time >= 180;
        }

        return true;
    }

    public static async Task RegelPhase3(DiscordUser user)
    {
        ulong user_id = user.Id;
        using var context = AgcDbContextFactory.CreateDbContext();
        
        var confirmation = await context.RequirementConfirmations
            .FirstOrDefaultAsync(r => r.UserId == (long)user_id);

        if (confirmation != null)
        {
            context.RequirementConfirmations.Remove(confirmation);
            await context.SaveChangesAsync();
        }
    }

    public static string BoolToEmoji(bool value)
    {
        return value ? "<:angenommen:1190335045341282314>" : "<:abgelehnt:1190335046591205426>";
    }

    public static bool HasActiveBannSystemReport(List<BannSystemReport> reports)
    {
        return reports.Any(report => report.active);
    }

    public static int getVoteColor(int pvotes, int nvotes)
    {
        if (pvotes == 0 && nvotes == 0)
            return -1; // No votes → gray (default)
        if (pvotes == nvotes)
            return 2; // Tie → yellow
        if (pvotes > nvotes)
            return 1; // More positive votes → green

        return 0; // More negative votes → red
    }

    public static async Task<int> GetTeamMemberCount(DiscordClient client)
    {
        try
        {
            DiscordGuild unbanGuild = await client.GetGuildAsync(GlobalProperties.UnbanServerId);
            DiscordRole pingRole = unbanGuild.GetRole(GlobalProperties.PingRoleId);
            
            int teamMemberCount = unbanGuild.Members.Count(m => m.Value.Roles.Any(r => r.Id == pingRole.Id));
            return teamMemberCount;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static async Task<bool> CheckVoteThreshold(DiscordClient client,  int totalVotes)
    {
        try
        {
            int teamMemberCount = await GetTeamMemberCount(client);
            if (teamMemberCount == 0) return false; 
            
            double votePercentage = (double)totalVotes / teamMemberCount * 100;
            
            return votePercentage > 70;
        }
        catch (Exception)
        {
            return false;
        }
    }

    
    public static async Task<bool> IsVoteOutcomeDecided(DiscordClient client, int pvotes, int nvotes)
    {
        try
        {
            int teamMemberCount = await GetTeamMemberCount(client);
            if (teamMemberCount == 0) return false;
            
            int remainingVotes = teamMemberCount - (pvotes + nvotes);
            
            if (pvotes > teamMemberCount / 2)
                return true;
                
            if (nvotes > teamMemberCount / 2)
                return true;
                
            if (pvotes + remainingVotes < nvotes)
                return true;
                
            if (nvotes + remainingVotes < pvotes)
                return true;
                
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }


    public static async Task UpdateEndPendingStatus(DiscordClient client, ulong messageId, int pvotes, int nvotes)
    {
        try
        {
            int totalVotes = pvotes + nvotes;
            bool thresholdReached = await CheckVoteThreshold(client, totalVotes);
            bool isTie = pvotes == nvotes;
            bool outcomeDecided = await IsVoteOutcomeDecided(client, pvotes, nvotes);
          
            using var context = AgcDbContextFactory.CreateDbContext();
            
            var abstimmung = await context.Abstimmungen
                .FirstOrDefaultAsync(a => a.MessageId == (long)messageId);
            
            if (abstimmung != null)
            {
                // Never end a vote if there's a tie
                abstimmung.EndPending = !isTie && ((thresholdReached) || outcomeDecided);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }
    }
}