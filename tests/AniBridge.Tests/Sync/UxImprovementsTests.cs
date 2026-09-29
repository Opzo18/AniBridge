using AniBridge.Arr.Radarr;
using AniBridge.Arr.Sonarr;
using AniBridge.Controllers;
using AniBridge.Metadata;
using AniBridge.Providers;
using AniBridge.Providers.Models;
using AniBridge.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Sync;

public class UxImprovementsTests
{
    [Theory]
    [InlineData(SyncOutcome.Failed, "401 Unauthorized", "API key")]
    [InlineData(SyncOutcome.Failed, "Connection refused", "Unreachable")]
    [InlineData(SyncOutcome.Skipped, "unrecognized title", "AniList")]
    [InlineData(SyncOutcome.Skipped, "Sonarr/Radarr disabled", "Enable Sonarr")]
    [InlineData(SyncOutcome.Skipped, "status disabled in configuration", "unchecked")]
    public void ErrorHints_MapsToActionableText(SyncOutcome outcome, string detail, string expectedFragment)
    {
        var hint = ErrorHints.ForDetail(outcome, detail);
        Assert.NotNull(hint);
        Assert.Contains(expectedFragment, hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorHints_UnknownDetail_ReturnsNull()
    {
        Assert.Null(ErrorHints.ForDetail(SyncOutcome.Added, null));
    }

    [Fact]
    public void FileStore_History_KeepsNewestFirstCapped()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new FileSyncReportStore(dir, NullLogger<FileSyncReportStore>.Instance);

        for (var i = 0; i < 12; i++)
        {
            store.Save(new SyncReport
            {
                FinishedAt = DateTimeOffset.UtcNow.AddMinutes(i),
                Result = new SyncResult
                {
                    Items = [new SyncItem("T" + i, AnimeStatus.Watching, SyncOutcome.Added, null)],
                },
            });
        }

        var history = store.LoadHistory();
        Assert.Equal(FileSyncReportStore.MaxHistory, history.Count);
        Assert.Equal("T11", history[0].Result.Items[0].Title);
        // last-sync file still points at the newest report
        Assert.Equal("T11", store.Load()!.Result.Items[0].Title);
    }

    [Fact]
    public async Task ArrTest_EmptyRequest_ReturnsHelpfulError()
    {
        var controller = new ArrOptionsController();
        var result = await controller.TestSonarr(
            new ArrTestRequest("", ""), CancellationToken.None);
        var options = Assert.IsType<ArrOptions>(Assert.IsType<ActionResult<ArrOptions>>(result).Value);
        Assert.NotNull(options.Error);
        Assert.Contains("URL", options.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShindenTest_EmptyCredentials_ReturnsNotOk()
    {
        var controller = new ShindenTestController(
            NullLogger<global::AniBridge.Providers.Shinden.ShindenClient>.Instance,
            NullLogger<global::AniBridge.Providers.Shinden.ShindenListService>.Instance);
        var result = await controller.Test(
            new ShindenTestRequest("", "", ""), CancellationToken.None);
        var value = Assert.IsType<ShindenTestResult>(Assert.IsType<ActionResult<ShindenTestResult>>(result).Value);
        Assert.False(value.Ok);
    }

    [Fact]
    public void SyncItem_BackwardCompat_OldCtorStillWorks()
    {
        var item = new SyncItem("X", AnimeStatus.Watching, SyncOutcome.Added, null);
        Assert.Null(item.SourceUrl);
        Assert.Null(item.Hint);
    }

    [Fact]
    public void SaveProgress_UpdatesFileWithoutTouchingHistory()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new FileSyncReportStore(dir, NullLogger<FileSyncReportStore>.Instance);

        store.Save(new SyncReport { FinishedAt = DateTimeOffset.UtcNow });
        Assert.Single(store.LoadHistory());

        store.SaveProgress(new SyncReport
        {
            FinishedAt = DateTimeOffset.UtcNow,
            InProgress = true,
            Result = new SyncResult
            {
                Items = [new SyncItem("Live", AnimeStatus.Watching, SyncOutcome.WouldAdd, "Sonarr")],
            },
        });

        // Last-sync file shows the interim snapshot, history still has just the final one.
        Assert.True(store.Load()!.InProgress);
        Assert.Equal("Live", store.Load()!.Result.Items[0].Title);
        Assert.Single(store.LoadHistory());
        Assert.False(store.LoadHistory()[0].InProgress);
    }

    [Fact]
    public void OldReportJson_WithoutInProgress_ReadsAsFinal()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, FileSyncReportStore.FileName),
            """{"FinishedAt":"2026-09-28T20:00:00Z","DryRun":false,"Result":{"Items":[]}}""");
        var store = new FileSyncReportStore(dir, NullLogger<FileSyncReportStore>.Instance);

        Assert.False(store.Load()!.InProgress);
    }

    [Fact]
    public async Task SyncService_FiltersDisabledStatusesBeforeProcessing()
    {
        var metadata = new CountingMetadata();
        var finals = new List<SyncReport>();
        var service = new SyncService(
            new FixedListProvider(
            [
                new AnimeListItem("S", "1", "W", "http://x/1", AnimeStatus.Watching, 0, null),
                new AnimeListItem("S", "2", "D", "http://x/2", AnimeStatus.Dropped, 0, null),
                new AnimeListItem("S", "3", "C", "http://x/3", AnimeStatus.Completed, 0, null),
            ]),
            metadata,
            new Lazy<SonarrClient>(() => throw new InvalidOperationException("must not be used")),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")),
            NullLogger<SyncService>.Instance,
            () => new Configuration.PluginConfiguration { DryRun = true },
            TimeSpan.Zero,
            new ProgressCapturingStore(new List<SyncReport>(), finals));

        var result = await service.RunAsync();

        // Dropped is off by default: never resolved, never counted, never reported.
        Assert.Equal(2, metadata.Calls);
        Assert.Equal(2, result.Scanned);
        Assert.DoesNotContain(result.Items, i => i.Title == "D");
        var final = Assert.Single(finals);
        Assert.Equal("Watching, Planned, Completed, On hold", final.Scope);
    }

    [Fact]
    public async Task SyncService_WritesProgressSnapshotsDuringRun()
    {
        var progress = new List<SyncReport>();
        var finals = new List<SyncReport>();
        var service = new SyncService(
            new ManyProvider(30),
            new NullMetadata(),
            new Lazy<SonarrClient>(() => throw new InvalidOperationException("must not be used")),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")),
            NullLogger<SyncService>.Instance,
            () => new Configuration.PluginConfiguration { DryRun = true },
            TimeSpan.Zero,
            new ProgressCapturingStore(progress, finals));

        var result = await service.RunAsync();

        Assert.Equal(30, result.Scanned);
        // Progress at item 25 and at the final item 30; then one final Save.
        Assert.Equal(2, progress.Count);
        Assert.All(progress, p => Assert.True(p.InProgress));
        Assert.Equal(25, progress[0].Result.Scanned);
        var final = Assert.Single(finals);
        Assert.False(final.InProgress);
        Assert.Equal(30, final.Result.Scanned);
    }

    private sealed class ProgressCapturingStore(List<SyncReport> progress, List<SyncReport> finals)
        : ISyncReportStore
    {
        public void Save(SyncReport report) => finals.Add(report);

        public void SaveProgress(SyncReport report) => progress.Add(report);

        public SyncReport? Load() => finals.LastOrDefault();
    }

    private sealed class ManyProvider(int count) : IAnimeProvider
    {
        public string Name => "Many";

        public Task<IReadOnlyList<AnimeListItem>> GetListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimeListItem>>(
                Enumerable.Range(0, count)
                    .Select(i => new AnimeListItem("Many", i.ToString(), "T" + i, "http://x/" + i, AnimeStatus.Watching, 0, null))
                    .ToList());
    }

    private sealed class NullMetadata : IMetadataProvider
    {
        public string Name => "Null";

        public Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult<ResolvedMedia?>(null);
    }

    private sealed class FixedListProvider(IReadOnlyList<AnimeListItem> items) : IAnimeProvider
    {
        public string Name => "Fixed";

        public Task<IReadOnlyList<AnimeListItem>> GetListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(items);
    }

    private sealed class CountingMetadata : IMetadataProvider
    {
        public string Name => "Counting";

        public int Calls { get; private set; }

        public Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<ResolvedMedia?>(null);
        }
    }

    [Fact]
    public async Task SyncService_MissDiagnostics_AppendedToDetail()
    {
        var service = new SyncService(
            new ManyProvider(1),
            new MissMetadata(),
            new Lazy<SonarrClient>(() => throw new InvalidOperationException("must not be used")),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")),
            NullLogger<SyncService>.Instance,
            () => new Configuration.PluginConfiguration { DryRun = true },
            TimeSpan.Zero,
            new ProgressCapturingStore(new List<SyncReport>(), new List<SyncReport>()));

        var result = await service.RunAsync();

        var item = Assert.Single(result.Items);
        Assert.Equal(SyncOutcome.Skipped, item.Outcome);
        Assert.StartsWith("unrecognized title", item.Detail, StringComparison.Ordinal);
        Assert.Contains("closest:", item.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MissMetadata : IMetadataProvider
    {
        public string Name => "Miss";

        public Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult<ResolvedMedia?>(null);

        public string? DescribeLastMiss() => "closest: 'Some Other Show'";
    }
}
