using System.Net.Http.Json;
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

    public AniListClient(HttpClient http, ILogger<AniListClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static AniListClient CreateDefault(ILogger<AniListClient> logger)
    {
        var http = new HttpClient { BaseAddress = new Uri(Endpoint + "/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AniBridge/0.1 (Jellyfin plugin)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return new AniListClient(http, logger);
    }

    public async Task<IReadOnlyList<AniListMedia>> SearchAnimeAsync(
        string title, CancellationToken cancellationToken = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            string.Empty,
            new { query = SearchQuery, variables = new { search = title } },
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
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
