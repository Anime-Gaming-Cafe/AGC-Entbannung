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
using Microsoft.Extensions.Logging;

#endregion

namespace AGC_Entbannungssystem.Commands;

public sealed class DisableAutomationsCommand : ApplicationCommandsModule
{
    [ApplicationRequireStaffRole]
    [SlashCommand("disableautomations", "Schaltet die 24h-Auto-Aktionen für dieses Ticket ab oder wieder an.")]
    public static async Task DisableCommand(InteractionContext ctx)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        if (!ctx.Channel.Name.StartsWith("antrag-"))
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("⚠️ Dieser Befehl funktioniert nur in Antrag-Channels."));
            return;
        }

        await using var context = AgcDbContextFactory.CreateDbContext();
        var channelId = (long)ctx.Channel.Id;
        var existing = await context.DisabledAutomations
            .FirstOrDefaultAsync(d => d.ChannelId == channelId);

        bool wasDisabled;
        if (existing != null)
        {
            context.DisabledAutomations.Remove(existing);
            await context.SaveChangesAsync();
            wasDisabled = false;
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent(
                    "✅ Automatisierungen für dieses Ticket wieder **aktiviert**."));
        }
        else
        {
            var disabled = new DisabledAutomation
            {
                ChannelId = channelId,
                DisabledBy = (long)ctx.User.Id,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            context.DisabledAutomations.Add(disabled);
            await context.SaveChangesAsync();
            wasDisabled = true;
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent(
                    "✅ Automatisierungen für dieses Ticket **deaktiviert**. Der Watcher überspringt dieses Ticket."));
        }

        try
        {
            ulong logchannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsLogChannelId"));
            DiscordChannel logchannel = ctx.Guild.GetChannel(logchannelid);
            var logembed = new DiscordEmbedBuilder();
            if (wasDisabled)
            {
                logembed.WithTitle("Automatisierungen deaktiviert");
                logembed.WithDescription(
                    $"Automatisierungen für Antrag <#{ctx.Channel.Id}> (`#{ctx.Channel.Name}`) wurden durch {ctx.User.Mention} ({ctx.User.Id}) **deaktiviert**.");
                logembed.WithColor(DiscordColor.Orange);
            }
            else
            {
                logembed.WithTitle("Automatisierungen wieder aktiviert");
                logembed.WithDescription(
                    $"Automatisierungen für Antrag <#{ctx.Channel.Id}> (`#{ctx.Channel.Name}`) wurden durch {ctx.User.Mention} ({ctx.User.Id}) **wieder aktiviert**.");
                logembed.WithColor(DiscordColor.Green);
            }
            await logchannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(logembed));
        }
        catch (Exception ex)
        {
            ctx.Client.Logger.LogError(ex, "Konnte /disableautomations nicht in AbstimmungsLog loggen");
        }
    }
}
