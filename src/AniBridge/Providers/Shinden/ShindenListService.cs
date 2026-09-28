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
        var html = await _client.GetStringAsync(
            $"animelist/{userId.Trim().Trim('/')}/all", cancellationToken).ConfigureAwait(false);
        var entries = await ShindenParser.ParseAnimeListAsync(html, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Shinden: fetched {Count} entries from list {UserId}.", entries.Count, userId);
        return entries;
    }
}
