using AniBridge.Providers.Models;
using AniBridge.Providers.Shinden;
using Xunit;

namespace AniBridge.Tests.Providers.Shinden;

public class ShindenProviderTests
{
    [Theory]
    [InlineData("Oglądam", AnimeStatus.Watching)]
    [InlineData("Obejrzane", AnimeStatus.Completed)]
    [InlineData("Planuję", AnimeStatus.Planned)]
    [InlineData("Wstrzymane", AnimeStatus.OnHold)]
    [InlineData("Porzucone", AnimeStatus.Dropped)]
    [InlineData("  Oglądam  ", AnimeStatus.Watching)]
    public void MapStatus_KnownStatuses_MapsCorrectly(string text, AnimeStatus expected)
    {
        Assert.Equal(expected, ShindenProvider.MapStatus(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Nieznany")]
    [InlineData("Ogladam")] // bez ogonka
    public void MapStatus_Unknown_ReturnsNull(string text)
    {
        Assert.Null(ShindenProvider.MapStatus(text));
    }

    [Fact]
    public void MapEntry_MapsAllFields()
    {
        var entry = new ShindenListEntry
        {
            ShindenId = 44158,
            Title = "Overlord",
            Url = "/series/44158-overlord",
            StatusText = "Obejrzane",
            WatchedEpisodes = 13,
            TotalEpisodes = 13,
            TypeText = "TV",
        };

        var item = ShindenProvider.MapEntry(entry);

        Assert.NotNull(item);
        Assert.Equal("Shinden", item.Provider);
        Assert.Equal("44158", item.ProviderId);
        Assert.Equal("Overlord", item.Title);
        Assert.Equal("https://shinden.pl/series/44158-overlord", item.Url);
        Assert.Equal(AnimeStatus.Completed, item.Status);
        Assert.Equal(13, item.WatchedEpisodes);
        Assert.Equal(13, item.TotalEpisodes);
    }

    [Fact]
    public void MapEntry_UnknownStatus_ReturnsNull()
    {
        var entry = new ShindenListEntry
        {
            ShindenId = 1,
            Title = "X",
            Url = "/series/1-x",
            StatusText = "Nieznany",
        };

        Assert.Null(ShindenProvider.MapEntry(entry));
    }
}
