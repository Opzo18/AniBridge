using System.Text.Json.Serialization;

namespace AniBridge.Arr.Radarr;

/// <summary>
/// Minimal Radarr API v3 DTO (only used fields).
/// </summary>
public sealed class RadarrMovie
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("tmdbId")]
    public int TmdbId { get; init; }

    [JsonPropertyName("year")]
    public int Year { get; init; }

    [JsonPropertyName("alternateTitles")]
    public List<RadarrAlternateTitle>? AlternateTitles { get; init; }
}

public sealed class RadarrAlternateTitle
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

/// <summary>
/// POST /api/v3/movie payload. Enums as strings in Radarr format
/// (monitor: movieOnly/none, minimumAvailability: announced/inCinemas/released).
/// </summary>
public sealed class RadarrNewMovie
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("tmdbId")]
    public int TmdbId { get; init; }

    [JsonPropertyName("year")]
    public int Year { get; init; }

    [JsonPropertyName("qualityProfileId")]
    public int QualityProfileId { get; init; }

    [JsonPropertyName("rootFolderPath")]
    public string RootFolderPath { get; init; } = string.Empty;

    [JsonPropertyName("monitored")]
    public bool Monitored { get; init; }

    [JsonPropertyName("minimumAvailability")]
    public string MinimumAvailability { get; init; } = "released";

    [JsonPropertyName("addOptions")]
    public RadarrAddOptions AddOptions { get; init; } = new();
}

public sealed class RadarrAddOptions
{
    [JsonPropertyName("monitor")]
    public string Monitor { get; init; } = "movieOnly";

    [JsonPropertyName("searchForMovie")]
    public bool SearchForMovie { get; init; }
}
