using AniBridge.Arr.Radarr;
using AniBridge.Arr.Sonarr;
using AniBridge.Metadata;
using AniBridge.Metadata.AniList;
using AniBridge.Providers;
using AniBridge.Providers.Shinden;
using AniBridge.Sync;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AniBridge.Tests;

/// <summary>
/// Verifies DI registration completeness. applicationHost is unused,
/// so tests pass null.
/// </summary>
public class PluginServiceRegistratorTests
{
    [Fact]
    public void RegisterServices_RegistersAllServices()
    {
        var services = new ServiceCollection();
        new PluginServiceRegistrator().RegisterServices(services, null!);

        var types = services.Select(d => d.ServiceType).ToList();
        Assert.Contains(typeof(ShindenClient), types);
        Assert.Contains(typeof(ShindenListService), types);
        Assert.Contains(typeof(IAnimeProvider), types);
        Assert.Contains(typeof(AniListClient), types);
        Assert.Contains(typeof(IMetadataProvider), types);
        Assert.Contains(typeof(SonarrClient), types);
        Assert.Contains(typeof(RadarrClient), types);
        Assert.Contains(typeof(Lazy<SonarrClient>), types);
        Assert.Contains(typeof(Lazy<RadarrClient>), types);
        Assert.Contains(typeof(SyncService), types);
    }
}
