using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;

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
                new DiscordInteractionResponseBuilder().WithContent("Dieser Kanal ist nicht in einer Abstimmung!"));
            return;
        }

        string dbstring = Helperfunctions.DbString();
        await using var conn = new Npgsql.NpgsqlConnection(dbstring);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand("SELECT * FROM abstimmungen WHERE antragskanal = @channelid", conn);
        cmd.Parameters.AddWithValue("channelid", channel.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Keine Abstimmung für diesen Kanal gefunden!"));
            return;
        }
        long messageId = reader.GetInt64(1);
        long expiresAt = reader.GetInt64(2);
        int pvotes = reader.GetInt32(4);
        int nvotes = reader.GetInt32(5);
        bool endpending = reader.GetBoolean(6);
        var createdby = reader.GetString(3);
        
        await reader.CloseAsync();
        DiscordChannel voteChannel = await ctx.Client.GetChannelAsync(ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId")));
        DiscordMessage voteMessage = await voteChannel.GetMessageAsync((ulong)messageId);
        var voteid = (long)voteMessage.Id + (long)voteChannel.Id;
        int color = Helperfunctions.getVoteColor(pvotes, nvotes);
        await using var cmd2 = new Npgsql.NpgsqlCommand("SELECT * FROM abstimmungen_teamler WHERE abstimmung_id = @messageid", conn);
        cmd2.Parameters.AddWithValue("messageid", messageId);
        await using var reader2 = await cmd2.ExecuteReaderAsync();
        var positiveVotes = new List<string>();
        var negativeVotes = new List<string>();
        while (await reader2.ReadAsync())
        {
            long userId = reader2.GetInt64(0);
            bool voteValue = reader2.GetBoolean(1);
            if (voteValue)
            {
                positiveVotes.Add(IdToMention(userId));
            }
            else
            {
                negativeVotes.Add(IdToMention(userId));
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
                             $"**Abstimmung erstellt von:** {IdToMention(long.Parse(createdby))}\n" +
                             $"**Abstimmung ID:** {voteid}\n" +
                             $"{(endpending ? $"Die Abstimmung ist noch nicht beendet. Endet: <t:{expiresAt}:f> (<t:{expiresAt}:R>)" : "Die Abstimmung ist bereits beendet.")}")
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
        string dbstring = Helperfunctions.DbString();
        await using var conn = new Npgsql.NpgsqlConnection(dbstring);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand("SELECT * FROM abstimmungen WHERE antragskanal = @channelid", conn);
        cmd.Parameters.AddWithValue("channelid", channel.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync();
    }
}
    
    