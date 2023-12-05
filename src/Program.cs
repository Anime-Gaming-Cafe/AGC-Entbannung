#region

using System.Reflection;
using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using AGC_Entbannungssystem.Tasks;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.EventArgs;
using DisCatSharp.ApplicationCommands.Exceptions;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using DisCatSharp.Exceptions;
using DisCatSharp.Interactivity;
using DisCatSharp.Interactivity.Enums;
using DisCatSharp.Interactivity.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

#endregion

namespace AGC_Entbannungssystem;

public sealed class CurrentApplicationData
{
    public static DiscordClient? Client { get; set; }
    public static DiscordUser? BotApplication { get; set; }
    public static bool isReady { get; set; }
}

internal sealed class Program
{
    private static void Main(string[] args)
    {
        MainAsync().GetAwaiter().GetResult();
    }

    private static async Task MainAsync()
    {
        var logger = Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug().Filter.ByExcluding(x => x.MessageTemplate.Text.Contains("Heartbeat"))
            .Filter.ByExcluding(x => x.MessageTemplate.Text.Contains("Updating hash"))
            .Filter.ByExcluding(x => x.MessageTemplate.Text.Contains("Bucket cleaner task"))
            .Filter.ByExcluding(x => x.MessageTemplate.Text.Contains("Request for user rate limit"))
            .Filter.ByExcluding(x => x.MessageTemplate.Text.Contains("Request for user rate limit bucket"))
            .Filter.ByExcluding(x => x.MessageTemplate.Text.Contains("unused bucket"))
            .Filter.ByExcluding(x => x.MessageTemplate.Text.Contains("Initial request"))
            .Filter.ByExcluding(x => x.MessageTemplate.Text.Contains("Socket handler suppressed an exception"))
            .WriteTo.Console()
            .CreateLogger();
        logger.Information("Starting AGC_Entbannungssystem...");

        string DcApiToken = "";
        try
        {
            DcApiToken = BotConfigurator.GetConfig("MainConfig", "Discord_Token");
        }
        catch
        {
            logger.Fatal("Discord Token not found in config. Please add the Token.");
            Console.ReadKey();
            Environment.Exit(41);
        }

        IServiceProvider serviceProvider = new ServiceCollection()
            .AddLogging(logging => { logging.AddSerilog(logger); }).BuildServiceProvider();

        var client = new DiscordClient(new DiscordConfiguration
        {
            Token = DcApiToken,
            TokenType = TokenType.Bot,
            AutoReconnect = true,
            Intents = DiscordIntents.All,
            DeveloperUserId = GlobalProperties.BotOwnerId,
            MinimumLogLevel = LogLevel.Debug,
            ServiceProvider = serviceProvider
        });


        client.ClientErrored += Discord_ClientErrored;
        client.UseInteractivity(new InteractivityConfiguration
        {
            Timeout = TimeSpan.FromMinutes(5),
            AckPaginationButtons = true,
            PaginationBehaviour = PaginationBehaviour.Ignore
        });

        var slash = client.UseApplicationCommands(new ApplicationCommandsConfiguration
        {
            ServiceProvider = serviceProvider,
            EnableDefaultHelp = false
        });
        ulong unbanServerId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanServerId"));
        slash.RegisterGuildCommands(Assembly.GetExecutingAssembly(), unbanServerId);
        client.RegisterEventHandlers(Assembly.GetExecutingAssembly());
        slash.SlashCommandErrored += Discord_SlashCommandErrored;


        client.Ready += Discord_Ready;

        await client.ConnectAsync();

        CurrentApplicationData.Client = client;
        CurrentApplicationData.BotApplication = client.CurrentUser;

        _ = RunTasks(client);


        await Task.Delay(-1);
    }

    private static async Task RunTasks(DiscordClient client)
    {
        await Task.Delay(TimeSpan.FromSeconds(20));
        _ = CheckTeamRole.Run(client);
    }


    private static async Task Discord_Ready(DiscordClient sender, ReadyEventArgs e)
    {
        CurrentApplicationData.isReady = true;
    }

    private static async Task Discord_SlashCommandErrored(ApplicationCommandsExtension sender,
        SlashCommandErrorEventArgs e)
    {
        if (e.Exception is SlashExecutionChecksFailedException)
        {
            var ex = (SlashExecutionChecksFailedException)e.Exception;
            if (ex.FailedChecks.Any(x => x is ApplicationCommandRequireUserPermissionsAttribute))
            {
                await e.Context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Du hast kei").AsEphemeral());
                e.Handled = true;
                return;
            }

            e.Handled = true;
        }
    }


    private static Task Discord_ClientErrored(DiscordClient sender, ClientErrorEventArgs e)
    {
        if (e.Exception is SlashExecutionChecksFailedException)
        {
            e.Handled = true;
            return Task.CompletedTask;
        }

        if (e.Exception is NotFoundException)
        {
            e.Handled = true;
            return Task.CompletedTask;
        }

        if (e.Exception is BadRequestException)
        {
            e.Handled = true;
            return Task.CompletedTask;
        }
        
        ErrorReporting.SendErrorToDev(sender, sender.CurrentUser, e.Exception).GetAwaiter().GetResult();

        sender.Logger.LogError($"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}");
        sender.Logger.LogError($"Stacktrace: {e.Exception.GetType()}: {e.Exception.StackTrace}");
        return Task.CompletedTask;
    }
}

public static class GlobalProperties
{
    public static ulong BotOwnerId { get; } = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "BotOwnerId"));
    public static ulong UnbanServerId { get; } = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanServerId"));
    public static ulong MainGuildId { get; } = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "MainServerId"));

    public static ulong DevGuildId { get; } = ulong.Parse(BotConfigurator.GetConfig("ErrorTracking", "DeveloperServerId"));
    public static bool ErrorTrackingEnabled { get; } =
        bool.Parse(BotConfigurator.GetConfig("ErrorTracking", "ErrorTrackingEnabled"));
    public static ulong ErrorTrackingChannelId { get; } =
        ulong.Parse(BotConfigurator.GetConfig("ErrorTracking", "DeveloperGuildErrorChannelId"));
    public static ulong MainGuildTeamRoleId { get; } =
        ulong.Parse(BotConfigurator.GetConfig("MainConfig", "MainGuildTeamRoleId"));

    public static ulong UnbanServerTeamRoleId { get; } =
        ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanGuildTeamRoleId"));
}