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
using DisCatSharp.Exceptions;
using DisCatSharp.Interactivity.Extensions;
using Microsoft.EntityFrameworkCore;

#endregion

namespace AGC_Entbannungssystem.Commands;

public class RevokeBan : ApplicationCommandsModule
{
    [ApplicationCommandRequirePermissions(Permissions.Administrator)]
    [SlashCommand("revokeban", "Entbannt einen User von AGC.")]
    public static async Task RevokeBanCommand(InteractionContext ctx,
        [Option("user", "Der User, der entbannt werden soll.")]
        DiscordUser user,
        [Option("antragsnummer", "Die Antragsnummer."), MinimumLength(4), MaximumLength(4)]
        string antragsnummer,
        [Autocomplete(typeof(RevokeBanCommandAutocompletionProvider))]
        [Option("Grund", "Der Grund für die Entbannung", true)]
        string reason)
    {
        DiscordGuild mainGuild = await ctx.Client.GetGuildAsync(GlobalProperties.MainGuildId);
        DiscordBan? banentry;


        bool isBanned = false;
        try
        {
            banentry = await mainGuild.GetBanAsync(user.Id);
            isBanned = true;
        }
        catch (NotFoundException)
        {
            banentry = null;
            isBanned = false;
        }

        string banreason = banentry?.Reason ?? "Kein Grund angegeben.";
        if (!isBanned)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent($"Der User ``{user.Id}`` ist nicht gebannt."));
            return;
        }

        // check if any channel contains the antragsnummer any
        var channels = await ctx.Guild.GetChannelsAsync();
        DiscordChannel? channel = channels.FirstOrDefault(x => x.Name.Contains(antragsnummer));
        if (channel == null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent(
                    $"Es wurde kein Antrag mit der Antragsnummer ``{antragsnummer}`` gefunden."));
            return;
        }

        await using var context = AgcDbContextFactory.CreateDbContext();
        var activeVote = await context.Abstimmungen
            .FirstOrDefaultAsync(a => a.ChannelId == (long)channel.Id);

        if (activeVote != null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent(
                    $"Der Antrag mit der Antragsnummer ``{antragsnummer}`` ist noch in Abstimmung. Bitte warte bis die Abstimmung beendet ist."));
            return;
        }

        var caseid = Helperfunctions.GenerateCaseId();
        var confirmEmbedBuilder = new DiscordEmbedBuilder()
            .WithTitle("Überprüfe deine Eingabe | Aktion: Entbannung")
            .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
            .WithDescription($"Bitte überprüfe deine Eingabe und bestätige mit ✅ um fortzufahren.\n\n" +
                             $"__Users:__\n" +
                             $"```{user.UsernameWithDiscriminator}```\n__Grund:__```{reason}```")
            .WithColor(BotConfigurator.GetEmbedColor());
        List<DiscordButtonComponent> buttons = new(2)
        {
            new DiscordButtonComponent(ButtonStyle.Secondary, $"unban_accept_{caseid}", "✅"),
            new DiscordButtonComponent(ButtonStyle.Secondary, $"unban_deny_{caseid}", "❌")
        };
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(confirmEmbedBuilder).AddComponents(buttons));
        var interactivity = ctx.Client.GetInteractivity();
        DiscordMessage msg = await ctx.GetOriginalResponseAsync();
        var result = await interactivity.WaitForButtonAsync(msg, ctx.User, TimeSpan.FromSeconds(300));
        buttons.ForEach(x => x.Disable());
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(confirmEmbedBuilder).AddComponents(buttons));
        if (result.TimedOut)
        {
            confirmEmbedBuilder.WithTitle("Entbannung abgebrochen")
                .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
                .WithDescription(
                    "Der Ban wurde abgebrochen.\n\nGrund: Zeitüberschreitung. ⚠️")
                .WithColor(DiscordColor.Red);
            var whb = new DiscordWebhookBuilder();
            whb.AddEmbed(confirmEmbedBuilder);
            await ctx.EditResponseAsync(whb);
            return;
        }

        if (result.Result.Id == "unban_deny_{caseid}")
        {
            confirmEmbedBuilder.WithTitle("Entbannung abgebrochen")
                .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
                .WithDescription(
                    "Die Entbannung wurde abgebrochen.\n\nGrund: Abgebrochen vom ausführer! ⚠️")
                .WithColor(DiscordColor.Red);
            var whb = new DiscordWebhookBuilder();
            whb.AddEmbed(confirmEmbedBuilder);
            await ctx.EditResponseAsync(whb);
            return;
        }

        if (result.Result.Id == $"unban_accept_{caseid}")
        {
            confirmEmbedBuilder.WithTitle("Entbannung bestätigt")
                .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
                .WithDescription("Die Entbannung wird durchgeführt.")
                .WithColor(DiscordColor.Green);
            var whb = new DiscordWebhookBuilder();
            whb.AddEmbed(confirmEmbedBuilder);
            await ctx.EditResponseAsync(whb);

            await mainGuild.UnbanMemberAsync(user.Id, reason);
            var flagstring = $"Durch Antrag entbannt. \nUrsprünglicher Banngrund: ``{banreason}`` \n" +
                             $"__Details:__\n Entbanngrund: ``{reason}``\n Antrags-ID: ``{antragsnummer}``\n Entbannungszeitpunkt: {DateTimeOffset.Now.Timestamp()}\n Entbannung ausgeführt von: ``{ctx.User.UsernameWithDiscriminator}`` ({ctx.User.Id})";
            
            var flag = new Flag
            {
                UserId = (long)user.Id,
                PunisherId = (long)ctx.Client.CurrentUser.Id,
                Datum = DateTimeOffset.Now.ToUnixTimeSeconds(),
                Description = flagstring,
                CaseId = caseid
            };
            context.Flags.Add(flag);
            await context.SaveChangesAsync();
            var successEmbedBuilder = new DiscordEmbedBuilder()
                .WithTitle("Entbannung erfolgreich")
                .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
                .WithDescription(
                    $"Der User ``{user.UsernameWithDiscriminator}`` ``{user.Id}`` wurde erfolgreich entbannt. \n" +
                    $"Grund: ```{reason}```")
                .WithColor(DiscordColor.Green);
            var swhb = new DiscordWebhookBuilder();
            swhb.AddEmbed(successEmbedBuilder);
            await ctx.EditResponseAsync(swhb);
        }
    }
}