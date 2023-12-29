#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Npgsql;

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
        var constring = Helperfunctions.DbString();

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Prüfe Eingaben..."));
        await Task.Delay(1000);

        if (!antragsnummer.All(char.IsDigit))
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("⚠️ Die Antragsnummer ist ungültig!"));
            return;
        }

        // check if antragsnummer already exists
        await using var con = new NpgsqlConnection(constring);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT * FROM antragsverlauf WHERE antrags_id = @antragsnummer", con);
        cmd.Parameters.AddWithValue("antragsnummer", antragsnummer);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (reader.HasRows)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("⚠️ Die Antragsnummer existiert bereits!"));
            return;
        }

        // begin to write to db
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Schreibe in die Datenbank..."));
        await Task.Delay(1000);
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long userid = (long)user.Id;
        long modid = (long)ctx.User.Id;
        bool isunbanned = isUnbanned(status);
        await using var con2 = new NpgsqlConnection(constring);
        await con2.OpenAsync();
        await using var cmd2 = new NpgsqlCommand(
            "INSERT INTO antragsverlauf (antrags_id, user_id, mod_id, timestamp, entbannt, reason) VALUES (@antragsnummer, @userid, @modid, @timestamp, @isunbanned, @grund)",
            con2);
        cmd2.Parameters.AddWithValue("antragsnummer", antragsnummer);
        cmd2.Parameters.AddWithValue("userid", userid);
        cmd2.Parameters.AddWithValue("modid", modid);
        cmd2.Parameters.AddWithValue("timestamp", timestamp);
        cmd2.Parameters.AddWithValue("isunbanned", isunbanned);
        cmd2.Parameters.AddWithValue("grund", grund);
        await cmd2.ExecuteNonQueryAsync();
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

    private static bool isUnbanned(string value)
    {
        return value == "Angenommen";
    }
}