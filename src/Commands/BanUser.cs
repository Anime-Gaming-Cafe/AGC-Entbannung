#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.Exceptions;
using DisCatSharp.Interactivity.Extensions;

#endregion

namespace AGC_Entbannungssystem.Commands;

public sealed class BanUserCommand : ApplicationCommandsModule
{
    [ApplicationCommandRequirePermissions(Permissions.Administrator)]
    [SlashCommand("ban", "Bannt einen User vom Server")]
    public async Task BanMember(InteractionContext ctx,
        [Option("user", "Der User, der gebannt werden soll.")]
        DiscordUser user,
        [Option("Grund", "Der Grund für den Ban")]
        string reason)
    {
        var caseid = Helperfunctions.GenerateCaseId();
        var embedBuilder = new DiscordEmbedBuilder()
            .WithTitle($"Du wurdest von {ctx.Guild.Name} gebannt!")
            .WithDescription($"**Begründung:**```{reason}```\n")
            .WithColor(DiscordColor.Red);

        var embed = embedBuilder.Build();
        bool sent;
        var ReasonString =
            $"Grund: {reason} | Von Moderator: {ctx.User.UsernameWithDiscriminator} | Datum: {DateTime.Now:dd.MM.yyyy - HH:mm}";
        // abfrage
        var interactivity = ctx.Client.GetInteractivity();
        var confirmEmbedBuilder = new DiscordEmbedBuilder()
            .WithTitle("Überprüfe deine Eingabe | Aktion: Ban")
            .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
            .WithDescription($"Bitte überprüfe deine Eingabe und bestätige mit ✅ um fortzufahren.\n\n" +
                             $"__Users:__\n" +
                             $"```{user.UsernameWithDiscriminator}```\n__Grund:__```{reason}```")
            .WithColor(BotConfigurator.GetEmbedColor());
        var embed__ = confirmEmbedBuilder.Build();
        List<DiscordButtonComponent> buttons = new(2)
        {
            new DiscordButtonComponent(ButtonStyle.Secondary, $"ban_accept_{caseid}", "✅"),
            new DiscordButtonComponent(ButtonStyle.Secondary, $"ban_deny_{caseid}", "❌")
        };
        var confirmMessage = new DiscordInteractionResponseBuilder()
            .AddEmbed(embed__).AddComponents(buttons);
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, confirmMessage);
        var message = await ctx.GetOriginalResponseAsync();
        var interaction = await interactivity.WaitForButtonAsync(message, ctx.User, TimeSpan.FromSeconds(60));
        buttons.ForEach(x => x.Disable());
        if (interaction.TimedOut)
        {
            var embed_ = new DiscordMessageBuilder()
                .WithEmbed(confirmEmbedBuilder.WithTitle("Ban abgebrochen")
                    .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
                    .WithDescription(
                        "Der Ban wurde abgebrochen.\n\nGrund: Zeitüberschreitung. <:counting_warning:962007085426556989>")
                    .WithColor(DiscordColor.Red).Build());
            await message.ModifyAsync(embed_);
            return;
        }

        if (interaction.Result.Id == $"ban_deny_{caseid}")
        {
            await interaction.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
            var embed_ = new DiscordMessageBuilder()
                .WithEmbed(confirmEmbedBuilder.WithTitle("Ban abgebrochen")
                    .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
                    .WithDescription(
                        "Der Ban wurde abgebrochen.\n\nGrund: Abgebrochen. <:counting_warning:962007085426556989>")
                    .WithColor(DiscordColor.Red).Build());
            await message.ModifyAsync(embed_);
            return;
        }

        if (interaction.Result.Id == $"ban_accept_{caseid}")
        {
            await interaction.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
            var loadingEmbedBuilder = new DiscordEmbedBuilder()
                .WithTitle("Ban wird bearbeitet")
                .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
                .WithDescription("Der Ban wird bearbeitet. Bitte warten...")
                .WithColor(DiscordColor.Yellow);
            var loadingEmbed = loadingEmbedBuilder.Build();
            var loadingMessage = new DiscordMessageBuilder()
                .WithEmbed(loadingEmbed).AddComponents(buttons);
            await message.ModifyAsync(loadingMessage);

            var b_users = "";
            var n_users = "";
            string e_string;
            var ec = DiscordColor.Red;
            DiscordMessage? umsg = null;
            try
            {
                umsg = await user.SendMessageAsync(embed);
                sent = true;
            }
            catch
            {
                sent = false;
            }

            var semoji = sent ? "<:yes:861266772665040917>" : "<:no:861266772724023296>";
            try
            {
                await ctx.Guild.BanMemberAsync(user.Id, 0, ReasonString);
                var dm = sent ? "✅" : "❌";
                b_users += $"{user.UsernameWithDiscriminator} | DM: {dm}\n";
            }
            catch (UnauthorizedException)
            {
                n_users += $"{user.UsernameWithDiscriminator}\n";
            }

            if (n_users != "")
            {
                e_string = $"Der Ban war nicht erfolgreich.\n" +
                           $"__Grund:__ ```{reason}```\n";
                e_string += $"__Nicht gebannte User:__\n" +
                            $"```{n_users}```";
                ec = DiscordColor.Red;
                if (sent)
                    try
                    {
                        await umsg.DeleteAsync();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
            }
            else
            {
                e_string = $"Der Ban wurde erfolgreich abgeschlossen.\n" +
                           $"__Grund:__ ```{reason}```\n" +
                           $"__Gebannte User:__\n" +
                           $"```{b_users}```";
                ec = DiscordColor.Green;
            }

            var discordEmbedBuilder = new DiscordEmbedBuilder()
                .WithTitle("Ban abgeschlossen")
                .WithDescription(e_string)
                .WithFooter(ctx.User.UsernameWithDiscriminator, ctx.User.AvatarUrl)
                .WithColor(ec);
            var discordEmbed = discordEmbedBuilder.Build();
            await message.ModifyAsync(new DiscordMessageBuilder().WithEmbed(discordEmbed));
        }
    }
}