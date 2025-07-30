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

[ApplicationRequireStaffRole]
[SlashCommandGroup("antragshistorie", "Antrangshistorie Befehle")]
public sealed class AntragsHistorie : ApplicationCommandsModule
{
    [SlashCommand("eintragen", "Fügt einen Antrag zur Historie hinzu.")]
    public static async Task AddAntrag(InteractionContext ctx,
        [Option("Antragsstatus", "Der Status des Antrages (Angenommen, Abgelehnt)")]
        [Choice("Angenommen", "Angenommen")]
        [Choice("Abgelehnt", "Abgelehnt")]
        string status, [Option("Antragsnummer", "Die Antragsnummer")] string antragsnummer,
        [Option("User", "Der User, der den Antrag gestellt hat")]
        DiscordUser user,
        [Option("Antragsgrund", "Der Grund für die Entscheidung")]
        string grund)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Prüfe Eingaben..."));
        await Task.Delay(1000);

        if (!antragsnummer.All(char.IsDigit))
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("⚠️ Die Antragsnummer ist ungültig!"));
            return;
        }

        await using var context = AgcDbContextFactory.CreateDbContext();

        // Check if antragsnummer already exists
        var existingAntrag = await context.Antragsverlauf
            .FirstOrDefaultAsync(a => a.AntragsId == antragsnummer);

        if (existingAntrag != null)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("⚠️ Die Antragsnummer existiert bereits!"));
            return;
        }

        // Create new entry
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Schreibe in die Datenbank..."));
        await Task.Delay(1000);

        var antragsverlauf = new Antragsverlauf
        {
            AntragsId = antragsnummer,
            UserId = (long)user.Id,
            ModId = (long)ctx.User.Id,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Entbannt = isUnbanned(status),
            Reason = grund
        };

        context.Antragsverlauf.Add(antragsverlauf);
        await context.SaveChangesAsync();

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("✅ Antrag wurde erfolgreich eingetragen!"));

        var eb = new DiscordEmbedBuilder();
        if (isUnbanned(status))
        {
            eb.WithTitle("Antrag wurde angenommen!");
            eb.WithColor(DiscordColor.Green);
        }
        else
        {
            eb.WithTitle("Antrag wurde abgelehnt!");
            eb.WithColor(DiscordColor.Red);
        }

        eb.WithDescription(
            $"**Status:** {Helperfunctions.BoolToEmoji(isUnbanned(status))}\n**Bearbeitet von:** {ctx.User.Mention} ({ctx.User.Id}) \n**Antragsnummer:** {antragsnummer}\n**Betroffener User:** {user.Mention} ({user.Id})\n**Grund:** {grund}");
        eb.WithFooter("Entbannungssystem", ctx.User.AvatarUrl);
        eb.WithTimestamp(DateTimeOffset.Now);
        var chid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "HistoryChannelId"));
        var ch = await ctx.Client.GetChannelAsync(chid);
        await ch.SendMessageAsync(eb);
    }

    [SlashCommand("recent", "Zeigt die letzten 5 Anträge an.")]
    public static async Task RecentAnträge(InteractionContext ctx)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Lade Daten... <a:agcutils_loading:952604537515024514>"));
        await Task.Delay(1000);

        await using var context = AgcDbContextFactory.CreateDbContext();

        var recentAnträge = await context.Antragsverlauf
            .OrderByDescending(a => a.Timestamp)
            .Take(5)
            .ToListAsync();

        var eb = new DiscordEmbedBuilder();
        eb.WithColor(DiscordColor.Blurple);

        int pos = 0;
        int neg = 0;

        foreach (var antrag in recentAnträge)
        {
            var user = await ctx.Client.GetUserAsync((ulong)antrag.UserId);
            var mod = await ctx.Client.GetUserAsync((ulong)antrag.ModId);

            if (antrag.Entbannt)
            {
                pos++;
            }
            else
            {
                neg++;
            }

            eb.AddField(new DiscordEmbedField($"Antragsnummer: {antrag.AntragsId}",
                $"> User: {user.Mention} ({user.Id})\n" +
                $"> Entbannung: {Helperfunctions.BoolToEmoji(antrag.Entbannt)}\n" +
                $"> Grund: ``{antrag.Reason}``\n" +
                $"> Bearbeitet von: {mod.Mention} ({mod.Id})\n" +
                $"> Zeitpunkt: <t:{antrag.Timestamp}:f> (<t:{antrag.Timestamp}:R>)"));
            await Task.Delay(100);
        }

        eb.WithTitle($"Letzte {recentAnträge.Count} Anträge");
        eb.WithFooter($"Entbannt: {pos} | Nicht Entbannt: {neg}");

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().AddEmbed(eb));
    }

    private static bool isUnbanned(string value)
    {
        return value == "Angenommen";
    }
}