using AniBridge.Providers.Models;

namespace AniBridge.Providers;

/// <summary>
/// Provides a normalized title list from a given service.
/// Responsible only for fetching data — no Sonarr/Radarr logic.
/// </summary>
public interface IAnimeProvider
{
    string Name { get; }

    Task<IReadOnlyList<AnimeListItem>> GetListAsync(CancellationToken cancellationToken = default);
}
