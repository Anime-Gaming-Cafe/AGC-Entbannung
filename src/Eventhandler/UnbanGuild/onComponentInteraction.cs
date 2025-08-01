#region

using AGC_Entbannungssystem.Entities;
using AGC_Entbannungssystem.Entities.Database;
using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using AGC_Entbannungssystem.Tasks;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using DisCatSharp.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

#endregion

namespace AGC_Entbannungssystem.Eventhandler.UnbanGuild;

[EventHandler]
public class onComponentInteraction : ApplicationCommandsModule
{
    private readonly Dictionary<ulong, long> timeMessuarements = new();

    [Event]
    public async Task ComponentInteractionCreated(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            string cid = e.Interaction.Data.CustomId;

            # region votebuttons

            if (cid.StartsWith("vote_"))
            {
                try
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().AsEphemeral());
                    ulong channelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));
                    DiscordChannel channel = await client.GetChannelAsync(channelid);
                    DiscordMessage message = await channel.GetMessageAsync(e.Message.Id);
                    if (message == null)
                    {
                        await e.Interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent("Die Nachricht wurde nicht gefunden."));
                        return;
                    }

                    var existingVote = await Helperfunctions.UserHasVoted(e); // returns Positive, Negative or null

                    if (cid.StartsWith("vote_yes_"))
                    {
                        if (existingVote != null)
                        {
                            await Helperfunctions.removeVoteFromAntrag(e);
                            await e.Interaction.EditOriginalResponseAsync(
                                new DiscordWebhookBuilder().WithContent("Dein vorheriger Vote wurde entfernt."));
                            await UpdateVoteMessages.UpdateSingleVoteMessage(CurrentApplicationData.Client,
                                e.Message.Id);
                            return;
                        }

                        // add vote
                        await Helperfunctions.addVoteToAntrag(e, true);
                        await e.Interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent($"Dein Vote wurde gezählt! Stimme: **Ja**"));
                        await UpdateVoteMessages.UpdateSingleVoteMessage(CurrentApplicationData.Client, e.Message.Id);
                    }
                    else if (cid.StartsWith("vote_no_"))
                    {
                        if (existingVote != null)
                        {
                            await Helperfunctions.removeVoteFromAntrag(e);
                            await e.Interaction.EditOriginalResponseAsync(
                                new DiscordWebhookBuilder().WithContent("Dein vorheriger Vote wurde entfernt."));
                            await UpdateVoteMessages.UpdateSingleVoteMessage(CurrentApplicationData.Client,
                                e.Message.Id);
                            return;
                        }

                        await Helperfunctions.addVoteToAntrag(e, false);
                        await e.Interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent($"Dein Vote wurde gezählt! Stimme: **Nein**"));
                        await UpdateVoteMessages.UpdateSingleVoteMessage(CurrentApplicationData.Client, e.Message.Id);
                    }
                    else
                    {
                        await e.Interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent("Unbekannter Button!"));
                    }
                }
                catch (Exception exception)
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Fehler!");
                    embed.WithDescription(
                        "Es ist ein Fehler aufgetreten. Bitte versuche es später erneut. Der Fehler wurde automatisch an den Entwickler weitergeleitet.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                    client.Logger.LogError($"Exception occured: {exception.GetType()}: {exception.Message}");
                    // print line number
                    client.Logger.LogError($"Line: {exception.StackTrace?.Split('\n')[0]}");
                    // log stack trace
                    client.Logger.LogError(exception.StackTrace);
                    await ErrorReporting.SendErrorToDev(client, e.User, exception);
                }

                return;
            }

            # endregion

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

                // bs check start
                var bsreportlist = new List<BannSystemReport>();
                bool bs_status = false;
                if (GlobalProperties.isBannSystemEnabled)
                {
                    try
                    {
                        bsreportlist = await Helperfunctions.BSReportToWarn(e.User);
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        bs_status = Helperfunctions.HasActiveBannSystemReport(bsreportlist);
                    }
                    catch (Exception)
                    {
                    }
                }


                // bs check end
                if (bs_status)
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Bannsystem");
                    embed.WithDescription(
                        "Du wurdest vom globalen Bannsystem gebannt. Du kannst hier keinen Entbannungsantrag stellen. \n\n" +
                        "Bitte wende dich an [Bannsystem Support](https://bannsystem.de) um deinen Bann zu klären. Dein Bann betrifft nicht nur AGC, sondern alle Server, die das Bannsystem nutzen. Nachdem dein Bann aufgehoben wurde, kannst du - wenn nicht Entbannt - einen Entbannungsantrag stellen.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                    try
                    {
                        ulong logChannelId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "LogChannelId"));
                        var logChannel = await client.GetChannelAsync(logChannelId);
                        await logChannel.SendMessageAsync(
                            $"{e.User.Mention} ({e.User.Id}) hat die Antragshinweise **geöffnet** (BANNSYSTEM GEBANNT | DB Check) - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)}");
                    }
                    catch (Exception exception)
                    {
                        await ErrorReporting.SendErrorToDev(client, e.User, exception);
                        client.Logger.LogError($"Exception occured: {exception.GetType()}: {exception.Message}");
                    }

                    return;
                }

                if (await GetPermaBlock(e.User.Id))
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Permanent ausgeschlossen");
                    embed.WithDescription(
                        "Du wurdest vom Entbannungssystem permanent ausgeschlossen. Du kannst keinen Entbannungsantrag stellen. Das heißt du bist für immer von der Entbannung ausgeschlossen.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                    try
                    {
                        ulong logChannelId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "LogChannelId"));
                        var logChannel = await client.GetChannelAsync(logChannelId);
                        await logChannel.SendMessageAsync(
                            $"{e.User.Mention} ({e.User.Id}) hat die Antragshinweise **geöffnet** (PERMANENT AUSGESCHLOSSEN) - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)}");
                    }
                    catch (Exception exception)
                    {
                        await ErrorReporting.SendErrorToDev(client, e.User, exception);
                        client.Logger.LogError($"Exception occured: {exception.GetType()}: {exception.Message}");
                    }

                    return;
                }


                var cons = Helperfunctions.DbString();
                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().WithContent("Prüfe, ob du für Anträge gesperrt bist..."));
                await Task.Delay(500);

                await using var context = AgcDbContextFactory.CreateDbContext();
                var sperre = await context.Antragssperren
                    .FirstOrDefaultAsync(s => s.UserId == (long)e.User.Id);

                if (sperre != null)
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
                            $"{e.User.Mention} ({e.User.Id}) hat die Antragshinweise **geöffnet** (BANNSYSTEM GEBANNT | Reason String check) - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)}");
                    }
                    catch (Exception exception)
                    {
                        await ErrorReporting.SendErrorToDev(client, e.User, exception);
                        client.Logger.LogError($"Exception occured: {exception.GetType()}: {exception.Message}");
                    }

                    return;
                }

                if (timeMessuarements.ContainsKey(e.User.Id))
                {
                    timeMessuarements[e.User.Id] = DateTimeOffset.Now.ToUnixTimeSeconds();
                }
                else
                {
                    timeMessuarements.Add(e.User.Id, DateTimeOffset.Now.ToUnixTimeSeconds());
                }

                var openticketrole =
                    e.Guild.GetRole(ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AppealRoleId")));
                var member = await e.Guild.GetMemberAsync(e.User.Id, true);

                var rb = new DiscordWebhookBuilder();
                var button = new DiscordButtonComponent(ButtonStyle.Success, "open_appealticket_confirm",
                    "Ich habe alles gelesen und verstanden!",
                    emoji: new DiscordComponentEmoji("✅"));
                if (!member.Roles.Contains(openticketrole))
                {
                    rb.AddComponents(button);
                }

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
                string tookseconds = "";
                long timediff = 0;
                if (timeMessuarements.ContainsKey(e.User.Id))
                {
                    long start = timeMessuarements[e.User.Id];
                    long end = DateTimeOffset.Now.ToUnixTimeSeconds();
                    long diff = end - start;
                    tookseconds = $"{diff}";
                    timediff = diff;

                    if (diff < 15)
                    {
                        tookseconds = "⚠️ " + tookseconds + " Sekunden";
                    }
                    else
                    {
                        tookseconds = tookseconds + " Sekunden";
                    }
                }
                else
                {
                    tookseconds = "N/A";
                }


                ulong logChannelId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "LogChannelId"));
                var logChannel = await client.GetChannelAsync(logChannelId);


                var member_ = await e.Guild.GetMemberAsync(e.User.Id);
                var appealrole_ = e.Guild.GetRole(ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AppealRoleId")));


                if (timediff < 15 && !member_.Roles.Contains(appealrole_))
                {
                    var embed = new DiscordEmbedBuilder();
                    embed.WithTitle("Antrag abgelehnt!");
                    embed.WithDescription(
                        "Da du die Antragshinweise nicht gelesen hast und einfach nur auf den Button geklickt hast, wurde dein Antrag automatisch abgelehnt. Du kannst es in 3 Monaten erneut versuchen.");
                    embed.WithColor(DiscordColor.Red);
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                        new DiscordInteractionResponseBuilder().AddEmbed(embed));
                    await logChannel.SendMessageAsync(
                        $"{e.User.Mention} ({e.User.Id}) hat die Antragshinweise **nicht gelesen** | Automatische ablehnung erfolgt - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)} | Zeit benötigt: {tookseconds}");
                    await Sperre(e.User, "Hinweise nicht gelesen", e);
                    await AblehnungEintragen(e.User, "Hinweise nicht gelesen", e);
                    return;
                }


                await logChannel.SendMessageAsync(
                    $"{e.User.Mention} ({e.User.Id}) hat die Antragshinweise **akzeptiert** - {DateTime.Now.Timestamp(TimestampFormat.ShortDateTime)} | Zeit benötigt: {tookseconds}");
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder().WithContent("Hinweise wurden akzeptiert. Fahre fort..."));
                await Task.Delay(2000);
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

                await using var context2 = AgcDbContextFactory.CreateDbContext();
                
                var sperrInfo = await context2.Antragssperren
                    .FirstOrDefaultAsync(s => s.UserId == (long)e.User.Id);

                try
                {
                    if (sperrInfo != null)
                    {
                        var expiresAt = sperrInfo.ExpiresAt;
                        var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var secondsLeft = expiresAt - unixNow;
                        var humanTime = TimeSpan.FromSeconds(secondsLeft);

                        string sperrstring = "Du bist für einen Antrag gesperrt. Deine Sperre läuft bis <t:" +
                                             expiresAt + ":f> - ( <t:" + expiresAt + ":R> )";
                        await e.Interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent(sperrstring));
                        await logChannel.SendMessageAsync(
                            $"🕒 **Sperrzeit verbleibend für {e.User.Username} ({e.User.Id})**: {humanTime.Days} Tage, {humanTime.Hours} Stunden, {humanTime.Minutes} Minuten ({secondsLeft} Sekunden)");
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

    private static async Task<bool> GetPermaBlock(ulong userid)
    {
        await using var context = AgcDbContextFactory.CreateDbContext();
        return await context.PermaBlocks
            .AnyAsync(p => p.UserId == (long)userid);
    }


    private static async Task Sperre(DiscordUser user, string reason, ComponentInteractionCreateEventArgs e)
    {
        var timestamp = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds();
        
        await using var context = AgcDbContextFactory.CreateDbContext();
        
        var sperre = new Antragssperre
        {
            UserId = (long)user.Id,
            Reason = reason,
            ExpiresAt = timestamp
        };

        context.Antragssperren.Add(sperre);
        await context.SaveChangesAsync();

        try
        {
            // try to give role
            ulong roleid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreRoleId"));
            DiscordRole role = e.Guild.GetRole(roleid);
            DiscordMember member = await e.Guild.GetMemberAsync(user.Id);
            await member.GrantRoleAsync(role, "Antragssperre");
        }
        catch (Exception exception)
        {
            // ignored
        }

        DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
        embed.WithDescription(
            $"{DateTime.UtcNow.Timestamp(TimestampFormat.LongDateTime)} - {user.Mention} ({user.Id}) - Antrag -/- (Autosperre) - ``{reason}`` -> Gesperrt bis: <t:{timestamp}:f> ( <t:{timestamp}:R> )");
        embed.WithFooter(
            $"Gesperrt durch {CurrentApplicationData.Client.CurrentUser.UsernameWithDiscriminator} ({CurrentApplicationData.Client.CurrentUser.Id})",
            CurrentApplicationData.Client.CurrentUser.AvatarUrl);
        ulong infochannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreInfoChannelId"));
        DiscordChannel ichan = e.Guild.GetChannel(infochannelid);
        await ichan.SendMessageAsync(embed);
    }

    private static async Task AblehnungEintragen(DiscordUser user, string reason, ComponentInteractionCreateEventArgs e)
    {
        await using var context = AgcDbContextFactory.CreateDbContext();
        
        var antragsverlauf = new Antragsverlauf
        {
            AntragsId = "STUB0000",
            UserId = (long)user.Id,
            ModId = (long)CurrentApplicationData.Client.CurrentUser.Id,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Entbannt = false,
            Reason = reason
        };

        context.Antragsverlauf.Add(antragsverlauf);
        await context.SaveChangesAsync();

        var eb = new DiscordEmbedBuilder();
        eb.WithTitle("Antrag wurde abgelehnt!");
        eb.WithColor(DiscordColor.Red);
        eb.WithDescription(
            $"**Status:** {Helperfunctions.BoolToEmoji(false)}\n**Bearbeitet von:** {CurrentApplicationData.Client.CurrentUser.Mention} ({CurrentApplicationData.Client.CurrentUser.Id}) \n**Antragsnummer:** -/-\n**Betroffener User:** {user.Mention} ({user.Id})\n**Grund:** {reason}");
        eb.WithFooter("Entbannungssystem", CurrentApplicationData.Client.CurrentUser.AvatarUrl);
        eb.WithTimestamp(DateTimeOffset.Now);
        var chid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "HistoryChannelId"));
        var ch = e.Guild.GetChannel(chid);
        await ch.SendMessageAsync(eb);
    }
}