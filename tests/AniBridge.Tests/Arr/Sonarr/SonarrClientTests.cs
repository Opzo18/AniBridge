using System.Net;
using System.Text;
using AniBridge.Arr.Sonarr;
using AniBridge.Metadata;
using AniBridge.Providers.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Arr.Sonarr;

public class SonarrClientTests
{
    private const string LookupJson = """
        [
          {"id":0,"title":"Suntory Minami Alps no Tennen Mizu","tvdbId":111,"alternateTitles":[]},
          {"id":0,"title":"Kimi no Na wa.","tvdbId":314095,"alternateTitles":[{"title":"Your Name."}]}
        ]
        """;

    private static ResolvedMedia Media(string title) =>
        new(title, MediaType.Tv, 21519, null, 2016, 1);

    private static SonarrClient CreateClient(QueueHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8989/api/v3/") };
        http.DefaultRequestHeaders.Add("X-Api-Key", "testkey");
        return new SonarrClient(http, NullLogger<SonarrClient>.Instance, 1, "/tv");
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ExistsAsync_SeriesPresent_ReturnsTrue()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson)); // lookup → tvdb 314095
        handler.Enqueue(Json("""[{"id":5,"title":"Kimi no Na wa.","tvdbId":314095}]"""));

        Assert.True(await CreateClient(handler).ExistsAsync(Media("Your Name")));
    }

    [Fact]
    public async Task ExistsAsync_SeriesAbsent_ReturnsFalse()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson));
        handler.Enqueue(Json("[]"));

        Assert.False(await CreateClient(handler).ExistsAsync(Media("Your Name")));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ExistsAsync_NoExactMatch_ReturnsFalseWithoutSecondRequest()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson));

        Assert.False(await CreateClient(handler).ExistsAsync(Media("Some Totally Different Title")));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AddAsync_PostsExpectedPayload()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson)); // lookup
        handler.Enqueue(Json("""{"id":5,"title":"Kimi no Na wa.","tvdbId":314095}""")); // POST ответ
        var client = CreateClient(handler);

        await client.AddAsync(Media("Your Name"), AnimeStatus.Watching);

        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal("http://localhost:8989/api/v3/series", post.RequestUri!.ToString());
        Assert.Equal("testkey", string.Join(",", post.Headers.GetValues("X-Api-Key")));
        var body = handler.Bodies.Single(b => b.Length > 0);
        Assert.Contains("\"tvdbId\":314095", body);
        Assert.Contains("\"monitored\":true", body);
        Assert.Contains("\"monitor\":\"all\"", body);
        Assert.Contains("\"searchForMissingEpisodes\":true", body);
        Assert.Contains("\"qualityProfileId\":1", body);
        Assert.Contains("/tv", body);
    }

    [Fact]
    public async Task AddAsync_NoExactMatch_SkipsPost()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson));

        await CreateClient(handler).AddAsync(Media("Some Totally Different Title"), AnimeStatus.Watching);

        Assert.Single(handler.Requests); // lookup only, no POST
    }

    [Theory]
    [InlineData(AnimeStatus.Watching, true, "all", true)]
    [InlineData(AnimeStatus.Planned, true, "future", false)]
    [InlineData(AnimeStatus.OnHold, true, "none", false)]
    [InlineData(AnimeStatus.Completed, false, "none", false)]
    [InlineData(AnimeStatus.Dropped, false, "none", false)]
    public void MapMonitoring_MapsCorrectly(
        AnimeStatus status, bool monitored, string monitor, bool search)
    {
        Assert.Equal((monitored, monitor, search), SonarrClient.MapMonitoring(status));
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = new();

        public List<string> Bodies { get; } = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
