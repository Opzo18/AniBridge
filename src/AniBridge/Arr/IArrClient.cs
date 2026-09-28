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

    Task AddAsync(ResolvedMedia media, AnimeStatus status, CancellationToken cancellationToken = default);
}
