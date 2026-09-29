using System.Net;
using System.Text;
using AniBridge.Arr.Radarr;
using AniBridge.Arr.Sonarr;
using AniBridge.Configuration;
using AniBridge.Metadata;
using AniBridge.Providers;
using AniBridge.Providers.Models;
using AniBridge.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Sync;

public class SyncServiceTests
{
    private static AnimeListItem Item(string title, AnimeStatus status = AnimeStatus.Watching) =>
        new("Shinden", title.GetHashCode().ToString(), title,
            "https://shinden.pl/series/1-x", status, 0, null);

    private static ResolvedMedia Tv(string title) => new(title, MediaType.Tv, 1, null, 2020, 12);

    private static ResolvedMedia Movie(string title) => new(title, MediaType.Movie, 2, null, 2016, 1);

    private static PluginConfiguration Config() => new()
    {
        SonarrEnabled = true,
        RadarrEnabled = true,
        DryRun = false, // dry run has its own test
    };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static SonarrClient Sonarr(QueueHandler h)
    {
        var http = new HttpClient(h) { BaseAddress = new Uri("http://sonarr:8989/api/v3/") };
        return new SonarrClient(http, NullLogger<SonarrClient>.Instance, 1, "/tv");
    }

    private static RadarrClient Radarr(QueueHandler h)
    {
        var http = new HttpClient(h) { BaseAddress = new Uri("http://radarr:7878/api/v3/") };
        return new RadarrClient(http, NullLogger<RadarrClient>.Instance, 1, "/movies");
    }

    private static SyncService Create(
        IAnimeProvider provider,
        IMetadataProvider metadata,
        PluginConfiguration config,
        Lazy<SonarrClient> sonarr,
        Lazy<RadarrClient> radarr) =>
        new(provider, metadata, sonarr, radarr,
            NullLogger<SyncService>.Instance, () => config, TimeSpan.Zero);

    [Fact]
    public async Task RunAsync_FullFlow_ReturnsExpectedCounts()
    {
        var provider = new FakeProvider(
            Item("New Series"), Item("Existing Series"), Item("New Movie"), Item("Unknown Title"));
        var metadata = new FakeMetadata(new Dictionary<string, ResolvedMedia?>
        {
            ["New Series"] = Tv("New Series"),
            ["Existing Series"] = Tv("Existing Series"),
            ["New Movie"] = Movie("New Movie"),
            ["Unknown Title"] = null,
        });

        var sonarrHandler = new QueueHandler();
        sonarrHandler.Enqueue(Json("""[{"id":0,"title":"New Series","tvdbId":101}]"""));
        sonarrHandler.Enqueue(Json("[]")); // nie istnieje
        sonarrHandler.Enqueue(Json("""[{"id":0,"title":"New Series","tvdbId":101}]""")); // lookup w AddAsync
        sonarrHandler.Enqueue(Json("""{"id":9}""")); // POST
        sonarrHandler.Enqueue(Json("""[{"id":0,"title":"Existing Series","tvdbId":102}]"""));
        sonarrHandler.Enqueue(Json("""[{"id":3,"title":"Existing Series","tvdbId":102}]""")); // istnieje

        var radarrHandler = new QueueHandler();
        radarrHandler.Enqueue(Json("""[{"id":0,"title":"New Movie","tmdbId":201,"year":2016}]"""));
        radarrHandler.Enqueue(Json("[]"));
        radarrHandler.Enqueue(Json("""[{"id":0,"title":"New Movie","tmdbId":201,"year":2016}]""")); // lookup w AddAsync
        radarrHandler.Enqueue(Json("""{"id":8}"""));

        var service = Create(provider, metadata, Config(),
            new Lazy<SonarrClient>(() => Sonarr(sonarrHandler)),
            new Lazy<RadarrClient>(() => Radarr(radarrHandler)));

        var result = await service.RunAsync();

        Assert.Equal("Scanned: 4, Added: 2, AlreadyExists: 1, Skipped: 1, Failed: 0, WouldAdd: 0", result.ToString());
    }

    [Fact]
    public async Task RunAsync_ArrDisabled_SkipsWithoutTouchingClient()
    {
        var provider = new FakeProvider(Item("Some Series"));
        var metadata = new FakeMetadata(new Dictionary<string, ResolvedMedia?>
        {
            ["Some Series"] = Tv("Some Series"),
        });
        var config = Config();
        config.SonarrEnabled = false;

        var service = Create(provider, metadata, config,
            new Lazy<SonarrClient>(() => throw new InvalidOperationException("must not be used")),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")));

        var result = await service.RunAsync();

        Assert.Equal(SyncOutcome.Skipped, Assert.Single(result.Items).Outcome);
    }

    [Fact]
    public async Task RunAsync_AddThrows_MarksFailedAndContinues()
    {
        var provider = new FakeProvider(Item("Broken"), Item("Good"));
        var metadata = new FakeMetadata(new Dictionary<string, ResolvedMedia?>
        {
            ["Broken"] = Tv("Broken"),
            ["Good"] = Tv("Good"),
        });

        var handler = new QueueHandler();
        handler.Enqueue(Json("""[{"id":0,"title":"Broken","tvdbId":301}]"""));
        handler.Enqueue(Json("[]"));
        handler.Enqueue(Json("""[{"id":0,"title":"Broken","tvdbId":301}]""")); // lookup w AddAsync
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError)); // POST 500
        handler.Enqueue(Json("""[{"id":0,"title":"Good","tvdbId":302}]"""));
        handler.Enqueue(Json("[]"));
        handler.Enqueue(Json("""[{"id":0,"title":"Good","tvdbId":302}]""")); // lookup w AddAsync
        handler.Enqueue(Json("""{"id":10}"""));

        var service = Create(provider, metadata, Config(),
            new Lazy<SonarrClient>(() => Sonarr(handler)),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")));

        var result = await service.RunAsync();

        Assert.Equal("Scanned: 2, Added: 1, AlreadyExists: 0, Skipped: 0, Failed: 1, WouldAdd: 0", result.ToString());
        Assert.Equal("Broken", result.Items.First(i => i.Outcome == SyncOutcome.Failed).Title);
    }

    [Fact]
    public async Task RunAsync_DroppedStatus_FilteredOutByDefault()
    {
        var provider = new FakeProvider(Item("Dropped Show", AnimeStatus.Dropped));
        var metadata = new FakeMetadata(new Dictionary<string, ResolvedMedia?>());

        var service = Create(provider, metadata, Config(),
            new Lazy<SonarrClient>(() => throw new InvalidOperationException("must not be used")),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")));

        var result = await service.RunAsync();

        // Disabled statuses never reach metadata/*Arr and are not counted at all.
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Scanned);
    }

    [Fact]
    public async Task RunAsync_DryRun_MarksWouldAddWithoutPost()
    {
        var provider = new FakeProvider(Item("Dry Series"));
        var metadata = new FakeMetadata(new Dictionary<string, ResolvedMedia?>
        {
            ["Dry Series"] = Tv("Dry Series"),
        });

        var handler = new QueueHandler();
        handler.Enqueue(Json("""[{"id":0,"title":"Dry Series","tvdbId":401}]"""));
        handler.Enqueue(Json("[]")); // not present
        // NOTE: no lookup + POST for AddAsync — dry run must stop here.

        var config = Config();
        config.DryRun = true;
        var service = Create(provider, metadata, config,
            new Lazy<SonarrClient>(() => Sonarr(handler)),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")));

        var result = await service.RunAsync();

        var item = Assert.Single(result.Items);
        Assert.Equal(SyncOutcome.WouldAdd, item.Outcome);
        Assert.Equal(1, result.WouldAdd);
        Assert.Equal(2, handler.Requests.Count); // lookup + exists check only
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task RunAsync_DryRun_IncludesMatchedAliasInDetail()
    {
        var provider = new FakeProvider(Item("Dogulwang"));
        var metadata = new FakeMetadata(new Dictionary<string, ResolvedMedia?>
        {
            ["Dogulwang"] = new ResolvedMedia("Dogulwang", MediaType.Tv, 187538, null, 2026, 12, "Tomb Raider King"),
        });

        var handler = new QueueHandler();
        handler.Enqueue(Json("""[{"id":0,"title":"Dogulwang","tvdbId":501}]"""));
        handler.Enqueue(Json("[]")); // not present

        var config = Config();
        config.DryRun = true;
        var service = Create(provider, metadata, config,
            new Lazy<SonarrClient>(() => Sonarr(handler)),
            new Lazy<RadarrClient>(() => throw new InvalidOperationException("must not be used")));

        var result = await service.RunAsync();

        var item = Assert.Single(result.Items);
        Assert.Equal(SyncOutcome.WouldAdd, item.Outcome);
        Assert.Equal("Sonarr (as 'Tomb Raider King')", item.Detail);
    }

    private sealed class FakeProvider(params AnimeListItem[] items) : IAnimeProvider
    {
        public string Name => "Fake";

        public Task<IReadOnlyList<AnimeListItem>> GetListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimeListItem>>(items);
    }

    private sealed class FakeMetadata(Dictionary<string, ResolvedMedia?> map) : IMetadataProvider
    {
        public string Name => "Fake";

        public Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult(map.TryGetValue(item.Title, out var media) ? media : null);
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
