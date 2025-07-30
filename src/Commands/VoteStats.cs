using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Microsoft.EntityFrameworkCore;

namespace AGC_Entbannungssystem.Commands;

public sealed class VoteStats : ApplicationCommandsModule
{
    [ApplicationCommandRequirePermissions(Permissions.Administrator)]
    [SlashCommand("votestats", "Zeigt die Abstimmungsstatistiken an.")]
    public async Task votestats(InteractionContext ctx,
        [Option("antragskanal", "Der Kanal, in dem der Antrag gestellt wurde.")]
        DiscordChannel channel)
    {
        if (!await isChannelInVotingChannel(channel))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Dieser Kanal ist nicht in einer Abstimmung!").AsEphemeral());
            return;
        }

        await using var context = AgcDbContextFactory.CreateDbContext();

        var abstimmung = await context.Abstimmungen
            .FirstOrDefaultAsync(a => a.ChannelId == (long)channel.Id);

        if (abstimmung == null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Keine Abstimmung für diesen Kanal gefunden!").AsEphemeral());
            return;
        }

        long messageId = abstimmung.MessageId;
        long expiresAt = abstimmung.ExpiresAt;
        int pvotes = abstimmung.PositiveVotes;
        int nvotes = abstimmung.NegativeVotes;
        var createdby = abstimmung.CreatedBy;
        
        DiscordChannel voteChannel = await ctx.Client.GetChannelAsync(ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId")));
        DiscordMessage voteMessage = await voteChannel.GetMessageAsync((ulong)messageId);
        var voteid = ((long)voteMessage.Id + (long)voteChannel.Id).ToString();
        int color = Helperfunctions.getVoteColor(pvotes, nvotes);

        var teamlerVotes = await context.AbstimmungenTeamler
            .Where(t => t.VoteId == voteid)
            .ToListAsync();

        var positiveVotes = new List<string>();
        var negativeVotes = new List<string>();

        foreach (var vote in teamlerVotes)
        {
            if (vote.VoteValue == 1)
            {
                positiveVotes.Add(IdToMention(vote.UserId));
            }
            else if (vote.VoteValue == 0)
            {
                negativeVotes.Add(IdToMention(vote.UserId));
            }
        }

        var voteEmbed = new DiscordEmbedBuilder()
            .WithTitle("Abstimmungsstatistiken")
            .WithDescription($"Abstimmung für den Antrag ``{channel.Name}`` | ({channel.Mention})\n" +
                             $"**Positive Stimmen:** {pvotes}\n" +
                             $"**User, die Positiv abgestimmt haben:**\n" +
                                $"{(positiveVotes.Count > 0 ? string.Join(", ", positiveVotes) : "Keine positiven Stimmen")}\n\n" +
                             $"**Negative Stimmen:** {nvotes}\n" +
                                $"**User, die Negativ abgestimmt haben:**\n" +
                                    $"{(negativeVotes.Count > 0 ? string.Join(", ", negativeVotes) : "Keine negativen Stimmen")}\n\n" +
                             $"Die Abstimmung endet am <t:{expiresAt}:f> (<t:{expiresAt}:R>)\n\n" +
                             $"**Abstimmung erstellt von:** {IdToMention(createdby)}\n" +
                             $"**Abstimmung ID:** {voteid}\n" +
                             $"Die Abstimmung ist noch nicht beendet. Endet: <t:{expiresAt}:f> (<t:{expiresAt}:R>)")
            .WithColor(color);
        
        // usual format is antrag-0000
        var antragsnummer = channel.Name.Split('-').LastOrDefault()?.Trim();
        var jumpToVoteButton = new DiscordLinkButtonComponent(
            $"https://discord.com/channels/{ctx.Guild.Id}/{voteChannel.Id}/{voteMessage.Id}", $"Zur Abstimmung springen");
        var jumpToAntragButton = new DiscordLinkButtonComponent(
            $"https://discord.com/channels/{ctx.Guild.Id}/{channel.Id}", $"Zum Antrag {antragsnummer} springen");
        var responseBuilder = new DiscordInteractionResponseBuilder()
            .AddEmbed(voteEmbed.Build())
            .AddComponents(jumpToAntragButton, jumpToVoteButton)
            .AsEphemeral();
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
    }
    
    private static string IdToMention(ulong id)
    {
        return $"<@{id}>";
    }

    private static string IdToMention(long id)
    {
        return IdToMention((ulong)id);
    }
    
    private static async Task<bool> isChannelInVotingChannel(DiscordChannel channel)
    {
        await using var context = AgcDbContextFactory.CreateDbContext();
        return await context.Abstimmungen
            .AnyAsync(a => a.ChannelId == (long)channel.Id);
    }
}