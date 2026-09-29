using AniBridge.Metadata;
using AniBridge.Providers.Models;

namespace AniBridge.Arr;

/// <summary>
/// *Arr client (Sonarr/Radarr). Check and add only —
/// never deletes media or files.
/// </summary>
public interface IArrClient
{
    string Name { get; }

    Task<bool> ExistsAsync(ResolvedMedia media, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the title can be matched in the *Arr catalog (lookup only,
    /// no library check). Used for dry-run so WouldAdd is not reported for
    /// titles that would be skipped live.
    /// </summary>
    Task<bool> HasMatchAsync(ResolvedMedia media, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the item was posted to *Arr, false when there was
    /// no exact catalog match (caller reports Skipped, never Added).
    /// </summary>
    Task<bool> AddAsync(ResolvedMedia media, AnimeStatus status, CancellationToken cancellationToken = default);
}
