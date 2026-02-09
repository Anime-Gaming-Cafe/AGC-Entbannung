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
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

#endregion

namespace AGC_Entbannungssystem;

public sealed class CurrentApplicationData
{
    public static ILogger Logger { get; set; }
    public static string VersionString { get; } = GetVersionString();
    public static DiscordClient? Client { get; set; }
    public static DiscordUser? BotApplication { get; set; }
    public static bool isReady { get; set; }

    private static string GetVersionString()
    {
        try
        {
            var version = typeof(Program)
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            Console.Out.WriteLineAsync(version);

            if (!string.IsNullOrEmpty(version))
            {
                if (version.StartsWith("v"))
                {
                    return version;
                }

                try
                {
                    if (Logger != null)
                    {
                        Logger.Warning(
                            $"Version string '{version}' doesn't follow the expected format (should start with 'v')");
                    }
                }
                catch
                {
                }

                return version;
            }

            try
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                return $"0.0.1-nightly.{timestamp}";
            }
            catch (Exception ex)
            {
                try
                {
                    if (Logger != null)
                    {
                        Logger.Error(ex, "Failed to generate timestamp for version string");
                    }
                }
                catch
                {
                }

                return "0.0.1-nightly.unknown";
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (Logger != null)
                {
                    Logger.Error(ex, "Failed to determine version string");
                }
            }
            catch
            {
            }

            return "0.0.1-unknown";
        }
    }
}

internal sealed class Program
{
    private static void Main(string[] args)
    {
        MainAsync().GetAwaiter().GetResult();
    }

    private static async Task MainAsync()
    {
        var builder = WebApplication.CreateBuilder();

        var logger = Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("DisCatSharp", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.HealthChecks", LogEventLevel.Warning)
            .WriteTo.Console()
            .CreateLogger();


        
        builder.Host.UseSerilog(logger);
        
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

        var config = new DiscordConfiguration
        {
            Token = DcApiToken,
            TokenType = TokenType.Bot,
            AutoReconnect = true,
            Intents = DiscordIntents.All,
            DeveloperUserId = GlobalProperties.BotOwnerId,
            MinimumLogLevel = LogLevel.Information,
            DisableUpdateCheck = true,
            ShowReleaseNotesInUpdateCheck = false
        };

        var proxyHost = BotConfigurator.GetConfig("MainConfig", "RestProxyHost");
        var proxyPort = BotConfigurator.GetConfig("MainConfig", "RestProxyPort");

        if (!string.IsNullOrEmpty(proxyHost) && !string.IsNullOrEmpty(proxyPort) && int.TryParse(proxyPort, out int port))
        {
            // substitue the url with /api/v10
            string url = $"http://{proxyHost}:{port}/api/v10";
            config.Proxy = new System.Net.WebProxy(url);
        }

        builder.Services.AddSingleton(new DiscordClient(config));

        builder.Services.AddHealthChecks()
            .AddCheck("Liveness", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck("Discord-Connection", () =>
            {
                var client = CurrentApplicationData.Client;
                if (client == null)
                {
                    return HealthCheckResult.Unhealthy("DiscordClient not initialized");
                }

                if (!CurrentApplicationData.isReady)
                {
                    return HealthCheckResult.Degraded("DiscordClient is not yet ready");
                }

                return client.Ping >= 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy($"Discord not connected (Ping: {client.Ping})");
            }, tags: ["ready"]);

        var app = builder.Build();

        app.UseHttpMetrics();
        app.MapHealthChecks("/healthz/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });
        app.MapMetrics();

        var client = app.Services.GetRequiredService<DiscordClient>();
        var serviceProvider = app.Services;
        CurrentApplicationData.Client = client;

        ulong unbanServerId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanServerId"));



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
            EnableDefaultHelp = false,
            CheckAllGuilds = true, DebugStartup = true
        });
        slash.RegisterGuildCommands(Assembly.GetExecutingAssembly(), unbanServerId);
        client.RegisterEventHandlers(Assembly.GetExecutingAssembly());
        slash.SlashCommandErrored += Discord_SlashCommandErrored;


        client.Ready += Discord_Ready;

        await client.ConnectAsync(new DiscordActivity("Verwaltung der Entbannungen",
            ActivityType.Custom), UserStatus.Idle);

        CurrentApplicationData.BotApplication = client.CurrentUser;

        _ = RunTasks(client);

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() =>
        {
            logger.Information("SIGTERM received, disconnecting Discord client...");
            client.DisconnectAsync().GetAwaiter().GetResult();
            logger.Information("Discord client disconnected.");
        });

        await app.RunAsync();
    }

    private static async Task RunTasks(DiscordClient client)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        _ = PrometheusMetricsTask.Run(client);
        _ = CheckTeamRole.Run(client);
        _ = CheckExpiredVotes.Run(client);
        _ = CheckExpiredBlock.Run(client);
        _ = FillAutocompletions.Run(client);
        _ = UpdateVoteMessages.Run(client);
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
                    new DiscordInteractionResponseBuilder().WithContent("Du hast keine Berechtigung :3").AsEphemeral());
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

    public static ulong PingRoleId { get; } =
        ulong.Parse(BotConfigurator.GetConfig("MainConfig", "PingRoleId"));

    public static ulong DevGuildId { get; } =
        ulong.Parse(BotConfigurator.GetConfig("ErrorTracking", "DeveloperServerId"));

    public static bool ErrorTrackingEnabled { get; } =
        bool.Parse(BotConfigurator.GetConfig("ErrorTracking", "ErrorTrackingEnabled"));

    public static ulong ErrorTrackingChannelId { get; } =
        ulong.Parse(BotConfigurator.GetConfig("ErrorTracking", "DeveloperGuildErrorChannelId"));

    public static ulong MainGuildTeamRoleId { get; } =
        ulong.Parse(BotConfigurator.GetConfig("MainConfig", "MainGuildTeamRoleId"));

    public static ulong UnbanServerTeamRoleId { get; } =
        ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanGuildTeamRoleId"));

    public static bool isBannSystemEnabled { get; } =
        bool.Parse(BotConfigurator.GetConfig("ModHQConfig", "BannSystemEnabled"));
}