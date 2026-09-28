using AniBridge.Providers.Shinden;
using Xunit;

namespace AniBridge.Tests.Providers.Shinden;

public class ShindenListServiceTests
{
    [Theory]
    [InlineData("420984-opzo", "420984-opzo")]
    [InlineData("624951", "624951")]
    [InlineData("  420984-opzo  ", "420984-opzo")]
    [InlineData("https://shinden.pl/animelist/420984-opzo", "420984-opzo")]
    [InlineData("https://lista.shinden.pl/animelist/420984-opzo", "420984-opzo")]
    [InlineData("https://shinden.pl/animelist/420984-opzo/all", "420984-opzo")]
    [InlineData("shinden.pl/animelist/624951-sebas766/", "624951-sebas766")]
    public void NormalizeUserId_AcceptsIdsAndUrls(string input, string expected)
    {
        Assert.Equal(expected, ShindenListService.NormalizeUserId(input));
    }
}
