using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;

namespace AGC_Entbannungssystem.Helpers;

public static class ErrorReporting
{
    public static async Task SendErrorToDev(DiscordClient client, DiscordUser user,
        Exception exception)
    {
        if (!GlobalProperties.ErrorTrackingEnabled) return;
        var botOwner = await client.GetUserAsync(GlobalProperties.BotOwnerId);
        var embed2 = new DiscordEmbedBuilder();
        embed2.WithTitle("Fehler aufgetreten!");
        embed2.WithDescription($"Es ist ein Fehler aufgetreten. \n\n" +
                               $"__Fehlermeldung:__\n" +
                               $"```{exception.Message}```\n" +
                               $"__Stacktrace:__\n" +
                               $"```{exception.StackTrace}```\n" +
                               $"__User:__\n" +
                               $"``{user.UsernameWithDiscriminator}`` - ``{user.Id}``\n");
        embed2.WithColor(DiscordColor.Red);
        try
        {
            await botOwner.SendMessageAsync(embed: embed2);
        }
        catch (Exception)
        {
            // ignored
        }

        try
        {
            ulong errortrackingguildid = GlobalProperties.DevGuildId;
            var errortrackingguild = await client.GetGuildAsync(errortrackingguildid);
            var errortrackingchannel = errortrackingguild.GetChannel(GlobalProperties.ErrorTrackingChannelId);
            await errortrackingchannel.SendMessageAsync(embed: embed2);
        }
        catch (Exception)
        {
            
        }
        
    }
}