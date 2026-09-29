using AniBridge.Arr.Radarr;
using AniBridge.Arr.Sonarr;
using AniBridge.Metadata;
using AniBridge.Metadata.AniList;
using AniBridge.Providers;
using AniBridge.Providers.Shinden;
using AniBridge.Sync;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AniBridge;

/// <summary>
/// Registers plugin services in the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ShindenClient>(sp =>
            ShindenClient.CreateDefault(sp.GetRequiredService<ILogger<ShindenClient>>()));
        serviceCollection.AddSingleton<ShindenListService>();
        serviceCollection.AddSingleton<IAnimeProvider, ShindenProvider>();
        serviceCollection.AddSingleton<AniListClient>(sp =>
            AniListClient.CreateDefault(sp.GetRequiredService<ILogger<AniListClient>>()));
        serviceCollection.AddSingleton<IMetadataProvider>(sp => new AniListMetadataProvider(
            sp.GetRequiredService<AniListClient>(),
            sp.GetRequiredService<ILogger<AniListMetadataProvider>>(),
            async (item, ct) => await sp.GetRequiredService<ShindenListService>()
                .GetTitleAliasesAsync(item.Url, ct).ConfigureAwait(false)));
        serviceCollection.AddSingleton<SonarrClient>(sp =>
        {
            var config = Plugin.Instance?.Configuration;
            return SonarrClient.CreateDefault(
                sp.GetRequiredService<ILogger<SonarrClient>>(),
                config?.SonarrUrl ?? string.Empty,
                config?.SonarrApiKey ?? string.Empty,
                config?.SonarrQualityProfileId ?? 1,
                config?.SonarrRootFolder ?? string.Empty,
                config?.SonarrMonitor ?? "all",
                config?.SonarrSeriesType ?? "anime",
                config?.SonarrSeasonFolder ?? true);
        });
        serviceCollection.AddSingleton<RadarrClient>(sp =>
        {
            var config = Plugin.Instance?.Configuration;
            return RadarrClient.CreateDefault(
                sp.GetRequiredService<ILogger<RadarrClient>>(),
                config?.RadarrUrl ?? string.Empty,
                config?.RadarrApiKey ?? string.Empty,
                config?.RadarrQualityProfileId ?? 1,
                config?.RadarrRootFolder ?? string.Empty,
                config?.RadarrMonitor ?? "movieOnly",
                config?.RadarrAvailability ?? "released");
        });
        serviceCollection.AddSingleton(sp =>
            new Lazy<SonarrClient>(sp.GetRequiredService<SonarrClient>));
        serviceCollection.AddSingleton(sp =>
            new Lazy<RadarrClient>(sp.GetRequiredService<RadarrClient>));
        serviceCollection.AddSingleton<ISyncReportStore>(sp => new FileSyncReportStore(
            Path.Combine(
                Plugin.Paths?.DataPath ?? Path.GetTempPath(),
                "anibridge"),
            sp.GetRequiredService<ILogger<FileSyncReportStore>>()));
        serviceCollection.AddSingleton<SyncService>(sp => new SyncService(
            sp.GetRequiredService<IAnimeProvider>(),
            sp.GetRequiredService<IMetadataProvider>(),
            sp.GetRequiredService<Lazy<SonarrClient>>(),
            sp.GetRequiredService<Lazy<RadarrClient>>(),
            sp.GetRequiredService<ILogger<SyncService>>(),
            () => Plugin.Instance?.Configuration,
            null,
            sp.GetRequiredService<ISyncReportStore>()));
    }
}
