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

        var task = new DailySyncTask(
            service,
            new NullStore(),
            NullLogger<DailySyncTask>.Instance);
        Assert.Equal("AniBridgeDailySync", task.Key);
        Assert.Single(task.GetDefaultTriggers());

        var progress = new CollectingProgress();
        await task.ExecuteAsync(progress, CancellationToken.None);

        Assert.Empty(progress.Values); // empty list → no partial reports, but no errors either
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_SavesErrorReportAndRethrows()
    {
        var service = new SyncService(
            new ThrowingProvider(),
            new FakeMetadata(),
            new Lazy<SonarrClient>(() => throw new InvalidOperationException("must not be used")),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")),
            NullLogger<SyncService>.Instance,
            () => new PluginConfiguration(),
            TimeSpan.Zero);

        var captured = new List<SyncReport>();
        var task = new DailySyncTask(
            service,
            new CapturingStore(captured),
            NullLogger<DailySyncTask>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ExecuteAsync(new CollectingProgress(), CancellationToken.None));

        var report = Assert.Single(captured);
        Assert.Equal("boom", report.Error);
        Assert.Equal(0, report.Result.Scanned);
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

    private sealed class ThrowingProvider : IAnimeProvider
    {
        public string Name => "Throwing";

        public Task<IReadOnlyList<AnimeListItem>> GetListAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class NullStore : ISyncReportStore
    {
        public void Save(SyncReport report)
        {
        }

        public SyncReport? Load() => null;
    }

    private sealed class CapturingStore(List<SyncReport> captured) : ISyncReportStore
    {
        public void Save(SyncReport report) => captured.Add(report);

        public SyncReport? Load() => captured.LastOrDefault();
    }
}
