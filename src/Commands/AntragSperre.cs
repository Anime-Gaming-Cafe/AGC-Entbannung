#region

using AGC_Entbannungssystem.AutocompletionProviders;
using AGC_Entbannungssystem.Entities.Database;
using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

#endregion

namespace AGC_Entbannungssystem.Commands;

public class AntragSperre : ApplicationCommandsModule
{
    [ApplicationRequireStaffRole]
    [SlashCommand("sperre", "Sperrt einen User von der Antragstellung für eine bestimmte Zeit.")]
    public static async Task SperreCommand(InteractionContext ctx,
        [Option("user", "Der User, der gesperrt werden soll.")]
        DiscordUser user,
        [Option("antragsnummer", "Die Antragsnummer."), MinimumLength(4), MaximumLength(4)]
        string antragsnummer,
        [Autocomplete(typeof(SperreCommandAutocompletionProvider))] [Option("grund", "Der Grund für die Sperre.", true)]
        string reason)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

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

        await using var context = AgcDbContextFactory.CreateDbContext();
        var existingSperre = await context.Antragssperren
            .FirstOrDefaultAsync(a => a.UserId == (long)user.Id);

        if (existingSperre != null)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("⚠️ Der User ist bereits gesperrt!"));
            return;
        }

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent(
                "Der User ist nicht gesperrt. Sperre den User für die angegebene Zeit..."));
        await Task.Delay(1000);
        try
        {
            DiscordMember member = await ctx.Guild.GetMemberAsync(user.Id);
            await member.GrantRoleAsync(role, "Antragssperre");
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("\u2705 Der User wurde gesperrt!"));
        }
        catch (Exception e)
        {
            ctx.Client.Logger.LogError(e, "Error while granting role to user. User is not in guild.");
        }

        var timestamp = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds();
        var antragssperre = new Antragssperre
        {
            UserId = (long)user.Id,
            Reason = reason,
            ExpiresAt = timestamp
        };
        context.Antragssperren.Add(antragssperre);
        await context.SaveChangesAsync();
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("\u2705 Die Sperre wurde eingetragen!"));
        DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
        embed.WithDescription(
            $"{DateTime.UtcNow.Timestamp(TimestampFormat.LongDateTime)} - {user.Mention} ({user.Id}) - Antrag {antragsnummer} - ``{reason}`` -> Gesperrt bis: <t:{timestamp}:f> ( <t:{timestamp}:R> )");
        embed.WithFooter($"Gesperrt durch {ctx.User.UsernameWithDiscriminator} ({ctx.User.Id})", ctx.User.AvatarUrl);
        ulong infochannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreInfoChannelId"));
        DiscordChannel ichan = ctx.Guild.GetChannel(infochannelid);
        await ichan.SendMessageAsync(embed);
    }
}