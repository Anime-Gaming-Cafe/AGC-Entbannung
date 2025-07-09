using AGC_Entbannungssystem.Helpers;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Npgsql;

namespace AGC_Entbannungssystem.Commands;

#region



#endregion

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
        var constring = Helperfunctions.DbString();
        await using var con = new NpgsqlConnection(constring);
        await con.OpenAsync();
        await using var cmd1 = new NpgsqlCommand("SELECT * FROM permas WHERE userid = @user_id", con);
        cmd1.Parameters.AddWithValue("user_id", (long)user.Id);
        await using var reader = await cmd1.ExecuteReaderAsync();
        if (reader.HasRows)
        {
            return false;
        }


        await using var con1 = new NpgsqlConnection(constring);
        await con1.OpenAsync();
        await using var cmd = new NpgsqlCommand("INSERT INTO permas (userid) VALUES (@user_id)", con1);
        cmd.Parameters.AddWithValue("user_id", (long)user.Id);
        await cmd.ExecuteNonQueryAsync();
        return true;
    }
}