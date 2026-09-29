using AniBridge.Controllers;
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
}
