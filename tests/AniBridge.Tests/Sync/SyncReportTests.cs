using AniBridge.Arr.Radarr;
using AniBridge.Arr.Sonarr;
using AniBridge.Configuration;
using AniBridge.Controllers;
using AniBridge.Metadata;
using AniBridge.Providers;
using AniBridge.Providers.Models;
using AniBridge.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Sync;

public class SyncReportTests
{
    [Fact]
    public void FileStore_RoundTrip_PreservesReport()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new FileSyncReportStore(dir, NullLogger<FileSyncReportStore>.Instance);

        Assert.Null(store.Load());

        var report = new SyncReport
        {
            FinishedAt = DateTimeOffset.UtcNow,
            DryRun = true,
            Result = new SyncResult
            {
                Items = new[]
                {
                    new SyncItem("A", AnimeStatus.Watching, SyncOutcome.WouldAdd, "Sonarr"),
                    new SyncItem("B", AnimeStatus.Planned, SyncOutcome.Skipped, "unrecognized title"),
                },
            },
        };
        store.Save(report);

        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.True(loaded.DryRun);
        Assert.Equal("Scanned: 2, Added: 0, AlreadyExists: 0, Skipped: 1, Failed: 0, WouldAdd: 1",
            loaded.Result.ToString());
        Assert.Equal("B", loaded.Result.Items[1].Title);
    }

    [Fact]
    public void FileStore_CorruptFile_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, FileSyncReportStore.FileName), "not json{{{");
        var store = new FileSyncReportStore(dir, NullLogger<FileSyncReportStore>.Instance);

        Assert.Null(store.Load());
    }

    [Fact]
    public async Task SyncService_SavesReportWithDryRunFlag()
    {
        var captured = new List<SyncReport>();
        var service = new SyncService(
            new FakeProvider(),
            new FakeMetadata(),
            new Lazy<SonarrClient>(() => throw new InvalidOperationException("must not be used")),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")),
            NullLogger<SyncService>.Instance,
            () => new PluginConfiguration { DryRun = true },
            TimeSpan.Zero,
            new CapturingStore(captured));

        await service.RunAsync();

        var report = Assert.Single(captured);
        Assert.True(report.DryRun);
        Assert.Equal(1, report.Result.Scanned);
    }

    [Fact]
    public void ReportController_MissingReport_Returns404()
    {
        var controller = new SyncReportController(new CapturingStore(new List<SyncReport>()));

        var result = controller.GetLastSync();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void ReportController_WithReport_ReturnsIt()
    {
        var report = new SyncReport { DryRun = false };
        var controller = new SyncReportController(new FixedStore(report));

        var result = controller.GetLastSync();

        Assert.Same(report, result.Value);
    }

    private sealed class FakeProvider : IAnimeProvider
    {
        public string Name => "Fake";

        public Task<IReadOnlyList<AnimeListItem>> GetListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimeListItem>>(
                [new AnimeListItem("Fake", "1", "X", "http://x", AnimeStatus.Watching, 0, null)]);
    }

    private sealed class FakeMetadata : IMetadataProvider
    {
        public string Name => "Fake";

        public Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult<ResolvedMedia?>(null);
    }

    private sealed class CapturingStore(List<SyncReport> captured) : ISyncReportStore
    {
        public void Save(SyncReport report) => captured.Add(report);

        public SyncReport? Load() => captured.LastOrDefault();
    }

    private sealed class FixedStore(SyncReport report) : ISyncReportStore
    {
        public void Save(SyncReport report) => throw new NotSupportedException();

        public SyncReport? Load() => report;
    }
}
