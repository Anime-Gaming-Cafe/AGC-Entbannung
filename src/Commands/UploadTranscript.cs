#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;

#endregion

namespace AGC_Entbannungssystem.Commands;

public sealed class UploadTranscript : ApplicationCommandsModule
{
    [ApplicationRequireStaffRole]
    [SlashCommand("uploadtranscript", "Lädt ein Transkript auf unseren Webserver hoch. (Bspw. für Bannsystem)")]
    public static async Task UploadTranscriptCommand(InteractionContext ctx,
        [Option("Transkript", "Die Transcript URL")]
        string transcripturl)
    {
        string path = BotConfigurator.GetConfig("MainConfig", "BackupTranscriptPath");
        if (!transcripturl.Contains("https://"))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Die URL ist ungültig!"));
            return;
        }

        // check if it contains tickettool.xyz
        if (!transcripturl.Contains("tickettool.xyz"))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Die URL ist ungültig!"));
            return;
        }

        if (!transcripturl.Contains("https://tickettool.xyz/direct?url=https://cdn.discordapp.com/attachments/"))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Die URL ist ungültig!"));
            return;
        }

        // download the file
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(transcripturl);
        if (!response.IsSuccessStatusCode)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Die URL ist ungültig!"));
            return;
        }

        var content = await response.Content.ReadAsByteArrayAsync();
        // keep the filename from the url end it at ?
        string caseid = Helperfunctions.GenerateCaseId();
        var filename = caseid + "-" + transcripturl.Split("/").Last().Split("?").First();
        // save the file
        await File.WriteAllBytesAsync(path + filename, content);
        var url = BotConfigurator.GetConfig("MainConfig", "BackupTranscriptUrl") + filename;
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().WithContent(
                $"Das Transkript wurde erfolgreich hochgeladen! URL: {url}"));
    }
}