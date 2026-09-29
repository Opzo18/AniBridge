using System.Net.Http.Json;
using System.Text.Json;
using AniBridge.Metadata;
using AniBridge.Providers.Models;
using Microsoft.Extensions.Logging;

namespace AniBridge.Arr.Radarr;

/// <summary>
/// Radarr API v3 client (movies). Title-based lookup via
/// /movie/lookup with EXACT matching — when uncertain, skip.
/// </summary>
public sealed class RadarrClient : IArrClient
{
    public const string ClientName = "Radarr";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger<RadarrClient> _logger;
    private readonly int _qualityProfileId;
    private readonly string _rootFolderPath;
    private readonly string _monitor;
    private readonly string _availability;

    public RadarrClient(
        HttpClient http,
        ILogger<RadarrClient> logger,
        int qualityProfileId,
        string rootFolderPath,
        string monitor = "movieOnly",
        string availability = "released")
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _qualityProfileId = qualityProfileId;
        _rootFolderPath = rootFolderPath ?? throw new ArgumentNullException(nameof(rootFolderPath));
        _monitor = string.IsNullOrWhiteSpace(monitor) ? "movieOnly" : monitor.Trim();
        _availability = string.IsNullOrWhiteSpace(availability) ? "released" : availability.Trim();
    }

    public string Name => ClientName;

    /// <summary>
    /// Production factory: BaseAddress from configuration + X-Api-Key header.
    /// </summary>
    public static RadarrClient CreateDefault(
        ILogger<RadarrClient> logger,
        string baseUrl,
        string apiKey,
        int qualityProfileId,
        string rootFolderPath,
        string monitor = "movieOnly",
        string availability = "released")
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Radarr is not configured (URL or API key).");
        }

        var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/api/v3/") };
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return new RadarrClient(http, logger, qualityProfileId, rootFolderPath, monitor, availability);
    }

    public async Task<bool> ExistsAsync(ResolvedMedia media, CancellationToken cancellationToken = default)
    {
        foreach (var title in CandidateTitles(media))
        {
            var movie = await LookupExactAsync(title, cancellationToken).ConfigureAwait(false);
            if (movie is null)
            {
                continue;
            }

            var existing = await GetByTmdbIdAsync(movie.TmdbId, cancellationToken).ConfigureAwait(false);
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
            var movie = await LookupExactAsync(title, cancellationToken).ConfigureAwait(false);
            if (movie is not null)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> AddAsync(ResolvedMedia media, AnimeStatus status, CancellationToken cancellationToken = default)
    {
        RadarrMovie? movie = null;
        foreach (var title in CandidateTitles(media))
        {
            movie = await LookupExactAsync(title, cancellationToken).ConfigureAwait(false);
            if (movie is not null)
            {
                break;
            }
        }

        if (movie is null)
        {
            _logger.LogWarning(
                "Radarr: no exact match for {Title} (tried {Candidates}), skipping.",
                media.Title, string.Join(", ", CandidateTitles(media)));
            return false;
        }

        var (monitored, search) = MapMonitorFlags(status);
        var payload = new RadarrNewMovie
        {
            Title = movie.Title ?? media.Title,
            TmdbId = movie.TmdbId,
            Year = movie.Year,
            QualityProfileId = _qualityProfileId,
            RootFolderPath = _rootFolderPath,
            Monitored = monitored,
            MinimumAvailability = _availability,
            AddOptions = new RadarrAddOptions
            {
                Monitor = _monitor,
                SearchForMovie = search,
            },
        };

        using var resp = await _http.PostAsJsonAsync(
            "movie", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        _logger.LogInformation(
            "Radarr: added {Title} (tmdb {TmdbId}, monitor {Monitor}).",
            payload.Title, payload.TmdbId, _monitor);
        return true;
    }

    /// <summary>
    /// Maps user status to monitoring flags. Monitor mode and minimum availability
    /// come from configuration (RadarrMonitor/RadarrAvailability);
    /// Watching searches for the movie immediately.
    /// </summary>
    public static (bool Monitored, bool Search) MapMonitorFlags(AnimeStatus status) => status switch
        {
            AnimeStatus.Watching => (true, true),
            AnimeStatus.Planned => (true, false),
            AnimeStatus.OnHold => (true, false),
            AnimeStatus.Completed => (false, false),
            AnimeStatus.Dropped => (false, false),
            _ => (false, false),
        };

    private async Task<RadarrMovie?> LookupExactAsync(string title, CancellationToken cancellationToken)
    {
        using var resp = await _http.GetAsync(
            $"movie/lookup?term={Uri.EscapeDataString(title)}", cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var candidates = await JsonSerializer.DeserializeAsync<List<RadarrMovie>>(
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

    private async Task<List<RadarrMovie>> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken)
    {
        using var resp = await _http.GetAsync(
            $"movie?tmdbId={tmdbId}", cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<List<RadarrMovie>>(
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
