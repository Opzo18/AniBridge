namespace AniBridge.Sync;

/// <summary>
/// Aggregate sync result. Counters computed from Items — no drift.
/// </summary>
public sealed class SyncResult
{
    public IReadOnlyList<SyncItem> Items { get; init; } = [];

    public int Scanned => Items.Count;

    public int Added => Count(SyncOutcome.Added);

    public int AlreadyExists => Count(SyncOutcome.AlreadyExists);

    public int Skipped => Count(SyncOutcome.Skipped);

    public int Failed => Count(SyncOutcome.Failed);

    public override string ToString() =>
        $"Scanned: {Scanned}, Added: {Added}, AlreadyExists: {AlreadyExists}, Skipped: {Skipped}, Failed: {Failed}";

    private int Count(SyncOutcome outcome) => Items.Count(i => i.Outcome == outcome);
}
