namespace AniBridge.Metadata;

/// <summary>
/// Confidently resolved title. Returned only on an unambiguous match —
/// otherwise the resolver returns null (the entry is skipped, never guessed).
/// </summary>
public sealed record ResolvedMedia(
    string Title,
    MediaType Type,
    int AniListId,
    int? TmdbId,
    int? Year,
    int? Episodes);
