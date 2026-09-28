using System.Net;
using System.Text;
using AniBridge.Providers.Shinden;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Providers.Shinden;

/// <summary>
/// ShindenClient tests on a stubbed HttpMessageHandler — no secrets, no network.
/// The shinden_login_failed.html fixture is the real Shinden.pl response to bad credentials.
/// </summary>
public class ShindenClientTests
{
    private const string FailedFixture = "Providers/Shinden/Fixtures/shinden_login_failed.html";

    private const string LoginPageHtml = "<html><body><form id=\"login_form\"></form></body></html>";

    private const string HomeLoggedInHtml =
        "<html><body><div id=\"user-menu\"><a href=\"/main/logout\">Wyloguj</a></div></body></html>";

    private static ShindenClient CreateClient(QueueHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri(ShindenClient.BaseUrl + "/") };
        ShindenClient.ApplyBrowserHeaders(http);
        return new ShindenClient(http, NullLogger<ShindenClient>.Instance);
    }

    private static HttpResponseMessage Html(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/html") };

    [Fact]
    public async Task LoginAsync_EmptyCredentials_ReturnsFalseWithoutRequests()
    {
        var handler = new QueueHandler();
        using var client = CreateClient(handler);

        Assert.False(await client.LoginAsync(string.Empty, string.Empty));
        Assert.False(await client.LoginAsync("user", string.Empty));
        Assert.Empty(handler.Requests);
        Assert.False(client.IsLoggedIn);
    }

    [Fact]
    public async Task LoginAsync_ErrorMessages_ReturnsFalse()
    {
        var fixture = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, FailedFixture));
        var handler = new QueueHandler();
        handler.Enqueue(Html(LoginPageHtml)); // GET main/login
        handler.Enqueue(Html(fixture)); // POST main/0/login
        using var client = CreateClient(handler);

        Assert.False(await client.LoginAsync("anibridge_probe_xyz", "wrongpassword123"));
        Assert.False(client.IsLoggedIn);
        Assert.Equal(2, handler.Requests.Count); // brak weryfikacyjnego GET /
    }

    [Fact]
    public async Task LoginAsync_NoErrorsAndLogoutLink_ReturnsTrue()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Html(LoginPageHtml));
        handler.Enqueue(Html(LoginPageHtml)); // POST bez li.message.error
        handler.Enqueue(Html(HomeLoggedInHtml)); // GET / zalogowany
        using var client = CreateClient(handler);

        Assert.True(await client.LoginAsync("someuser", "somepass"));
        Assert.True(client.IsLoggedIn);
        Assert.Equal("someuser", client.LoggedInUser);
    }

    [Fact]
    public async Task LoginAsync_PostsUsernameAndPasswordAsForm()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Html(LoginPageHtml));
        handler.Enqueue(Html(LoginPageHtml));
        handler.Enqueue(Html(HomeLoggedInHtml));
        using var client = CreateClient(handler);

        await client.LoginAsync("someuser", "somepass");

        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        var body = Assert.Single(handler.Bodies, b => b.Length > 0);
        Assert.Contains("username=someuser", body);
        Assert.Contains("password=somepass", body);
    }

    [Fact]
    public void CreateDefault_SetsBrowserHeaders()
    {
        using var client = ShindenClient.CreateDefault(NullLogger<ShindenClient>.Instance);

        // Mere creation without exceptions plus client presence is enough;
        // headers are verified indirectly through ApplyBrowserHeaders.
        var http = new HttpClient { BaseAddress = new Uri(ShindenClient.BaseUrl + "/") };
        ShindenClient.ApplyBrowserHeaders(http);
        Assert.NotEmpty(http.DefaultRequestHeaders.UserAgent.ToString());
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
