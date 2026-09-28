using System.Text.Json.Serialization;

namespace AniBridge.Arr.Sonarr;

/// <summary>
/// Minimal Sonarr API v3 DTO (only used fields).
/// </summary>
public sealed class SonarrSeries
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("tvdbId")]
    public int TvdbId { get; init; }

    [JsonPropertyName("alternateTitles")]
    public List<SonarrAlternateTitle>? AlternateTitles { get; init; }
}

public sealed class SonarrAlternateTitle
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

/// <summary>
/// POST /api/v3/series payload. Enums as strings in Sonarr format
/// (monitor: all/future/none, seriesType: anime).
/// </summary>
public sealed class SonarrNewSeries
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("tvdbId")]
    public int TvdbId { get; init; }

    [JsonPropertyName("qualityProfileId")]
    public int QualityProfileId { get; init; }

    [JsonPropertyName("rootFolderPath")]
    public string RootFolderPath { get; init; } = string.Empty;

    [JsonPropertyName("seasonFolder")]
    public bool SeasonFolder { get; init; } = true;

    [JsonPropertyName("monitored")]
    public bool Monitored { get; init; }

    [JsonPropertyName("monitorNewItems")]
    public string MonitorNewItems { get; init; } = "all";

    [JsonPropertyName("seriesType")]
    public string SeriesType { get; init; } = "anime";

    [JsonPropertyName("addOptions")]
    public SonarrAddOptions AddOptions { get; init; } = new();
}

public sealed class SonarrAddOptions
{
    [JsonPropertyName("monitor")]
    public string Monitor { get; init; } = "all";

    [JsonPropertyName("searchForMissingEpisodes")]
    public bool SearchForMissingEpisodes { get; init; }

    [JsonPropertyName("searchForCutoffUnmetEpisodes")]
    public bool SearchForCutoffUnmetEpisodes { get; init; }
}
