using System.Net.Http.Json;
using System.Text.Json;
using AniBridge.Metadata;
using AniBridge.Providers.Models;
using Microsoft.Extensions.Logging;

namespace AniBridge.Arr.Sonarr;

/// <summary>
/// Sonarr API v3 client (series). Title-based lookup via
/// /series/lookup with EXACT matching (title + alternateTitles) —
/// when uncertain, skip instead of guessing.
/// </summary>
public sealed class SonarrClient : IArrClient
{
    public const string ClientName = "Sonarr";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger<SonarrClient> _logger;
    private readonly int _qualityProfileId;
    private readonly string _rootFolderPath;
    private readonly string _monitor;
    private readonly string _seriesType;
    private readonly bool _seasonFolder;

    public SonarrClient(
        HttpClient http,
        ILogger<SonarrClient> logger,
        int qualityProfileId,
        string rootFolderPath,
        string monitor = "all",
        string seriesType = "anime",
        bool seasonFolder = true)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _qualityProfileId = qualityProfileId;
        _rootFolderPath = rootFolderPath ?? throw new ArgumentNullException(nameof(rootFolderPath));
        _monitor = string.IsNullOrWhiteSpace(monitor) ? "all" : monitor.Trim().ToLowerInvariant();
        _seriesType = string.IsNullOrWhiteSpace(seriesType) ? "anime" : seriesType.Trim().ToLowerInvariant();
        _seasonFolder = seasonFolder;
    }

    public string Name => ClientName;

    /// <summary>
    /// Production factory: BaseAddress from configuration + X-Api-Key header.
    /// </summary>
    public static SonarrClient CreateDefault(
        ILogger<SonarrClient> logger,
        string baseUrl,
        string apiKey,
        int qualityProfileId,
        string rootFolderPath,
        string monitor = "all",
        string seriesType = "anime",
        bool seasonFolder = true)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Sonarr is not configured (URL or API key).");
        }
        var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/api/v3/") };
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return new SonarrClient(http, logger, qualityProfileId, rootFolderPath, monitor, seriesType, seasonFolder);
    }

    public async Task<bool> ExistsAsync(ResolvedMedia media, CancellationToken cancellationToken = default)
    {
        foreach (var title in CandidateTitles(media))
        {
            var series = await LookupExactAsync(title, cancellationToken).ConfigureAwait(false);
            if (series is null)
            {
                continue;
            }

            var existing = await GetByTvdbIdAsync(series.TvdbId, cancellationToken).ConfigureAwait(false);
            if (existing.Count > 0)
            {
                return true;
            }

            // Found in catalog but not in library — no need to try other aliases.
            return false;
        }

        return false;
    }

    public async Task<bool> HasMatchAsync(ResolvedMedia media, CancellationToken cancellationToken = default)
    {
        foreach (var title in CandidateTitles(media))
        {
            var series = await LookupExactAsync(title, cancellationToken).ConfigureAwait(false);
            if (series is not null)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> AddAsync(ResolvedMedia media, AnimeStatus status, CancellationToken cancellationToken = default)
    {
        SonarrSeries? series = null;
        foreach (var title in CandidateTitles(media))
        {
            series = await LookupExactAsync(title, cancellationToken).ConfigureAwait(false);
            if (series is not null)
            {
                break;
            }
        }

        if (series is null)
        {
            _logger.LogWarning(
                "Sonarr: no exact match for {Title} (tried {Candidates}), skipping.",
                media.Title, string.Join(", ", CandidateTitles(media)));
            return false;
        }

        var (monitored, search) = MapMonitorFlags(status);
        var payload = new SonarrNewSeries
        {
            Title = series.Title ?? media.Title,
            TvdbId = series.TvdbId,
            QualityProfileId = _qualityProfileId,
            RootFolderPath = _rootFolderPath,
            SeasonFolder = _seasonFolder,
            Monitored = monitored,
            MonitorNewItems = _monitor,
            SeriesType = _seriesType,
            AddOptions = new SonarrAddOptions
            {
                Monitor = _monitor,
                SearchForMissingEpisodes = search,
            },
        };

        using var resp = await _http.PostAsJsonAsync(
            "series", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        _logger.LogInformation(
            "Sonarr: added {Title} (tvdb {TvdbId}, monitor {Monitor}).",
            payload.Title, payload.TvdbId, _monitor);
        return true;
    }

    /// <summary>
    /// Maps user status to monitoring flags. The monitor mode itself comes from
    /// configuration (SonarrMonitor, default "all"); Completed/Dropped neither
    /// search nor monitor new episodes; Watching searches for missing episodes immediately.
    /// </summary>
    public static (bool Monitored, bool Search) MapMonitorFlags(AnimeStatus status) =>
        status switch
        {
            AnimeStatus.Watching => (true, true),
            AnimeStatus.Planned => (true, false),
            AnimeStatus.OnHold => (true, false),
            AnimeStatus.Completed => (false, false),
            AnimeStatus.Dropped => (false, false),
            _ => (false, false),
        };

    private async Task<SonarrSeries?> LookupExactAsync(string title, CancellationToken cancellationToken)
    {
        using var resp = await _http.GetAsync(
            $"series/lookup?term={Uri.EscapeDataString(title)}", cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var candidates = await JsonSerializer.DeserializeAsync<List<SonarrSeries>>(
            stream, JsonOptions, cancellationToken).ConfigureAwait(false);

        if (candidates is null || candidates.Count == 0)
        {
            return null;
        }

        var wanted = Normalize(title);
        return candidates.FirstOrDefault(c =>
            (c.Title is not null && Normalize(c.Title) == wanted)
            || (c.AlternateTitles is not null && c.AlternateTitles.Any(a =>
                a.Title is not null && Normalize(a.Title) == wanted)));
    }

    private async Task<List<SonarrSeries>> GetByTvdbIdAsync(int tvdbId, CancellationToken cancellationToken)
    {
        using var resp = await _http.GetAsync(
            $"series?tvdbId={tvdbId}", cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<List<SonarrSeries>>(
            stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static string Normalize(string title)
    {
        var s = title.Trim().ToLowerInvariant()
            .Replace('’', '\'').Replace('‘', '\'').Replace('`', '\'');
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
        return s.TrimEnd('.', '!', '?', '…');
    }

    internal static IReadOnlyList<string> CandidateTitles(ResolvedMedia media)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>(4);
        foreach (var t in new[] { media.CanonicalTitle, media.EnglishTitle, media.Title, media.MatchedAlias })
        {
            if (string.IsNullOrWhiteSpace(t))
            {
                continue;
            }

            var trimmed = t.Trim();
            if (seen.Add(Normalize(trimmed)))
            {
                list.Add(trimmed);
            }
        }

        return list;
    }
}
