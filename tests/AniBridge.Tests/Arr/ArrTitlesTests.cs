using AniBridge.Arr;
using AniBridge.Metadata;
using Xunit;

namespace AniBridge.Tests.Arr;

public class ArrTitlesTests
{
    [Theory]
    [InlineData("Re:Zero kara Hajimeru Isekai Seikatsu 4th Season", "re zero kara hajimeru isekai seikatsu 4th season")]
    [InlineData("Kimi no Na wa.", "kimi no na wa")]
    [InlineData("Your Name.", "your name")]
    [InlineData("Frieren: Beyond Journey's End", "frieren beyond journeys end")]
    [InlineData("  Mushoku   Tensei III  ", "mushoku tensei iii")]
    public void Normalize_IgnoresCasePunctuationAndApostrophes(string input, string expected)
    {
        Assert.Equal(expected, ArrTitles.Normalize(input));
    }

    [Theory]
    [InlineData("MF Ghost Final Season", "MF Ghost")]
    [InlineData("Re:Zero kara Hajimeru Isekai Seikatsu 4th Season", "Re:Zero kara Hajimeru Isekai Seikatsu")]
    [InlineData("Tokidoki Bosotto Russia-go de Dereru Tonari no Alya-san Season 2", "Tokidoki Bosotto Russia-go de Dereru Tonari no Alya-san")]
    [InlineData("Sousou no Frieren 2nd Season", "Sousou no Frieren")]
    [InlineData("Mushoku Tensei III", "Mushoku Tensei")]
    [InlineData("Some Show Part 2", "Some Show")]
    public void StripSeason_RemovesSeasonSuffix(string input, string expected)
    {
        Assert.Equal(expected, ArrTitles.StripSeason(input));
    }

    [Theory]
    [InlineData("Black Torch")]
    [InlineData("Lv999 no Murabito")]
    [InlineData("AB")]
    public void StripSeason_WithoutSuffix_ReturnsNull(string input)
    {
        Assert.Null(ArrTitles.StripSeason(input));
    }

    [Fact]
    public void CandidateTitles_IncludesSynonymsAndSeasonBase_Deduped()
    {
        var media = new ResolvedMedia(
            "MF Ghost Final Season", MediaType.Tv, 1, null, 2026, 12, null,
            "MF Ghost Final Season", "MF Ghost Final Season",
            ["MF Ghost Final Season", "MF Ghost"]);

        var titles = ArrTitles.CandidateTitles(media);

        Assert.Contains("MF Ghost Final Season", titles);
        Assert.Contains("MF Ghost", titles);
        Assert.Equal(titles.Count, titles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
