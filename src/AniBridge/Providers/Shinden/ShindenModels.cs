namespace AniBridge.Providers.Shinden;

/// <summary>
/// Raw Shinden list entry — exactly what the HTML shows.
/// Interpretation (status mapping) happens later in Stage 4.
/// </summary>
public sealed class ShindenListEntry
{
    /// <summary>
    /// Shinden title ID (data-title-id attribute, e.g. 59480).
    /// </summary>
    public int ShindenId { get; init; }

    /// <summary>
    /// Title as shown on the list.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Relative title URL, e.g. /series/59480-noumin-kanren-no-skill-....
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Raw Shinden status text, e.g. "Oglądam", "Obejrzane", "Planuję".
    /// </summary>
    public string StatusText { get; init; } = string.Empty;

    public int WatchedEpisodes { get; init; }

    public int? TotalEpisodes { get; init; }

    /// <summary>
    /// Raw type text, e.g. "TV". Series/movie distinction in Stage 5 (AniList/TMDB).
    /// </summary>
    public string TypeText { get; init; } = string.Empty;
}
