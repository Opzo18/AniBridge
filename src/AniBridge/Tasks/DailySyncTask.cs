using AniBridge.Sync;
using MediaBrowser.Model.Tasks;

namespace AniBridge.Tasks;

/// <summary>
/// Jellyfin task: sync every 24h (04:00) + manual "Run" from Dashboard → Scheduled Tasks.
/// </summary>
public sealed class DailySyncTask : IScheduledTask
{
    private readonly SyncService _sync;

    public DailySyncTask(SyncService sync)
    {
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
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
        await _sync.RunAsync(progress, cancellationToken).ConfigureAwait(false);
    }
}
