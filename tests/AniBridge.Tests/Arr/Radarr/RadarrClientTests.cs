using System.Net;
using System.Text;
using AniBridge.Arr.Radarr;
using AniBridge.Metadata;
using AniBridge.Providers.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Arr.Radarr;

public class RadarrClientTests
{
    private const string LookupJson = """
        [
          {"id":0,"title":"Some Other Film","tmdbId":1,"year":2020,"alternateTitles":[]},
          {"id":0,"title":"Kimi no Na wa.","tmdbId":372058,"year":2016,"alternateTitles":[{"title":"Your Name."}]}
        ]
        """;

    private static ResolvedMedia Media(string title) =>
        new(title, MediaType.Movie, 21519, null, 2016, 1);

    private static RadarrClient CreateClient(QueueHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7878/api/v3/") };
        http.DefaultRequestHeaders.Add("X-Api-Key", "testkey");
        return new RadarrClient(http, NullLogger<RadarrClient>.Instance, 1, "/movies");
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ExistsAsync_MoviePresent_ReturnsTrue()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson)); // lookup → tmdb 372058
        handler.Enqueue(Json("""[{"id":7,"title":"Kimi no Na wa.","tmdbId":372058,"year":2016}]"""));

        Assert.True(await CreateClient(handler).ExistsAsync(Media("Your Name")));
    }

    [Fact]
    public async Task ExistsAsync_MovieAbsent_ReturnsFalse()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson));
        handler.Enqueue(Json("[]"));

        Assert.False(await CreateClient(handler).ExistsAsync(Media("Your Name")));
    }

    [Fact]
    public async Task AddAsync_PostsExpectedPayload()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson));
        handler.Enqueue(Json("""{"id":7,"title":"Kimi no Na wa.","tmdbId":372058,"year":2016}"""));
        var client = CreateClient(handler);

        await client.AddAsync(Media("Your Name"), AnimeStatus.Planned);

        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal("http://localhost:7878/api/v3/movie", post.RequestUri!.ToString());
        var body = handler.Bodies.Single(b => b.Length > 0);
        Assert.Contains("\"tmdbId\":372058", body);
        Assert.Contains("\"monitored\":true", body);
        Assert.Contains("\"monitor\":\"movieOnly\"", body);
        Assert.Contains("\"searchForMovie\":false", body);
        Assert.Contains("\"minimumAvailability\":\"released\"", body);
        Assert.Contains("/movies", body);
    }

    [Fact]
    public async Task AddAsync_PostsConfiguredMonitorAndAvailability()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson));
        handler.Enqueue(Json("""{"id":7,"title":"Kimi no Na wa.","tmdbId":372058,"year":2016}"""));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7878/api/v3/") };
        var client = new RadarrClient(
            http, NullLogger<RadarrClient>.Instance, 1, "/movies", "none", "announced");

        await client.AddAsync(Media("Your Name"), AnimeStatus.Watching);

        var body = handler.Bodies.Single(b => b.Length > 0);
        Assert.Contains("\"monitor\":\"none\"", body);
        Assert.Contains("\"minimumAvailability\":\"announced\"", body);
        Assert.Contains("\"monitored\":true", body);
        Assert.Contains("\"searchForMovie\":true", body);
    }

    [Fact]
    public async Task AddAsync_NoExactMatch_SkipsPost()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(LookupJson));

        await CreateClient(handler).AddAsync(Media("Some Totally Different Title"), AnimeStatus.Watching);

        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(AnimeStatus.Watching, true, true)]
    [InlineData(AnimeStatus.Planned, true, false)]
    [InlineData(AnimeStatus.OnHold, true, false)]
    [InlineData(AnimeStatus.Completed, false, false)]
    [InlineData(AnimeStatus.Dropped, false, false)]
    public void MapMonitorFlags_MapsCorrectly(
        AnimeStatus status, bool monitored, bool search)
    {
        Assert.Equal(
            (monitored, search),
            RadarrClient.MapMonitorFlags(status));
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
