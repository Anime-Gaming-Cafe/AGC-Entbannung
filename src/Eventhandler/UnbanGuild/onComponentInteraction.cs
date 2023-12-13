#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using DisCatSharp.Exceptions;
using Microsoft.Extensions.Logging;
using Npgsql;

#endregion

namespace AGC_Entbannungssystem.Eventhandler.UnbanGuild;

[EventHandler]
public class onComponentInteraction : ApplicationCommandsModule
{
    [Event]
    public async Task ComponentInteractionCreated(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            string cid = e.Interaction.Data.CustomId;
            if (cid == "open_appealticketinfo")
            {
                DiscordGuild mainGuild = await client.GetGuildAsync(GlobalProperties.MainGuildId);
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AsEphemeral());
                string? banreason = "";
                bool isBanned = false;
                try
                {
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("Prüfe, ob du gebannt bist..."));
                    var be = await mainGuild.GetBanAsync(e.User.Id);
                    banreason = be.Reason ?? "Kein Grund angegeben.";
                    await Task.Delay(1000);
                    isBanned = true;
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("Du bist gebannt! Setze fort..."));
                }
                catch (NotFoundException)
                {
                    // ignored
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("Du bist nicht gebannt! Breche ab..."));
                    isBanned = false;
                    await Task.Delay(500);
                }
                catch (Exception exception)
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Fehler!");
                    embed.WithDescription(
                        "Es ist ein Fehler aufgetreten. Bitte versuche es später erneut. Der Fehler wurde automatisch an den Entwickler weitergeleitet.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                    await ErrorReporting.SendErrorToDev(client, e.User, exception);
                }

                var cons = Helperfunctions.DbString();
                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().WithContent("Prüfe, ob du für Anträge gesperrt bist..."));
                await Task.Delay(500);
                await using var con = new NpgsqlConnection(cons);
                await con.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT * FROM antragssperre WHERE user_id = @userid", con);
                cmd.Parameters.AddWithValue("userid", (long)e.User.Id);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (reader.HasRows)
                {
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("Du bist für Anträge gesperrt!"));
                    await Task.Delay(1000);
                    var embed = new DiscordEmbedBuilder();
                    embed.WithDescription(
                        "Du bist aktuell für Anträge gesperrt. Du kannst keinen Entbannungsantrag stellen.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                    return;
                }

                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().WithContent("Du bist nicht für Anträge gesperrt! Setze fort..."));

                if (e.User.Id == GlobalProperties.BotOwnerId)
                {
                    // application test
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("Du bist der Botowner! Setze fort... (Test)"));
                    isBanned = true;
                }

                if (!isBanned)
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Nicht gebannt!");
                    embed.WithDescription(
                        "Wie es scheint, bist du nicht auf AGC gebannt. Diese Überprüfung ist automatisiert. Du kannst also keinen Entbannungsantrag stellen.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                    return;
                }
                
                if (banreason.ToLower().Contains("bannsystem | report-id:"))
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Bannsystem");
                    embed.WithDescription(
                        "Du wurdest vom globalen Bannsystem gebannt. Du kannst hier keinen Entbannungsantrag stellen. \n\n" +
                        "Bitte wende dich an [Bannsystem Support](https://bannsystem.de) um deinen Bann zu klären. Dein Bann betrifft nicht nur AGC, sondern alle Server, die das Bannsystem nutzen.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                    try
                    {
                        ulong logChannelId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "LogChannelId"));
                        var logChannel = await client.GetChannelAsync(logChannelId);
                        await logChannel.SendMessageAsync(
                            $"{e.User.Mention} ({e.User.Id}) hat die Antragshinweise **geöffnet** (BANNSYSTEM GEBANNT) - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)}");
                    }
                    catch (Exception exception)
                    {
                        await ErrorReporting.SendErrorToDev(client, e.User, exception);
                        client.Logger.LogError($"Exception occured: {exception.GetType()}: {exception.Message}");
                    }
                    return;
                }
                
                

                var rb = new DiscordWebhookBuilder();
                var button = new DiscordButtonComponent(ButtonStyle.Success, "open_appealticket_confirm",
                    "Ich habe alles gelesen und verstanden!",
                    emoji: new DiscordComponentEmoji("✅"));
                rb.AddComponents(button);
                rb.AddEmbeds(MessageGenerator.UnbanNoteGenerate());
                await e.Interaction.EditOriginalResponseAsync(rb);
                try
                {
                    ulong logChannelId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "LogChannelId"));
                    var logChannel = await client.GetChannelAsync(logChannelId);
                    await logChannel.SendMessageAsync(
                        $"{e.User.Mention} ({e.User.Id}) hat die Antragshinweise **geöffnet** - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)}");
                }
                catch (Exception exception)
                {
                    await ErrorReporting.SendErrorToDev(client, e.User, exception);
                    client.Logger.LogError($"Exception occured: {exception.GetType()}: {exception.Message}");
                }
            }
            else if (cid == "open_appealticket_confirm")
            {
                ulong logChannelId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "LogChannelId"));
                var logChannel = await client.GetChannelAsync(logChannelId);
                await logChannel.SendMessageAsync(
                    $"{e.User.Mention} ({e.User.Id}) hat die Antragshinweise **akzeptiert** - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)}");
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AsEphemeral());
                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().WithContent("Ticket wird erstellt..."));
                var appealrole = e.Guild.GetRole(ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AppealRoleId")));
                await Task.Delay(1000);
                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().WithContent("Prüfe auf offenes Ticket..."));
                DiscordMember member = await e.Guild.GetMemberAsync(e.User.Id);
                if (member.Roles.Contains(appealrole))
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Fehler!");
                    embed.WithDescription(
                        "Du hast bereits ein offenes Ticket. Bitte nutze dieses, um einen Entbannungsantrag zu stellen.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                    return;
                }

                await Task.Delay(1000);
                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().WithContent("Erstelle Ticket..."));
                await logChannel.SendMessageAsync(
                    $"$new {e.User.Id}");
                await Task.Delay(500);
                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().WithContent("Ticket erstellt!"));
            }
            else if (cid == "open_sperrinfo")
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AsEphemeral());

                ulong logChannelId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "LogChannelId"));
                var logChannel = await client.GetChannelAsync(logChannelId);
                await logChannel.SendMessageAsync(
                    $"{e.User.Mention} ({e.User.Id}) hat die Sperrzeit **abgefragt** - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)}");

                var cons = Helperfunctions.DbString();
                try
                {
                    await using var con = new NpgsqlConnection(cons);
                    await con.OpenAsync();

                    await using var cmd = new NpgsqlCommand("SELECT * FROM antragssperre WHERE user_id = @userid", con);
                    cmd.Parameters.AddWithValue("userid", (long)e.User.Id);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        var expiresAt = reader.GetInt64(1);
                        string sperrstring = "Du bist für einen Antrag gesperrt. Deine Sperre läuft bis <t:" +
                                             expiresAt + ":f> - ( <t:" + expiresAt + ":R> )";
                        await e.Interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent(sperrstring));
                    }
                    else
                    {
                        await e.Interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent("Du bist nicht gesperrt!"));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                    await ErrorReporting.SendErrorToDev(client, e.User, ex);
                }
            }


            await Task.CompletedTask;
        });
    }
}