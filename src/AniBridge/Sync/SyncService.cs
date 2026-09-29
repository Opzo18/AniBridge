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

    /// <summary>
    /// Interim progress snapshot every N processed entries (also on the final entry).
    /// </summary>
    private const int ProgressEvery = 25;

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

        // Statuses disabled in settings never reach metadata/*Arr: they are not
        // counted, paced, or reported per-title. The scope label lands in the report.
        var scope = ScopeLabel(config);
        var scoped = FilterByEnabledStatuses(items, config, out var ignored);
        if (ignored > 0)
        {
            _logger.LogInformation(
                "AniBridge: ignoring {Ignored} entries with disabled statuses (scope: {Scope}).",
                ignored, scope ?? "all");
        }

        var results = new List<SyncItem>(scoped.Count);
        for (var i = 0; i < scoped.Count; i++)
        {
            results.Add(await ProcessAsync(scoped[i], config, cancellationToken).ConfigureAwait(false));
            progress?.Report(scoped.Count == 0 ? 1 : (double)(i + 1) / scoped.Count);
            if ((i + 1) % ProgressEvery == 0 || i + 1 == scoped.Count)
            {
                SaveProgress(config, scope, results);
            }

            if (i + 1 < scoped.Count)
            {
                await Task.Delay(_pacing, cancellationToken).ConfigureAwait(false);
            }
        }

        var result = new SyncResult { Items = results };
        _logger.LogInformation("AniBridge: sync finished: {Result}", result);
        SaveReport(config, scope, result);
        return result;
    }

    private void SaveReport(PluginConfiguration? config, string? scope, SyncResult result)
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
                Scope = scope,
                Result = result,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AniBridge: could not save last sync report.");
        }
    }

    private void SaveProgress(PluginConfiguration? config, string? scope, List<SyncItem> partial)
    {
        if (_reportStore is null)
        {
            return;
        }

        try
        {
            _reportStore.SaveProgress(new SyncReport
            {
                FinishedAt = DateTimeOffset.UtcNow,
                DryRun = config?.DryRun ?? false,
                InProgress = true,
                Scope = scope,
                Result = new SyncResult { Items = partial.ToArray() },
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AniBridge: could not save sync progress snapshot.");
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

    private static List<AnimeListItem> FilterByEnabledStatuses(
        IReadOnlyList<AnimeListItem> items, PluginConfiguration? config, out int ignored)
    {
        ignored = 0;
        if (config is null)
        {
            return items.ToList();
        }

        var kept = new List<AnimeListItem>(items.Count);
        foreach (var item in items)
        {
            if (IsStatusEnabled(config, item.Status))
            {
                kept.Add(item);
            }
            else
            {
                ignored++;
            }
        }

        return kept;
    }

    private static string? ScopeLabel(PluginConfiguration? config)
    {
        if (config is null)
        {
            return null;
        }

        var enabled = new List<string>(5);
        if (config.SyncWatching) { enabled.Add("Watching"); }
        if (config.SyncPlanned) { enabled.Add("Planned"); }
        if (config.SyncCompleted) { enabled.Add("Completed"); }
        if (config.SyncOnHold) { enabled.Add("On hold"); }
        if (config.SyncDropped) { enabled.Add("Dropped"); }
        return enabled.Count == 0 ? "none" : string.Join(", ", enabled);
    }

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
