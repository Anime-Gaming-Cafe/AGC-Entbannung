using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;

namespace AGC_Entbannungssystem.Helpers;

public sealed class ApplicationRequireStaffRole : ApplicationCommandCheckBaseAttribute
{
    public override async Task<bool> ExecuteChecksAsync(BaseContext ctx)
    {
        var member = ctx.Member;
        var role = member.Roles.FirstOrDefault(x => x.Id == GlobalProperties.UnbanServerTeamRoleId);
        if (role is null)
        {
            var embed = new DiscordEmbedBuilder();
            embed.WithTitle("Du hast keine Berechtigung für diesen Command.");
            embed.WithDescription("Du musst das Staff-Rolle haben, um diesen Command auszuführen.");
            embed.WithColor(DiscordColor.Red);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed));
            return false;
        }

        return true;
    }
}