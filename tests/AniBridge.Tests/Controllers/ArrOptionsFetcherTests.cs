using System.Net;
using System.Text;
using AniBridge.Controllers;
using Xunit;

namespace AniBridge.Tests.Controllers;

public class ArrOptionsFetcherTests
{
    private const string ProfilesJson = """
        [{"id":1,"name":"HD-1080p"},{"id":2,"name":"Any"}]
        """;

    private const string FoldersJson = """
        [{"path":"/tv","freeSpace":123},{"path":"/anime","freeSpace":456}]
        """;

    [Fact]
    public async Task FetchAsync_ParsesProfilesAndFolders()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(ProfilesJson));
        handler.Enqueue(Json(FoldersJson));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://arr:8989/api/v3/") };

        var options = await ArrOptionsFetcher.FetchAsync(http);

        Assert.Null(options.Error);
        Assert.Equal([(1, "HD-1080p"), (2, "Any")],
            options.QualityProfiles.Select(p => (p.Id, p.Name)));
        Assert.Equal(["/tv", "/anime"], options.RootFolders);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responses.Dequeue());
    }
}
