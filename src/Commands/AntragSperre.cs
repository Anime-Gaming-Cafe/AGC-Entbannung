using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AGC_Entbannungssystem.Commands;

public class AntragSperre : ApplicationCommandsModule
{
    [ApplicationRequireStaffRole]
    [SlashCommand("sperre", "Sperrt einen User von der Antragstellung für eine bestimmte Zeit.")]
    public static async Task SperreCommand(InteractionContext ctx,
        [Option("user", "Der User, der gesperrt werden soll.")] DiscordUser user, [Option("antragsnummer", "Die Antragsnummer."), MinimumLength(4)] string antragsnummer,
        [Option("grund", "Der Grund für die Sperre.")] string reason)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());
        var constring = Helperfunctions.DbString();
        
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Prüfe Eingaben..."));

        
        if (!antragsnummer.All(char.IsDigit))
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("⚠️ Die Antragsnummer ist ungültig!"));
            return;
        }
        
        
        ulong roleid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreRoleId"));
        DiscordRole role = ctx.Guild.GetRole(roleid);
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Prüfe, ob der User bereits gesperrt ist..."));
        await using var con = new NpgsqlConnection(constring);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT * FROM antragssperre WHERE user_id = @userid", con);
        cmd.Parameters.AddWithValue("userid", (long)user.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (reader.HasRows)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("⚠️ Der User ist bereits gesperrt!"));
            return;
        }
        
        await con.CloseAsync();
        
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Der User ist nicht gesperrt. Sperre den User für die angegebene Zeit..."));
        try
        {
            DiscordMember member = await ctx.Guild.GetMemberAsync(user.Id);
            await member.GrantRoleAsync(role, "Antragssperre");
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent($"✅ Der User wurde gesperrt!"));
        }
        catch (Exception e)
        {
            ctx.Client.Logger.LogError(e, "Error while granting role to user. User is not in guild.");
        }
        var timestamp = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds();
        await using var con2 = new NpgsqlConnection(constring);
        await con2.OpenAsync();
        await using var cmd2 = new NpgsqlCommand("INSERT INTO antragssperre (user_id, reason, expires_at) VALUES (@userid, @reason, @timestamp)", con2);
        cmd2.Parameters.AddWithValue("userid", (long)user.Id);
        cmd2.Parameters.AddWithValue("reason", reason);
        cmd2.Parameters.AddWithValue("timestamp", timestamp);
        await cmd2.ExecuteNonQueryAsync();
        await con2.CloseAsync();
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent($"✅ Die Sperre wurde eingetragen!"));
        DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
        embed.WithDescription($"{DateTime.UtcNow.Timestamp(TimestampFormat.LongDateTime)} - {user.Mention} ({user.Id}) - Antrag {antragsnummer} - ``{reason}`` -> Gesperrt bis: <t:{timestamp}:f> ( <t:{timestamp}:R> )");
        embed.WithFooter($"Gesperrt durch {ctx.User.UsernameWithDiscriminator} ({ctx.User.Id})", ctx.User.AvatarUrl);
        ulong infochannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreInfoChannelId"));
        DiscordChannel ichan = ctx.Guild.GetChannel(infochannelid);
        await ichan.SendMessageAsync(embed);
    }
}