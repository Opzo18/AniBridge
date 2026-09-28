using AniBridge.Providers.Models;

namespace AniBridge.Sync;

public enum SyncOutcome
{
    Added,
    AlreadyExists,
    Skipped,
    Failed,
}

/// <summary>
/// Result of a single list entry.
/// </summary>
public sealed record SyncItem(string Title, AnimeStatus Status, SyncOutcome Outcome, string? Detail);
