#region

using System.Text.RegularExpressions;
using AGC_Entbannungssystem.Entities.Database;
using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

#endregion

namespace AGC_Entbannungssystem.Tasks;

public static class CheckInactiveTickets
{
    private static readonly Regex AntragNamePattern = new(@"^antrag-\d{4}$", RegexOptions.Compiled);
    private static readonly TimeSpan InactivityThreshold = TimeSpan.FromHours(24);

    public static async Task Run(DiscordClient client)
    {
        await Task.Delay(TimeSpan.FromMinutes(3));

        while (true)
        {
            try
            {
                await RunIteration(client);
            }
            catch (Exception err)
            {
                client.Logger.LogError(err, "Error in CheckInactiveTickets outer loop");
                try { await ErrorReporting.SendErrorToDev(client, client.CurrentUser, err); } catch { }
            }

            await Task.Delay(TimeSpan.FromMinutes(15));
        }
    }

    private static async Task RunIteration(DiscordClient client)
    {
        var guild = await client.GetGuildAsync(GlobalProperties.UnbanServerId);
        if (guild == null) return;

        ulong voteCategoryId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "VoteCategoryChannelId"));
        ulong bearbeitetCatId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "BearbeitetCategoryId"));
        ulong sperreInfoChannelId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreInfoChannelId"));
        ulong sperreRoleId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "SperreRoleId"));

        var antragChannels = guild.Channels.Values
            .Where(c => AntragNamePattern.IsMatch(c.Name))
            .ToList();

        var now = DateTimeOffset.UtcNow;

        foreach (var channel in antragChannels)
        {
            try
            {
                if (channel.ParentId == voteCategoryId) continue;

                var channelIdLong = (long)channel.Id;
                await using (var context = AgcDbContextFactory.CreateDbContext())
                {
                    if (await context.DisabledAutomations.AnyAsync(d => d.ChannelId == channelIdLong))
                        continue;
                }

                var messages = await channel.GetMessagesAsync(1);
                var lastMsg = messages?.FirstOrDefault();
                if (lastMsg == null) continue;

                bool authorIsTeamOrBot = lastMsg.Author.IsBot;
                if (!authorIsTeamOrBot)
                {
                    try
                    {
                        var lastAuthorMember = await guild.GetMemberAsync(lastMsg.Author.Id);
                        authorIsTeamOrBot = lastAuthorMember.Roles.Any(r => r.Id == GlobalProperties.UnbanServerTeamRoleId);
                    }
                    catch
                    {
                        authorIsTeamOrBot = false;
                    }
                }
                if (!authorIsTeamOrBot) continue;

                if (now - lastMsg.CreationTimestamp < InactivityThreshold) continue;

                var antragsnummer = channel.Name.Split('-').Last();

                var userId = await Helperfunctions.GetTicketUserIdAsync(channel);
                if (userId == null)
                {
                    client.Logger.LogWarning("CheckInactiveTickets: konnte UserId nicht aus Channel {ChannelId} parsen", channel.Id);
                    continue;
                }

                var userIdLong = (long)userId.Value;

                await using (var context = AgcDbContextFactory.CreateDbContext())
                {
                    if (await context.Antragssperren.AnyAsync(a => a.UserId == userIdLong))
                        continue;
                    if (await context.Antragsverlauf.AnyAsync(a => a.UserId == userIdLong && a.AntragsId == antragsnummer))
                        continue;
                }

                await TriggerAutoDeny24(client, guild, channel, userId.Value, antragsnummer,
                    bearbeitetCatId, sperreInfoChannelId, sperreRoleId);
            }
            catch (Exception ex)
            {
                client.Logger.LogError(ex, "Error processing channel {ChannelId} in CheckInactiveTickets", channel.Id);
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    private static async Task TriggerAutoDeny24(
        DiscordClient client,
        DiscordGuild guild,
        DiscordChannel channel,
        ulong userId,
        string antragsnummer,
        ulong bearbeitetCatId,
        ulong sperreInfoChannelId,
        ulong sperreRoleId)
    {
        const string reasonText = "Keine Response nach 24h";
        var expiresAt = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds();
        var userIdLong = (long)userId;

        await using (var context = AgcDbContextFactory.CreateDbContext())
        {
            var sperre = new Antragssperre
            {
                UserId = userIdLong,
                Reason = reasonText,
                ExpiresAt = expiresAt
            };
            context.Antragssperren.Add(sperre);
            await context.SaveChangesAsync();
        }

        try
        {
            var member = await guild.GetMemberAsync(userId);
            var sperreRole = guild.GetRole(sperreRoleId);
            await member.GrantRoleAsync(sperreRole, "Auto-Antragssperre (24h Inaktivität)");
        }
        catch (Exception ex)
        {
            client.Logger.LogWarning(ex, "Konnte Sperre-Rolle nicht setzen für UserId {UserId} (vermutlich nicht in Guild)", userId);
        }

        try
        {
            await channel.SendMessageAsync("deny24");
        }
        catch (Exception ex)
        {
            client.Logger.LogWarning(ex, "Konnte deny24 nicht in Channel {ChannelId} senden", channel.Id);
        }

        try
        {
            var targetUser = await client.GetUserAsync(userId);
            await Helperfunctions.TryAddAntragsverlaufAsync(
                client,
                entbannt: false,
                modUser: client.CurrentUser,
                antragsnummer: antragsnummer,
                targetUser: targetUser,
                grund: reasonText);
        }
        catch (Exception ex)
        {
            client.Logger.LogWarning(ex, "Auto-Antragsverlauf-Eintrag fehlgeschlagen für UserId {UserId}", userId);
        }

        try
        {
            var sperreInfoChannel = guild.GetChannel(sperreInfoChannelId);
            var embed = new DiscordEmbedBuilder()
                .WithDescription(
                    $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F> - <@{userId}> ({userId}) - Antrag {antragsnummer} - ``{reasonText}`` -> Gesperrt bis: <t:{expiresAt}:f> ( <t:{expiresAt}:R> )")
                .WithFooter("Automatisch durch 24h-Inaktivitäts-Watcher", client.CurrentUser.AvatarUrl)
                .WithColor(DiscordColor.Orange);
            await sperreInfoChannel.SendMessageAsync(embed);
        }
        catch (Exception ex)
        {
            client.Logger.LogWarning(ex, "Konnte Log-Embed nicht in SperreInfoChannel posten");
        }

        try
        {
            var bearbeitetCat = guild.GetChannel(bearbeitetCatId);
            await channel.ModifyAsync(x => x.Parent = bearbeitetCat);
        }
        catch (Exception ex)
        {
            client.Logger.LogWarning(ex, "Konnte Channel {ChannelId} nicht in Bearbeitet-Kategorie moven", channel.Id);
        }

        client.Logger.LogInformation("CheckInactiveTickets: Auto-deny24 ausgeführt für User {UserId} in Channel {ChannelId}", userId, channel.Id);
    }
}
