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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AGC_Entbannungssystem;

public class DiscordBotService : IHostedService
{
    private readonly DiscordClient _client;
    private readonly IServiceProvider _serviceProvider;
    private readonly Microsoft.Extensions.Logging.ILogger<DiscordBotService> _logger;

    public DiscordBotService(DiscordClient client, IServiceProvider serviceProvider, Microsoft.Extensions.Logging.ILogger<DiscordBotService> logger)
    {
        _client = client;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Discord bot service...");
        CurrentApplicationData.Client = _client;

        ulong unbanServerId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanServerId"));

        _client.ClientErrored += Discord_ClientErrored;
        _client.UseInteractivity(new InteractivityConfiguration
        {
            Timeout = TimeSpan.FromMinutes(5),
            AckPaginationButtons = true,
            PaginationBehaviour = PaginationBehaviour.Ignore
        });

        var slash = _client.UseApplicationCommands(new ApplicationCommandsConfiguration
        {
            ServiceProvider = _serviceProvider,
            EnableDefaultHelp = false,
            CheckAllGuilds = true,
            DebugStartup = true
        });
        
        slash.RegisterGuildCommands(Assembly.GetExecutingAssembly(), unbanServerId);
        _client.RegisterEventHandlers(Assembly.GetExecutingAssembly());
        slash.SlashCommandErrored += Discord_SlashCommandErrored;
        _client.Ready += Discord_Ready;

        await _client.ConnectAsync(new DiscordActivity("Verwaltung der Entbannungen", ActivityType.Custom), UserStatus.Idle);

        CurrentApplicationData.BotApplication = _client.CurrentUser;

        _ = RunTasks(_client);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Discord bot service...");
        if (CurrentApplicationData.Client != null)
        {
            await CurrentApplicationData.Client.DisconnectAsync();
            _logger.LogInformation("Discord client disconnected.");
        }
    }

    private async Task RunTasks(DiscordClient client)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        _ = PrometheusMetricsTask.Run(client);
        _ = CheckTeamRole.Run(client);
        _ = CheckExpiredVotes.Run(client);
        _ = CheckExpiredBlock.Run(client);
        _ = FillAutocompletions.Run(client);
        _ = UpdateVoteMessages.Run(client);
    }

    private async Task Discord_Ready(DiscordClient sender, ReadyEventArgs e)
    {
        CurrentApplicationData.isReady = true;
        await Task.CompletedTask;
    }

    private async Task Discord_SlashCommandErrored(ApplicationCommandsExtension sender, SlashCommandErrorEventArgs e)
    {
        if (e.Exception is SlashExecutionChecksFailedException ex)
        {
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

    private async Task Discord_ClientErrored(DiscordClient sender, ClientErrorEventArgs e)
    {
        if (e.Exception is SlashExecutionChecksFailedException or NotFoundException or BadRequestException)
        {
            e.Handled = true;
            return;
        }

        try
        {
            await ErrorReporting.SendErrorToDev(sender, sender.CurrentUser, e.Exception);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to send error to dev");
        }

        _logger.LogError(e.Exception, "Exception occurred: {ExceptionType}: {Message}", e.Exception.GetType(), e.Exception.Message);
    }
}
