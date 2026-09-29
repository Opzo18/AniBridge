using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AniBridge.Metadata.AniList;

/// <summary>
/// GraphQL transport to AniList (no API key). Anime search only —
/// matching and interpretation live in AniListMetadataProvider.
/// AniList limit: 90 req/min — SyncService will add pacing when calling in bulk.
/// </summary>
public sealed class AniListClient
{
    public const string Endpoint = "https://graphql.anilist.co";

    private const string SearchQuery = """
        query ($search: String) {
          Page(perPage: 5) {
            media(search: $search, type: ANIME) {
              id
              title { romaji english native }
              synonyms
              format
              episodes
              startDate { year }
            }
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger<AniListClient> _logger;
    private readonly TimeSpan _retryBaseDelay;

    public AniListClient(HttpClient http, ILogger<AniListClient> logger, TimeSpan? retryBaseDelay = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryBaseDelay = retryBaseDelay ?? TimeSpan.FromSeconds(15);
    }

    public static AniListClient CreateDefault(ILogger<AniListClient> logger)
    {
        var http = new HttpClient { BaseAddress = new Uri(Endpoint + "/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AniBridge/0.1 (Jellyfin plugin)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return new AniListClient(http, logger);
    }

    private const int MaxAttempts = 5;

    public async Task<IReadOnlyList<AniListMedia>> SearchAnimeAsync(
        string title, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(
            new { query = SearchQuery, variables = new { search = title } }, JsonOptions);

        for (var attempt = 1; ; attempt++)
        {
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(string.Empty, content, cancellationToken).ConfigureAwait(false);

            if ((int)resp.StatusCode == 429 && attempt < MaxAttempts)
            {
                var delay = GetRetryDelay(resp, attempt);
                _logger.LogWarning(
                    "AniList: rate limited for {Title}, retrying in {Delay}s (attempt {Attempt}/{Max}).",
                    title, (int)delay.TotalSeconds, attempt, MaxAttempts);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<AniListSearchResponse>(
                stream, JsonOptions, cancellationToken).ConfigureAwait(false);

            if (result?.Errors is { Count: > 0 })
            {
                _logger.LogWarning("AniList: search error for {Title}: {Error}", title, result.Errors[0].Message);
                return [];
            }

            return result?.Data?.Page?.Media ?? [];
        }
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage resp, int attempt)
    {
        if (resp.Headers.RetryAfter?.Delta is { } delta
            && delta > TimeSpan.Zero
            && delta <= TimeSpan.FromMinutes(5))
        {
            return delta;
        }

        return TimeSpan.FromTicks(_retryBaseDelay.Ticks * attempt);
    }
}
