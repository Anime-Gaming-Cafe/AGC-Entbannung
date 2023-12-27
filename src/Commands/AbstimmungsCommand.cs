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

public sealed class AbstimmungsCommand : ApplicationCommandsModule
{
    [ApplicationRequireStaffRole]
    [SlashCommand("abstimmung", "Erstellt eine Entbannungsabstimmung.")]
    public static async Task AbstimmungCommand(InteractionContext ctx)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());
        var constring = Helperfunctions.DbString();
        await using var con = new NpgsqlConnection(constring);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT * FROM abstimmungen WHERE channel_id = @channelid", con);
        cmd.Parameters.AddWithValue("channelid", (long)ctx.Channel.Id);
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Prüfe, ob bereits eine Abstimmung läuft..."));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (reader.HasRows)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("Es läuft bereits eine Abstimmung in diesem Channel!"));
            return;
        }

        await con.CloseAsync();

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Keine Abstimmung gefunden. Erstelle eine neue Abstimmung..."));
        if (!ctx.Channel.Name.StartsWith("antrag-"))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Dieser Channel ist kein Antrag!"));
            return;
        }

        if (ctx.Channel.Name.Contains("-geschlossen"))
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("Dieser Channel ist kein __offener__ Antrag!"));
            return;
        }

        var embed = new DiscordEmbedBuilder();
        embed.WithTitle("Entbannungsabstimmung");
        embed.Timestamp = DateTimeOffset.UtcNow;
        embed.WithDescription($"{ctx.Channel.Name} | ({ctx.Channel.Mention}) steht zur Abstimmung bereit.");
        ulong votechannelid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "AbstimmungsChannelId"));
        DiscordChannel votechannel = ctx.Guild.GetChannel(votechannelid);
        var votechannelmessage =
            await votechannel.SendMessageAsync($"<@&{BotConfigurator.GetConfig("MainConfig", "PingRoleId")}>",
                embed);

        //move channel to vote category
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent("Verschiebe Channel in die Abstimmungskategorie..."));
        ulong votecategoryid = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "VoteCategoryChannelId"));
        var votecategory = await CurrentApplicationData.Client.GetChannelAsync(votecategoryid);
        await ctx.Channel.ModifyAsync(x => x.Parent = votecategory);
        await Task.Delay(200);
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Verschoben!"));
        var notifyembed = new DiscordEmbedBuilder();
        notifyembed.WithTitle("Status Update");
        notifyembed.WithDescription($"Lieber User, \n" +
                                    $"wir besprechen deinen Antrag nun intern. Du erhältst eine Rückmeldung, sobald die Entscheidung feststeht! \nDies dauert in der Regel 12 Stunden.");
        notifyembed.WithColor(DiscordColor.Green);
        notifyembed.WithFooter("AGC Entbannungssystem");
        await ctx.Channel.SendMessageAsync(notifyembed);
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Abstimmung erstellt!"));
        // daumen hoch und runter
        await votechannelmessage.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":thumbsup:"));
        await votechannelmessage.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":thumbsdown:"));
        // unix timestamp now in 24h
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now12h = now + 43200;
        var constring2 = Helperfunctions.DbString();
        await using var con2 = new NpgsqlConnection(constring2);
        await con2.OpenAsync();
        await using var cmd2 =
            new NpgsqlCommand(
                "INSERT INTO abstimmungen (channel_id, message_id, expires_at) VALUES (@channelid, @messageid, @endtime)",
                con2);
        cmd2.Parameters.AddWithValue("channelid", (long)ctx.Channel.Id);
        cmd2.Parameters.AddWithValue("messageid", (long)votechannelmessage.Id);
        cmd2.Parameters.AddWithValue("endtime", now12h);
        await cmd2.ExecuteNonQueryAsync();
        await con2.CloseAsync();
    }
}