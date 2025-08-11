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

public sealed class RemoveAntragssperreCommand : ApplicationCommandsModule
{
    [ApplicationRequireStaffRole]
    [ApplicationCommandRequireGuildOwnerAttribute]
    [SlashCommand("removeantragssperre", "Entfernt die Antragssperre für einen User.")]
    public static async Task AbstimmungCommand(InteractionContext ctx,
        [Option("user", "Der User, dessen Antragssperre entfernt werden soll.")]
        DiscordUser user)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        await using var context = AgcDbContextFactory.CreateDbContext();
        
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Prüfe, ob bereits eine Antragssperre existiert..."));
        var existingSperre = await context.Antragssperren
            .FirstOrDefaultAsync(a => a.UserId == (long)ctx.User.Id);
        if (existingSperre == null)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("Es existiert keine Antragssperre für diesen User!"));
            return;
        }
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Antragssperre gefunden. Entferne die Sperre..."));
        existingSperre.ExpiresAt = 0;
        await context.SaveChangesAsync();
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Die Antragssperre wurde erfolgreich entfernt! Der User wird innerhalb einer Minute automatisch freigegeben."));
    }
}