namespace InisServer.Game;

/// <summary>
/// Periodically sweeps the <see cref="GameSessionManager"/>: stale lobbies and idle,
/// disconnected sessions are evicted from memory (games stay persisted and are rebuilt
/// on demand). Intervals are configurable under "Maintenance".
/// </summary>
public sealed class MaintenanceService(
    GameSessionManager sessions, IConfiguration config, ILogger<MaintenanceService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(config.GetValue("Maintenance:SweepMinutes", 5.0));
        var lobbyTtl = TimeSpan.FromMinutes(config.GetValue("Maintenance:LobbyTtlMinutes", 120.0));
        var sessionIdle = TimeSpan.FromMinutes(config.GetValue("Maintenance:SessionIdleMinutes", 60.0));

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var removed = sessions.Sweep(lobbyTtl, sessionIdle);
                    if (removed > 0)
                        log.LogInformation("Maintenance sweep evicted {Count} lobbies/sessions", removed);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Maintenance sweep failed");
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}
