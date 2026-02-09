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
        builder.Services.AddHostedService<DiscordBotService>();

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

        await app.RunAsync();
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