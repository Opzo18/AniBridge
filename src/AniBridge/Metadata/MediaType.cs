namespace AniBridge.Metadata;

/// <summary>
/// Media kind deciding routing: TV/series → Sonarr, movie → Radarr.
/// </summary>
public enum MediaType
{
    Tv,
    Movie,
}
