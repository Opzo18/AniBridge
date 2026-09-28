using System.Net;
using System.Text;
using AniBridge.Metadata;
using AniBridge.Metadata.AniList;
using AniBridge.Providers.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Metadata.AniList;

public class AniListMetadataProviderTests
{
    private const string FrierenJson = """
        {"data":{"Page":{"media":[
          {"id":154587,"title":{"romaji":"Sousou no Frieren","english":"Frieren: Beyond Journey’s End","native":"葬送のフリーレン"},"format":"TV","episodes":28,"startDate":{"year":2023}},
          {"id":182255,"title":{"romaji":"Sousou no Frieren 2nd Season","english":"Frieren: Beyond Journey’s End Season 2","native":"葬送のフリーレン 第2期"},"format":"TV","episodes":10,"startDate":{"year":2026}}
        ]}}}
        """;

    private const string YourNameJson = """
        {"data":{"Page":{"media":[
          {"id":97962,"title":{"romaji":"Suntory Minami Alps no Tennen Mizu","english":null,"native":"サントリー 南アルプスの天然水"},"format":"SPECIAL","episodes":3,"startDate":{"year":2021}},
          {"id":21519,"title":{"romaji":"Kimi no Na wa.","english":"Your Name.","native":"君の名は。"},"format":"MOVIE","episodes":1,"startDate":{"year":2016}}
        ]}}}
        """;

    private static AniListMetadataProvider CreateProvider(string json)
    {
        var handler = new SingleResponseHandler(json);
        var http = new HttpClient(handler) { BaseAddress = new Uri(AniListClient.Endpoint + "/") };
        var client = new AniListClient(http, NullLogger<AniListClient>.Instance);
        return new AniListMetadataProvider(client, NullLogger<AniListMetadataProvider>.Instance);
    }

    private static AnimeListItem Item(string title) =>
        new("Shinden", "1", title, "https://shinden.pl/series/1-x", AnimeStatus.Planned, 0, null);

    [Fact]
    public async Task ResolveAsync_ExactRomajiMatch_ReturnsTv()
    {
        var media = await CreateProvider(FrierenJson).ResolveAsync(Item("Sousou no Frieren"));

        Assert.NotNull(media);
        Assert.Equal(MediaType.Tv, media.Type);
        Assert.Equal(154587, media.AniListId);
        Assert.Equal(2023, media.Year);
        Assert.Equal(28, media.Episodes);
    }

    [Fact]
    public async Task ResolveAsync_EnglishMatchWithTrailingPeriod_ReturnsMovie()
    {
        // Shinden: "Your Name", AniList english: "Your Name." — normalizacja to spina,
        // a rozmyty pierwszy wynik (reklama Suntory) jest poprawnie odrzucony.
        var media = await CreateProvider(YourNameJson).ResolveAsync(Item("Your Name"));

        Assert.NotNull(media);
        Assert.Equal(MediaType.Movie, media.Type);
        Assert.Equal(21519, media.AniListId);
    }

    [Fact]
    public async Task ResolveAsync_InexactOnly_ReturnsNull()
    {
        var media = await CreateProvider(FrierenJson).ResolveAsync(Item("Frieren"));

        Assert.Null(media);
    }

    [Fact]
    public async Task ResolveAsync_NoCandidates_ReturnsNull()
    {
        var media = await CreateProvider("""{"data":{"Page":{"media":[]}}}""")
            .ResolveAsync(Item("No Such Title"));

        Assert.Null(media);
    }

    [Fact]
    public async Task ResolveAsync_UnknownFormat_ReturnsNull()
    {
        const string Json = """
            {"data":{"Page":{"media":[
              {"id":1,"title":{"romaji":"Some Clip","english":null,"native":null},"format":"MUSIC","episodes":1,"startDate":{"year":2020}}
            ]}}}
            """;

        Assert.Null(await CreateProvider(Json).ResolveAsync(Item("Some Clip")));
    }

    [Fact]
    public async Task ResolveAsync_YearSuffix_FallsBackToUniqueStrippedMatch()
    {
        const string Json = """
            {"data":{"Page":{"media":[
              {"id":21311,"title":{"romaji":"Bungou Stray Dogs","english":null,"native":null},"format":"TV","episodes":24,"startDate":{"year":2016}}
            ]}}}
            """;

        var media = await CreateProvider(Json).ResolveAsync(Item("Bungou Stray Dogs (2016)"));

        Assert.NotNull(media);
        Assert.Equal(21311, media.AniListId);
    }

    [Fact]
    public async Task ResolveAsync_YearSuffixWithTwoStrippedMatches_ReturnsNull()
    {
        const string Json = """
            {"data":{"Page":{"media":[
              {"id":1,"title":{"romaji":"Some Show","english":null,"native":null},"format":"TV","episodes":12,"startDate":{"year":2010}},
              {"id":2,"title":{"romaji":"Some Show (2014)","english":null,"native":null},"format":"TV","episodes":12,"startDate":{"year":2014}}
            ]}}}
            """;

        // Exact match wins for "Some Show (2014)"; for a year-less query both strip to one form.
        Assert.NotNull(await CreateProvider(Json).ResolveAsync(Item("Some Show (2014)")));
        Assert.Null(await CreateProvider(Json).ResolveAsync(Item("Some Show (2010)")));
    }

    [Theory]
    [InlineData("bungou stray dogs (2016)", "bungou stray dogs")]
    [InlineData("fairy tail", "fairy tail")]
    [InlineData("dog days'", "dog days'")]
    public void StripYear_StripsTrailingYearOnly(string input, string expected)
    {
        Assert.Equal(expected, AniListMetadataProvider.StripYear(input));
    }

    [Fact]
    public async Task SearchAnimeAsync_RetriesOnRateLimit_ThenSucceeds()
    {
        var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage((HttpStatusCode)429)); // no Retry-After → instant fallback in tests
        handler.Enqueue(new HttpResponseMessage((HttpStatusCode)429));
        handler.Enqueue(Json(FrierenJson));
        var http = new HttpClient(handler) { BaseAddress = new Uri(AniListClient.Endpoint + "/") };
        var client = new AniListClient(http, NullLogger<AniListClient>.Instance, TimeSpan.Zero);

        var provider = new AniListMetadataProvider(client, NullLogger<AniListMetadataProvider>.Instance);
        var media = await provider.ResolveAsync(Item("Sousou no Frieren"));

        Assert.NotNull(media);
        Assert.Equal(154587, media.AniListId);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task SearchAnimeAsync_PersistentRateLimit_ThrowsAfterMaxAttempts()
    {
        var handler = new QueueHandler();
        for (var i = 0; i < 5; i++)
        {
            handler.Enqueue(new HttpResponseMessage((HttpStatusCode)429));
        }

        var http = new HttpClient(handler) { BaseAddress = new Uri(AniListClient.Endpoint + "/") };
        var client = new AniListClient(http, NullLogger<AniListClient>.Instance, TimeSpan.Zero);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAnimeAsync("Frieren"));
        Assert.Equal(5, handler.Requests.Count);
    }

    [Theory]
    [InlineData("TV", MediaType.Tv)]
    [InlineData("TV_SHORT", MediaType.Tv)]
    [InlineData("OVA", MediaType.Tv)]
    [InlineData("ONA", MediaType.Tv)]
    [InlineData("SPECIAL", MediaType.Tv)]
    [InlineData("MOVIE", MediaType.Movie)]
    [InlineData("MUSIC", null)]
    [InlineData(null, null)]
    public void MapFormat_MapsCorrectly(string? format, MediaType? expected)
    {
        Assert.Equal(expected, AniListMetadataProvider.MapFormat(format));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class SingleResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
