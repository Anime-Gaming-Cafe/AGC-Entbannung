#region

using AGC_Entbannungssystem.Entities;
using AGC_Entbannungssystem.Helpers;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.Exceptions;

#endregion

namespace AGC_Entbannungssystem.Commands;

public sealed class CheckUser : ApplicationCommandsModule
{
[ApplicationRequireStaffRole]
[SlashCommand("checkuser", "Überprüft den Nutzer.")]
public static async Task CheckBanCommand(InteractionContext ctx,
    [Option("user", "Der User, der überprüft werden soll.")]
    DiscordUser user)
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

    string reason = banentry?.Reason ?? "Kein Grund angegeben.";

    var bsreportlist = new List<BannSystemReport>();
    bool bs_status = false;
    if (GlobalProperties.isBannSystemEnabled)
    {
        try
        {
            bsreportlist = await Helperfunctions.BSReportToWarn(user);
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

    var antragshistorie = await Helperfunctions.GetAntragshistorie(user);
    Console.WriteLine(antragshistorie.Count);
    string description = "";

    if (antragshistorie.Count == 0)
    {
        description = "-- Keine Anträge gefunden --";
    }
    else
    {
        foreach (var antrag in antragshistorie)
        {
            description += $"{BoolToString(antrag.unbanned)} - ``{antrag.antragsnummer}`` - ``{antrag.grund}`` - {await ctx.Client.GetUserAsync((ulong)antrag.mod_id)} - <t:{antrag.timestamp}:f> (<t:{antrag.timestamp}:R>) \n";
        }
    }

    var embed = new DiscordEmbedBuilder();
    embed.WithAuthor(user.Username, iconUrl: user.AvatarUrl);
    embed.WithThumbnail(user.AvatarUrl);
    embed.AddField(new DiscordEmbedField("User-ID", user.Id.ToString(), true));

    if (!isBanned)
    {
        embed.WithTitle("Der User ist nicht gebannt.");
        embed.WithDescription("**Nutzer auf AGC:** " + (mainGuild.Members.ContainsKey(user.Id)
            ? "Ja, seit " + mainGuild.Members[user.Id].JoinedAt.Timestamp()
            : "Nein") + "\n**Antragshistorie:** \n" + description);
        embed.WithColor(DiscordColor.Green);
    }
    else
    {
        embed.WithTitle("Der User ist gebannt.");
        embed.WithDescription($"Grund: ```{reason}``` \n**Antragshistorie:** \n" + description);
        embed.WithColor(DiscordColor.Red);
    }

    if (bs_status)
    {
        embed.AddField(new DiscordEmbedField("Bannsystem",
            "Der User hat aktive Bannsystem-Banns. Bitte prüfen! -> <#1181260689474605147> / <#948314117192679434>"));
    }

    embed.WithTimestamp(DateTimeOffset.Now);
    embed.WithFooter("Bericht angefordert von " + ctx.User.Username, ctx.User.AvatarUrl);

    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
        new DiscordInteractionResponseBuilder().AddEmbed(embed));
}


    
    private static string BoolToString(bool value)
    {
        return value ? "<:angenommen:1190335045341282314>" : "<:abgelehnt:1190335046591205426>";
    }
    
    
}