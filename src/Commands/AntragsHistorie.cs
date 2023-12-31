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
    
    [SlashCommand("recent", "Zeigt die letzten 5 Anträge an.")]
    public static async Task RecentAnträge(InteractionContext ctx)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());
        var constring = Helperfunctions.DbString();
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Lade Daten... <a:agcutils_loading:952604537515024514>"));
        await Task.Delay(1000);
        await using var con = new NpgsqlConnection(constring);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT * FROM antragsverlauf ORDER BY timestamp DESC LIMIT 5", con);
        await using var reader = await cmd.ExecuteReaderAsync();
        var eb = new DiscordEmbedBuilder();
        eb.WithColor(DiscordColor.Blurple);
        // schema user_id bigint, antrags_id varchar, entbannt boolean, reason text, mod_id bigint, timestamp bigint
        int i = 0;
        int pos = 0;
        int neg = 0;
        while (await reader.ReadAsync())
        {
            var user = await ctx.Client.GetUserAsync((ulong)reader.GetInt64(0));
            var antragsnummer = reader.GetString(1);
            bool unbanned = reader.GetBoolean(2);
            var grund = reader.GetString(3);
            var mod = await ctx.Client.GetUserAsync((ulong)reader.GetInt64(4));
            var timestamp = reader.GetInt64(5);
            
            if (unbanned)
            {
                pos++;
            }
            else
            {
                neg++;
            }
            
            eb.AddField(new DiscordEmbedField($"Antragsnummer: {antragsnummer}", 
                $"> User: {user.Mention} ({user.Id})\n" +
                $"> Entbannung: {Helperfunctions.BoolToEmoji(unbanned)}\n" +
                $"> Grund: ``{grund}``\n" +
                $"> Bearbeitet von: {mod.Mention} ({mod.Id})\n" +
                $"> Zeitpunkt: <t:{timestamp}:f> (<t:{timestamp}:R>)"));
            i++;
            await Task.Delay(100);
        }
        eb.WithTitle($"Letzte {i} Anträge");
        eb.WithFooter($"Entbannt: {pos} | Nicht: {neg}");

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().AddEmbed(eb));
    }

    private static bool isUnbanned(string value)
    {
        return value == "Angenommen";
    }
}