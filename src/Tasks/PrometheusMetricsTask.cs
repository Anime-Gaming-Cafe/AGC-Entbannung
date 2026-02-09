using System.Text.RegularExpressions;
using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using Prometheus;
using Serilog;

namespace AGC_Entbannungssystem.Tasks;

public static class PrometheusMetricsTask
{
    private static readonly Gauge GaugeBannedUsers = Metrics.CreateGauge("agc_unbans_blocked_users", "Anzahl aktuell gesperrter User (Sperre aktiv + PermaBlock)");
    private static readonly Gauge GaugeOpenTickets = Metrics.CreateGauge("agc_unbans_open_tickets_total", "Anzahl offener Entbannungs-Tickets (antrag-XXXX Channels)");

    public static async Task Run(DiscordClient client)
    {
        ulong unbanServerId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanServerId"));
        
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await using var context = AgcDbContextFactory.CreateDbContext();
                    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    var activeSperren = await Task.Run(() => context.Antragssperren.Count(s => s.ExpiresAt == 0 || s.ExpiresAt > now));
                    var perma = await Task.Run(() => context.PermaBlocks.Count());
                    
                    int openTicketsCount = 0;

                    try
                    {
                        var guild = await client.GetGuildAsync(unbanServerId);
                        if (guild != null)
                        {
                            openTicketsCount = guild.Channels.Values.Count(c => Regex.IsMatch(c.Name, @"^antrag-\d{4}$"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Warning(ex, "Fehler beim Abrufen der Guild-Daten für Metriken");
                    }

                    GaugeBannedUsers.Set(activeSperren + perma);
                    GaugeOpenTickets.Set(openTicketsCount);
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Fehler beim Aktualisieren der Prometheus-Metriken");
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        });
    }
}
