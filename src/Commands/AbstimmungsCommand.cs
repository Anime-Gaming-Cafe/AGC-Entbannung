#region

using AGC_Entbannungssystem.Entities.Database;
using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Microsoft.EntityFrameworkCore;

#endregion

namespace AGC_Entbannungssystem.Commands;

public sealed class AbstimmungsCommand : ApplicationCommandsModule
{
    [ApplicationRequireStaffRole]
    [SlashCommand("abstimmung", "Erstellt eine Entbannungsabstimmung.")]
    public static async Task AbstimmungCommand(InteractionContext ctx)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        await using var context = AgcDbContextFactory.CreateDbContext();
        
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Prüfe, ob bereits eine Abstimmung läuft..."));
        
        var existingVote = await context.Abstimmungen
            .FirstOrDefaultAsync(a => a.ChannelId == (long)ctx.Channel.Id);

        if (existingVote != null)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("Es läuft bereits eine Abstimmung in diesem Channel!"));
            return;
        }

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Keine Abstimmung gefunden. Erstelle eine neue Abstimmung..."));
        if (!ctx.Channel.Name.StartsWith("antrag-"))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Dieser Channel ist kein Antrag!"));
            return;
        }

        if (ctx.Channel.Name.Contains("-geschlossen"))
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("Dieser Channel ist kein __offener__ Antrag!"));
            return;
        }

        ulong logchannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsLogChannelId"));
        DiscordChannel logchannel = ctx.Guild.GetChannel(logchannelid);
        var logembed = new DiscordEmbedBuilder();
        logembed.WithTitle("Antrag in die Abstimmung verschoben");
        logembed.WithDescription(
            $"Antrag <#{ctx.Channel.Id}> (`#{ctx.Channel.Name}`) wurde durch {ctx.User.Mention} ({ctx.User.Id}) in die Abstimmung verschoben.");

        ulong votechannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));
        DiscordChannel votechannel = ctx.Guild.GetChannel(votechannelid);
        var idOfAntragChannel = ctx.Channel.Id.ToString();

        var votebuttons = new List<DiscordButtonComponent>
        {
            new(ButtonStyle.Secondary, "vote_yes_" + idOfAntragChannel, "👍 Ja"),
            new(ButtonStyle.Secondary, "vote_no_" + idOfAntragChannel, "👎 Nein")
        };
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now16h = now + 57600;

        var voteembed = MessageGenerator.getVoteEmbedInRunning(ctx.Channel, now16h, 0, 0, 3);
        var votechannelmessage = new DiscordMessageBuilder().AddComponents(votebuttons).AddEmbed(voteembed)
            .WithContent(Helperfunctions.getTeamPing());
        var votemessage = await votechannel.SendMessageAsync(votechannelmessage);
        await logchannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(logembed));

        //move channel to vote category
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Verschiebe Channel in die Abstimmungskategorie..."));
        ulong votecategoryid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "VoteCategoryChannelId"));
        var votecategory = await CurrentApplicationData.Client.GetChannelAsync(votecategoryid);
        await ctx.Channel.ModifyAsync(x => x.Parent = votecategory);
        await Task.Delay(200);
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Verschoben!"));
        var notifyembed = new DiscordEmbedBuilder();
        notifyembed.WithTitle("Status Update");
        notifyembed.WithDescription($"Lieber User, \n" +
                                    $"wir besprechen deinen Antrag nun intern. Du erhältst eine Rückmeldung, sobald die Entscheidung feststeht! \nDies dauert in der Regel 16 Stunden.");
        notifyembed.WithColor(DiscordColor.Green);
        notifyembed.WithFooter("AGC Entbannungssystem");
        await ctx.Channel.SendMessageAsync(notifyembed);
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Abstimmung erstellt!"));
        
        var abstimmung = new Abstimmung
        {
            ChannelId = (long)ctx.Channel.Id,
            MessageId = (long)votemessage.Id,
            ExpiresAt = now16h,
            PositiveVotes = 0,
            NegativeVotes = 0,
            EndPending = false
        };
        context.Abstimmungen.Add(abstimmung);
        await context.SaveChangesAsync();
    }
}