namespace AniBridge.Sync;

/// <summary>
/// Persisted outcome of the last sync run, shown on the settings page
/// so users don't have to dig through server logs.
/// </summary>
public sealed class SyncReport
{
    public DateTimeOffset FinishedAt { get; init; }

    public bool DryRun { get; init; }

    /// <summary>
    /// Fatal sync error (e.g. list fetch failed). Set when no per-item results exist.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// True for interim snapshots written while the sync is still running.
    /// The settings page keeps polling until a report with false arrives.
    /// Defaults to false so reports written by older versions read as final.
    /// </summary>
    public bool InProgress { get; init; }

    /// <summary>
    /// Human-readable sync scope, e.g. "Watching, Planned".
    /// Null for reports written by older versions.
    /// </summary>
    public string? Scope { get; init; }

    public SyncResult Result { get; init; } = new();
}
