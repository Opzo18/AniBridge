using AniBridge.Providers.Models;
using Microsoft.Extensions.Logging;

namespace AniBridge.Providers.Shinden;

/// <summary>
/// Shinden provider: combines ShindenClient (session) and ShindenListService (fetching)
/// with mapping to the shared AnimeListItem. No Sonarr/Radarr logic.
/// </summary>
public sealed class ShindenProvider : IAnimeProvider
{
    public const string ProviderName = "Shinden";

    private readonly ShindenClient _client;
    private readonly ShindenListService _lists;
    private readonly ILogger<ShindenProvider> _logger;

    public ShindenProvider(
        ShindenClient client, ShindenListService lists, ILogger<ShindenProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => ProviderName;

    public async Task<IReadOnlyList<AnimeListItem>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.ShindenEnabled)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(config.ShindenUsername)
            || string.IsNullOrWhiteSpace(config.ShindenUserId))
        {
            _logger.LogInformation("Shinden: missing configuration (login or list ID), skipping.");
            return [];
        }

        if (!await _client.LoginAsync(
                config.ShindenUsername, config.ShindenPassword, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var entries = await _lists.GetAnimeListAsync(config.ShindenUserId, cancellationToken).ConfigureAwait(false);

        var items = new List<AnimeListItem>(entries.Count);
        var skipped = 0;
        foreach (var entry in entries)
        {
            var item = MapEntry(entry);
            if (item is null)
            {
                skipped++;
                _logger.LogWarning(
                    "Shinden: unknown status {Status} for {Title}, skipping.",
                    entry.StatusText, entry.Title);
            }
            else
            {
                items.Add(item);
            }
        }

        _logger.LogInformation(
            "Shinden: normalized {Count} entries, skipped {Skipped}.", items.Count, skipped);
        return items;
    }

    public static AnimeListItem? MapEntry(ShindenListEntry entry)
    {
        var status = MapStatus(entry.StatusText);
        if (status is null)
        {
            return null;
        }

        return new AnimeListItem(
            ProviderName,
            entry.ShindenId.ToString(),
            entry.Title,
            ShindenClient.BaseUrl + entry.Url,
            status.Value,
            entry.WatchedEpisodes,
            entry.TotalEpisodes);
    }

    public static AnimeStatus? MapStatus(string statusText) => statusText.Trim() switch
    {
        "Oglądam" => AnimeStatus.Watching,
        "Obejrzane" => AnimeStatus.Completed,
        "Planuję" => AnimeStatus.Planned,
        "Wstrzymane" => AnimeStatus.OnHold,
        "Porzucone" => AnimeStatus.Dropped,
        _ => null,
    };
}
