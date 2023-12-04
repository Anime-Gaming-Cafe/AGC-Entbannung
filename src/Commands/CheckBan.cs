#region

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

public sealed class CheckBan : ApplicationCommandsModule
{
    [ApplicationRequireStaffRole]
    [SlashCommand("checkban", "Überprüft, ob und warum ein User auf AGC gebannt ist.")]
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

        var embed = new DiscordEmbedBuilder();
        embed.WithAuthor(user.Username, iconUrl: user.AvatarUrl);
        embed.WithThumbnail(user.AvatarUrl);
        embed.AddField(new DiscordEmbedField("User-ID", user.Id.ToString(), true));

        if (!isBanned)
        {
            embed.WithTitle("Der User ist nicht gebannt.");
            embed.WithDescription("**Nutzer auf AGC:** " + (mainGuild.Members.ContainsKey(user.Id)
                ? "Ja, seit " + mainGuild.Members[user.Id].JoinedAt.Timestamp()
                : "Nein"));
            embed.WithColor(DiscordColor.Green);
        }
        else
        {
            embed.WithTitle("Der User ist gebannt.");
            embed.WithDescription($"Grund: ```{reason}```");
            embed.WithColor(DiscordColor.Red);
        }

        embed.WithTimestamp(DateTimeOffset.Now);
        embed.WithFooter("Bericht angefordert von " + ctx.User.Username, ctx.User.AvatarUrl);

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(embed));
    }
}