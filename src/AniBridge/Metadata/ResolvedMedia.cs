namespace AniBridge.Metadata;

/// <summary>
/// Confidently resolved title. Returned only on an unambiguous match —
/// otherwise the resolver returns null (the entry is failed, never guessed).
/// Title is the original list title (for UI); CanonicalTitle/EnglishTitle are
/// the AniList canonical names used first for Sonarr/Radarr lookup;
/// Synonyms are extra AniList alternate titles tried as further candidates.
/// </summary>
public sealed record ResolvedMedia(
    string Title,
    MediaType Type,
    int AniListId,
    int? TmdbId,
    int? Year,
    int? Episodes,
    string? MatchedAlias = null,
    string? CanonicalTitle = null,
    string? EnglishTitle = null,
    IReadOnlyList<string>? Synonyms = null);
