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
        var idOfAntragChannel = ctx.Channel.Id.ToString();

        var votebuttons = new List<DiscordButtonComponent>()
        {
            new(ButtonStyle.Secondary, "vote_yes_" + idOfAntragChannel, "👍 Ja"),
            new(ButtonStyle.Secondary, "vote_no_" + idOfAntragChannel, "👎 Nein")
        };
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now16h = now + 57600;

        var voteembed = MessageGenerator.getVoteEmbedInRunning(ctx.Channel, now16h, 0, 0, 3);
        var votechannelmessage = new DiscordMessageBuilder().AddComponents(votebuttons).AddEmbed(voteembed).WithContent(Helperfunctions.getTeamPing());
        var votemessage = await votechannel.SendMessageAsync(
            votechannelmessage.AddEmbed(embed));

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
                                    $"wir besprechen deinen Antrag nun intern. Du erhältst eine Rückmeldung, sobald die Entscheidung feststeht! \nDies dauert in der Regel 16 Stunden.");
        notifyembed.WithColor(DiscordColor.Green);
        notifyembed.WithFooter("AGC Entbannungssystem");
        await ctx.Channel.SendMessageAsync(notifyembed);
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Abstimmung erstellt!"));
        var constring2 = Helperfunctions.DbString();
        await using var con2 = new NpgsqlConnection(constring2);
        await con2.OpenAsync();
        await using var cmd2 =
            new NpgsqlCommand(
                "INSERT INTO abstimmungen (channel_id, message_id, expires_at, created_by, pvotes, nvotes) VALUES (@channelid, @messageid, @endtime, @createdby, @pvotes, @nvotes)",
                con2);
        cmd2.Parameters.AddWithValue("channelid", (long)ctx.Channel.Id);
        cmd2.Parameters.AddWithValue("messageid", (long)votemessage.Id);
        cmd2.Parameters.AddWithValue("endtime", now16h);
        cmd2.Parameters.AddWithValue("createdby", (long)ctx.User.Id);
        cmd2.Parameters.AddWithValue("pvotes", 0);
        cmd2.Parameters.AddWithValue("nvotes", 0);
        await cmd2.ExecuteNonQueryAsync();
        await con2.CloseAsync();
    }
}