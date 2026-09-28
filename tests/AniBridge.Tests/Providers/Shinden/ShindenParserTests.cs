using AniBridge.Providers.Shinden;
using Xunit;

namespace AniBridge.Tests.Providers.Shinden;

/// <summary>
/// ShindenParser tests on the real /animelist/{user}/all page HTML (24 entries, 3 statuses).
/// </summary>
public class ShindenParserTests
{
    private const string AllFixture = "Providers/Shinden/Fixtures/shinden_animelist_all.html";

    private static Task<string> LoadFixtureAsync() =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, AllFixture));

    [Fact]
    public async Task ParseAnimeList_AllPage_ReturnsAllEntries()
    {
        var entries = await ShindenParser.ParseAnimeListAsync(await LoadFixtureAsync());

        Assert.Equal(24, entries.Count);
        Assert.Equal(3, entries.Select(e => e.StatusText).Distinct().Count());
    }

    [Fact]
    public async Task ParseAnimeList_FirstEntry_HasExpectedFields()
    {
        var entries = await ShindenParser.ParseAnimeListAsync(await LoadFixtureAsync());

        var first = entries[0];
        Assert.Equal(59480, first.ShindenId);
        Assert.Equal("Noumin Kanren no Skill Bakka Agetetara Naze ka Tsuyoku Natta.", first.Title);
        Assert.Equal("/series/59480-noumin-kanren-no-skill-bakka-agetetara-naze-ka-tsuyoku-natta", first.Url);
        Assert.Equal("Oglądam", first.StatusText);
        Assert.Equal(0, first.WatchedEpisodes);
        Assert.Equal(12, first.TotalEpisodes);
        Assert.Equal("TV", first.TypeText);
    }

    [Fact]
    public async Task ParseAnimeList_CompletedEntries_HaveFullProgress()
    {
        var entries = await ShindenParser.ParseAnimeListAsync(await LoadFixtureAsync());

        var done = entries.Where(e => e.StatusText == "Obejrzane").ToList();
        Assert.NotEmpty(done);
        Assert.All(done, e =>
        {
            Assert.NotNull(e.TotalEpisodes);
            Assert.Equal(e.TotalEpisodes, e.WatchedEpisodes);
        });
    }

    [Fact]
    public async Task ParseAnimeList_EmptyHtml_ReturnsEmpty()
    {
        Assert.Empty(await ShindenParser.ParseAnimeListAsync("<html><body></body></html>"));
    }

    [Fact]
    public async Task ParseAnimeList_RowWithoutLink_IsSkipped()
    {
        const string Html = """
            <html><body><table class="title-list"><tbody>
            <tr class="title-row" data-title-id="1"><td>1</td><td class="title-col">bez linku</td><td></td><td>Oglądam</td><td>1/12</td><td>TV</td></tr>
            <tr class="title-row" data-title-id="2"><td>2</td><td class="title-col"><a href="/series/2-x">X</a></td><td></td><td>Planuję</td><td>0/24</td><td>TV</td></tr>
            </tbody></table></body></html>
            """;

        var entries = await ShindenParser.ParseAnimeListAsync(Html);

        var single = Assert.Single(entries);
        Assert.Equal(2, single.ShindenId);
        Assert.Equal("X", single.Title);
        Assert.Equal("Planuję", single.StatusText);
    }
}
