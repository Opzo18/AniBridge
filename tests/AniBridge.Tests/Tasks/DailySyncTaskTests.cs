using AniBridge.Arr.Radarr;
using AniBridge.Arr.Sonarr;
using AniBridge.Configuration;
using AniBridge.Metadata;
using AniBridge.Providers;
using AniBridge.Providers.Models;
using AniBridge.Sync;
using AniBridge.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Tasks;

public class DailySyncTaskTests
{
    [Fact]
    public async Task ExecuteAsync_EmptyList_ReportsProgress()
    {
        var service = new SyncService(
            new FakeProvider(),
            new FakeMetadata(),
            new Lazy<SonarrClient>(() => throw new InvalidOperationException("must not be used")),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")),
            NullLogger<SyncService>.Instance,
            () => new PluginConfiguration(),
            TimeSpan.Zero);

        var task = new DailySyncTask(service);
        Assert.Equal("AniBridgeDailySync", task.Key);
        Assert.Single(task.GetDefaultTriggers());

        var progress = new CollectingProgress();
        await task.ExecuteAsync(progress, CancellationToken.None);

        Assert.Empty(progress.Values); // empty list → no partial reports, but no errors either
    }

    private sealed class FakeProvider : IAnimeProvider
    {
        public string Name => "Fake";

        public Task<IReadOnlyList<AnimeListItem>> GetListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimeListItem>>([]);
    }

    private sealed class FakeMetadata : IMetadataProvider
    {
        public string Name => "Fake";

        public Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult<ResolvedMedia?>(null);
    }

    private sealed class CollectingProgress : IProgress<double>
    {
        public List<double> Values { get; } = new();

        public void Report(double value) => Values.Add(value);
    }
}
