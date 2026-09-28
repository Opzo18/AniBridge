namespace AniBridge.Providers.Models;

/// <summary>
/// Normalized entry from any service list. SyncService works on this model,
/// so new providers (MAL, AniList, …) require no sync changes.
/// </summary>
public sealed record AnimeListItem(
    string Provider,
    string ProviderId,
    string Title,
    string Url,
    AnimeStatus Status,
    int WatchedEpisodes,
    int? TotalEpisodes);
