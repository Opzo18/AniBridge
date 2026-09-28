using System.Text.Json.Serialization;

namespace AniBridge.Metadata.AniList;

/// <summary>
/// AniList GraphQL response DTO (only the fields in use).
/// </summary>
public sealed class AniListSearchResponse
{
    [JsonPropertyName("data")]
    public AniListData? Data { get; init; }

    [JsonPropertyName("errors")]
    public List<AniListError>? Errors { get; init; }
}

public sealed class AniListData
{
    [JsonPropertyName("Page")]
    public AniListPage? Page { get; init; }
}

public sealed class AniListPage
{
    [JsonPropertyName("media")]
    public List<AniListMedia>? Media { get; init; }
}

public sealed class AniListMedia
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public AniListTitle? Title { get; init; }

    /// <summary>
    /// E.g. TV, TV_SHORT, MOVIE, SPECIAL, OVA, ONA.
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("episodes")]
    public int? Episodes { get; init; }

    [JsonPropertyName("startDate")]
    public AniListDate? StartDate { get; init; }
}

public sealed class AniListTitle
{
    [JsonPropertyName("romaji")]
    public string? Romaji { get; init; }

    [JsonPropertyName("english")]
    public string? English { get; init; }

    [JsonPropertyName("native")]
    public string? Native { get; init; }
}

public sealed class AniListDate
{
    [JsonPropertyName("year")]
    public int? Year { get; init; }
}

public sealed class AniListError
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
