using GolfProgress.Api.Configuration;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GolfProgress.Api.Services;

/// <summary>
/// Deletes audit events older than the retention period (#20).
/// </summary>
public sealed class EventRetentionService
{
    /// <summary>
    /// Rows deleted per SaveChanges, so a large backlog is removed in bounded
    /// transactions rather than one long one.
    /// </summary>
    internal const int BatchSize = 1000;

    private readonly GolfProgressDbContext _db;
    private readonly EventRetentionOptions _options;

    public EventRetentionService(GolfProgressDbContext db, IOptions<EventRetentionOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <summary>Deletes events created before the retention cut-off. Returns how many.</summary>
    public async Task<int> PurgeAsync(DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-_options.RetentionDays);
        var deleted = 0;

        while (true)
        {
            // Loaded and removed through the change tracker rather than with
            // ExecuteDelete, so it works on every provider, including the
            // in-memory one the test suite uses.
            var batch = await _db.Events
                .Where(e => e.CreatedAt < cutoff)
                .OrderBy(e => e.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) return deleted;

            _db.Events.RemoveRange(batch);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();

            deleted += batch.Count;
        }
    }
}

/// <summary>Runs <see cref="EventRetentionService"/> on the configured interval.</summary>
public sealed class EventRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly EventRetentionOptions _options;
    private readonly ILogger<EventRetentionWorker> _logger;

    public EventRetentionWorker(
        IServiceScopeFactory scopes, IOptions<EventRetentionOptions> options, ILogger<EventRetentionWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.PurgeIntervalHours));

        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<EventRetentionService>();

                var deleted = await service.PurgeAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation(
                        "Purged {Count} events older than {Days} days.", deleted, _options.RetentionDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed purge must not stop the app; it is retried next interval.
                _logger.LogError(ex, "Event retention purge failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
