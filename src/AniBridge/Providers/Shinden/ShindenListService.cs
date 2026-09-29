using Microsoft.Extensions.Logging;

namespace AniBridge.Providers.Shinden;

/// <summary>
/// Fetches and parses the user's anime list. The /all page returns all
/// statuses in a single request (in-progress, completed, plan, hold, dropped sections).
/// </summary>
public sealed class ShindenListService
{
    private readonly ShindenClient _client;
    private readonly ILogger<ShindenListService> _logger;

    public ShindenListService(ShindenClient client, ILogger<ShindenListService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ShindenListEntry>> GetAnimeListAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUserId(userId);
        var relativeUrl = $"animelist/{normalized}/all";
        string html;
        try
        {
            html = await _client.GetStringAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Shinden: failed to fetch list '{ShindenClient.BaseUrl}/{relativeUrl}' " +
                $"(List ID setting: '{userId}').", ex);
        }

        var entries = await ShindenParser.ParseAnimeListAsync(html, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Shinden: fetched {Count} entries from list {UserId}.", entries.Count, normalized);
        return entries;
    }

    /// <summary>
    /// Alternate titles from a Shinden title page, for AniList retry queries.
    /// Best-effort: any failure yields an empty list. Accepts the absolute
    /// AnimeListItem.Url or a relative /series/… path.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetTitleAliasesAsync(
        string titleUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var relative = titleUrl;
            const string marker = ".pl/";
            var i = relative.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                relative = relative[(i + marker.Length)..];
            }

            relative = relative.Trim().TrimStart('/');
            if (relative.Length == 0)
            {
                return [];
            }

            var html = await _client.GetStringAsync(relative, cancellationToken).ConfigureAwait(false);
            return await ShindenParser.ParseTitleAliasesAsync(html, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Shinden: could not fetch title aliases from {Url}.", titleUrl);
            return [];
        }
    }

    /// <summary>
    /// Accepts a bare list ID ("420984-opzo") or a pasted list URL
    /// (https://shinden.pl/animelist/420984-opzo, with or without /all).
    /// </summary>
    public static string NormalizeUserId(string userId)
    {
        var s = userId.Trim().Trim('/');
        const string marker = "animelist/";
        var i = s.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i >= 0)
        {
            s = s[(i + marker.Length)..];
        }

        s = s.Split('?', '#')[0];
        s = s.Split('/')[0];
        return s.Trim();
    }
}
