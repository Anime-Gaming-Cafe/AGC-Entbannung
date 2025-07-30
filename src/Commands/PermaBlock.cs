using AGC_Entbannungssystem.Services;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Microsoft.EntityFrameworkCore;

namespace AGC_Entbannungssystem.Commands;

public sealed class PermaBlockUserCommand : ApplicationCommandsModule
{
    [ApplicationCommandRequirePermissions(Permissions.Administrator)]
    [SlashCommand("permablock", "Blockt einen User vom Entbannungssystem")]
    public async Task BlockMember(InteractionContext ctx,
        [Option("user", "Der User, der geblockt werden soll.")]
        DiscordUser user)
    {
        var blocked = await BlockUserIfNotBlocked(user);
        if (blocked)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent(
                    $"User {user.Username} wurde erfolgreich geblockt."));
        }
        else
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent($"User {user.Username} ist bereits geblockt."));
        }
    }

    private static async Task<bool> BlockUserIfNotBlocked(DiscordUser user)
    {
        await using var context = AgcDbContextFactory.CreateDbContext();

        var existingBlock = await context.PermaBlocks
            .FirstOrDefaultAsync(p => p.UserId == (long)user.Id);

        if (existingBlock != null)
        {
            return false;
        }

        var permaBlock = new Entities.Database.PermaBlock
        {
            UserId = (long)user.Id
        };

        context.PermaBlocks.Add(permaBlock);
        await context.SaveChangesAsync();
        return true;
    }
}