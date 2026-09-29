using AniBridge.Sync;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace AniBridge.Tasks;

/// <summary>
/// Jellyfin task: sync every 24h (04:00) + manual "Run" from Dashboard → Scheduled Tasks.
/// A fatal error is saved to the last-sync report before rethrowing,
/// so it is visible on the settings page, not just in server logs.
/// </summary>
public sealed class DailySyncTask : IScheduledTask
{
    private readonly SyncService _sync;
    private readonly ISyncReportStore _reports;
    private readonly ILogger<DailySyncTask> _logger;

    public DailySyncTask(SyncService sync, ISyncReportStore reports, ILogger<DailySyncTask> logger)
    {
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _reports = reports ?? throw new ArgumentNullException(nameof(reports));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "AniBridge: list sync";

    public string Description => "Syncs the anime list (Shinden) with Sonarr (series) and Radarr (movies).";

    public string Category => "AniBridge";

    public string Key => "AniBridgeDailySync";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(4).Ticks,
            MaxRuntimeTicks = TimeSpan.FromHours(2).Ticks,
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            await _sync.RunAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                _reports.Save(new SyncReport
                {
                    FinishedAt = DateTimeOffset.UtcNow,
                    DryRun = Plugin.Instance?.Configuration?.DryRun ?? false,
                    Error = ex.Message,
                });
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx, "AniBridge: could not save failure report.");
            }

            throw;
        }
    }
}
