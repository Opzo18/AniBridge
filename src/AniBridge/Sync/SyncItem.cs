using AniBridge.Providers.Models;

namespace AniBridge.Sync;

public enum SyncOutcome
{
    Added,
    AlreadyExists,
    Skipped,
    Failed,
    /// <summary>
    /// Dry run only: would be added with DryRun off.
    /// </summary>
    WouldAdd,
}

/// <summary>
/// Result of a single list entry.
/// </summary>
public sealed record SyncItem(string Title, AnimeStatus Status, SyncOutcome Outcome, string? Detail);
