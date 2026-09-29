using AniBridge.Arr;
using AniBridge.Arr.Radarr;
using AniBridge.Arr.Sonarr;
using AniBridge.Configuration;
using AniBridge.Metadata;
using AniBridge.Providers;
using AniBridge.Providers.Models;
using Microsoft.Extensions.Logging;

namespace AniBridge.Sync;

/// <summary>
/// Main application layer: Provider → AnimeListItem → Metadata → TV/Movie → Sonarr/Radarr.
/// Idempotent (exists-before-add); one entry failing does not stop the rest.
/// </summary>
public sealed class SyncService
{
    private static readonly TimeSpan DefaultPacing = TimeSpan.FromMilliseconds(1000); // AniList limit 90/min

    private readonly IAnimeProvider _provider;
    private readonly IMetadataProvider _metadata;
    private readonly Lazy<SonarrClient> _sonarr;
    private readonly Lazy<RadarrClient> _radarr;
    private readonly ILogger<SyncService> _logger;
    private readonly Func<PluginConfiguration?> _config;
    private readonly TimeSpan _pacing;
    private readonly ISyncReportStore? _reportStore;

    public SyncService(
        IAnimeProvider provider,
        IMetadataProvider metadata,
        Lazy<SonarrClient> sonarr,
        Lazy<RadarrClient> radarr,
        ILogger<SyncService> logger,
        Func<PluginConfiguration?>? configProvider = null,
        TimeSpan? pacing = null,
        ISyncReportStore? reportStore = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _sonarr = sonarr ?? throw new ArgumentNullException(nameof(sonarr));
        _radarr = radarr ?? throw new ArgumentNullException(nameof(radarr));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = configProvider ?? (() => Plugin.Instance?.Configuration);
        _pacing = pacing ?? DefaultPacing;
        _reportStore = reportStore;
    }

    public async Task<SyncResult> RunAsync(
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var config = _config();
        var items = await _provider.GetListAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<SyncItem>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            results.Add(await ProcessAsync(items[i], config, cancellationToken).ConfigureAwait(false));
            progress?.Report((double)(i + 1) / items.Count);
            if (i + 1 < items.Count)
            {
                await Task.Delay(_pacing, cancellationToken).ConfigureAwait(false);
            }
        }

        var result = new SyncResult { Items = results };
        _logger.LogInformation("AniBridge: sync finished: {Result}", result);
        SaveReport(config, result);
        return result;
    }

    private void SaveReport(PluginConfiguration? config, SyncResult result)
    {
        if (_reportStore is null)
        {
            return;
        }

        try
        {
            _reportStore.Save(new SyncReport
            {
                FinishedAt = DateTimeOffset.UtcNow,
                DryRun = config?.DryRun ?? false,
                Result = result,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AniBridge: could not save last sync report.");
        }
    }

    private async Task<SyncItem> ProcessAsync(
        AnimeListItem item, PluginConfiguration? config, CancellationToken cancellationToken)
    {
        if (config is null || !IsStatusEnabled(config, item.Status))
        {
            const string detail = "status disabled in configuration";
            return new SyncItem(
                item.Title, item.Status, SyncOutcome.Skipped, detail, item.Url, null,
                ErrorHints.ForDetail(SyncOutcome.Skipped, detail));
        }

        try
        {
            var media = await _metadata.ResolveAsync(item, cancellationToken).ConfigureAwait(false);
            if (media is null)
            {
                const string detail = "unrecognized title";
                return new SyncItem(
                    item.Title, item.Status, SyncOutcome.Skipped, detail, item.Url, null,
                    ErrorHints.ForDetail(SyncOutcome.Skipped, detail));
            }

            var aniListUrl = "https://anilist.co/anime/" + media.AniListId;

            // Lazy.Value with bad Arr configuration throws a readable exception → Failed (caught below).
            var arr = SelectArr(media.Type, config);
            if (arr is null)
            {
                const string detail = "Sonarr/Radarr disabled";
                return new SyncItem(
                    item.Title, item.Status, SyncOutcome.Skipped, detail, item.Url, aniListUrl,
                    ErrorHints.ForDetail(SyncOutcome.Skipped, detail));
            }

            if (await arr.ExistsAsync(media, cancellationToken).ConfigureAwait(false))
            {
                return new SyncItem(item.Title, item.Status, SyncOutcome.AlreadyExists, null, item.Url, aniListUrl);
            }

            if (config.DryRun)
            {
                _logger.LogInformation(
                    "AniBridge (dry run): would add {Title} to {Arr}.", item.Title, arr.Name);
                return new SyncItem(item.Title, item.Status, SyncOutcome.WouldAdd, arr.Name, item.Url, aniListUrl);
            }

            await arr.AddAsync(media, item.Status, cancellationToken).ConfigureAwait(false);
            return new SyncItem(item.Title, item.Status, SyncOutcome.Added, null, item.Url, aniListUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AniBridge: sync error {Title}.", item.Title);
            return new SyncItem(
                item.Title, item.Status, SyncOutcome.Failed, ex.Message, item.Url, null,
                ErrorHints.ForDetail(SyncOutcome.Failed, ex.Message));
        }
    }

    private IArrClient? SelectArr(MediaType type, PluginConfiguration config) => type switch
    {
        MediaType.Tv when config.SonarrEnabled => _sonarr.Value,
        MediaType.Movie when config.RadarrEnabled => _radarr.Value,
        _ => null,
    };

    private static bool IsStatusEnabled(PluginConfiguration config, AnimeStatus status) => status switch
    {
        AnimeStatus.Planned => config.SyncPlanned,
        AnimeStatus.Watching => config.SyncWatching,
        AnimeStatus.Completed => config.SyncCompleted,
        AnimeStatus.Dropped => config.SyncDropped,
        AnimeStatus.OnHold => config.SyncOnHold,
        _ => false,
    };
}
