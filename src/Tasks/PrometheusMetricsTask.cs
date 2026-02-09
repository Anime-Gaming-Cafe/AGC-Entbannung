using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using Prometheus;
using Serilog;

namespace AGC_Entbannungssystem.Tasks;

public static class PrometheusMetricsTask
{
    private static readonly Gauge GaugeBannedUsers = Metrics.CreateGauge("agc_banned_users_total", "Anzahl aktuell gesperrter User (Sperre aktiv + PermaBlock + Rolle)");
    private static readonly Gauge GaugeOpenTickets = Metrics.CreateGauge("agc_open_tickets_total", "Anzahl offener Entbannungs-Tickets (antrag-* Channels)");

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

                    // Gesperrte User: aktive Antragssperren + PermaBlocks + User mit spezieller Rolle
                    var activeSperren = await Task.Run(() => context.Antragssperren.Count(s => s.ExpiresAt == 0 || s.ExpiresAt > now));
                    var perma = await Task.Run(() => context.PermaBlocks.Count());
                    
                    int roleBlockedCount = 0;
                    int openTicketsCount = 0;

                    try
                    {
                        var guild = await client.GetGuildAsync(unbanServerId);
                        if (guild != null)
                        {
                            // Rolle: 1180955197426634782
                            roleBlockedCount = guild.Members.Values.Count(m => m.Roles.Any(r => r.Id == 1180955197426634782));
                            
                            // Offene Tickets: Channels die mit "antrag-" beginnen
                            openTicketsCount = guild.Channels.Values.Count(c => c.Name.StartsWith("antrag-"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Warning(ex, "Fehler beim Abrufen der Guild-Daten für Metriken");
                    }

                    GaugeBannedUsers.Set(activeSperren + perma + roleBlockedCount);
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
