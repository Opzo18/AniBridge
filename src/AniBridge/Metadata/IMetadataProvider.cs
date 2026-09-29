using AniBridge.Providers.Models;

namespace AniBridge.Metadata;

/// <summary>
/// Resolves a normalized list entry (title) to concrete media
/// (series/movie + ID). Returns null when the match is ambiguous.
/// </summary>
public interface IMetadataProvider
{
    string Name { get; }

    Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Human-readable reason for the last miss (tried queries, closest candidates),
    /// or null when unknown. Best-effort diagnostics for the settings page.
    /// </summary>
    string? DescribeLastMiss() => null;
}
