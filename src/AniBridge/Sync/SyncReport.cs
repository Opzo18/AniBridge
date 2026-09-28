namespace AniBridge.Sync;

/// <summary>
/// Persisted outcome of the last sync run, shown on the settings page
/// so users don't have to dig through server logs.
/// </summary>
public sealed class SyncReport
{
    public DateTimeOffset FinishedAt { get; init; }

    public bool DryRun { get; init; }

    public SyncResult Result { get; init; } = new();
}
