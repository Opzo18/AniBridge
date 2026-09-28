using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace AniBridge.Providers.Shinden;

/// <summary>
/// HTTP client for Shinden.pl. Responsible only for transport and session
/// (cookies, login). HTML parsing belongs to ShindenParser (Stage 3).
/// </summary>
public sealed class ShindenClient : IDisposable
{
    public const string BaseUrl = "https://shinden.pl";

    private const string LoginPagePath = "main/login";
    private const string LoginPostPath = "main/0/login";
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";

    private readonly HttpClient _http;
    private readonly ILogger<ShindenClient> _logger;
    private bool _disposed;

    public ShindenClient(HttpClient http, ILogger<ShindenClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Whether the last login succeeded (not verified on every request).
    /// </summary>
    public bool IsLoggedIn { get; private set; }

    public string? LoggedInUser { get; private set; }

    /// <summary>
    /// Production factory: HttpClient with CookieContainer (required by
    /// the anti-bot redirect 307 → ?r307=1) and browser headers.
    /// Without a User-Agent Shinden responds with 467.
    /// </summary>
    public static ShindenClient CreateDefault(ILogger<ShindenClient> logger)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl + "/") };
        ApplyBrowserHeaders(http);
        return new ShindenClient(http, logger);
    }

    public static void ApplyBrowserHeaders(HttpClient http)
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pl-PL,pl;q=0.9,en;q=0.8");
    }

    /// <summary>
    /// Logs in to Shinden. Returns true on success, false on invalid
    /// credentials or empty configuration. Throws HttpRequestException on network issues.
    /// </summary>
    public async Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        IsLoggedIn = false;
        LoggedInUser = null;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("Shinden login skipped: missing username or password in configuration.");
            return false;
        }

        // 1. GET the login page — sets the _rnd / sess_shinden cookies.
        using var loginPage = await _http.GetAsync(LoginPagePath, cancellationToken).ConfigureAwait(false);
        loginPage.EnsureSuccessStatusCode();

        // 2. POST the #login_form form.
        using var post = new HttpRequestMessage(HttpMethod.Post, LoginPostPath);
        post.Headers.Referrer = new Uri(BaseUrl + "/main/login");
        post.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["login"] = string.Empty,
        });
        using var resp = await _http.SendAsync(post, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var parser = new HtmlParser();
        var doc = await parser.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        var errors = doc.QuerySelectorAll("li.message.error")
            .Select(e => e.TextContent.Trim())
            .Where(t => t.Length > 0)
            .ToList();
        if (errors.Count > 0)
        {
            // Deliberately log only Shinden messages, never credentials.
            foreach (var error in errors)
            {
                _logger.LogWarning("Shinden: login failed: {Error}", error);
            }

            return false;
        }

        // 3. Session verification: a logged-in page does not render the login form.
        // NOTE: logged-in page markers to be confirmed with a real account in Stage 3.
        var home = await GetStringAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        var homeDoc = await parser.ParseDocumentAsync(home, cancellationToken).ConfigureAwait(false);
        if (homeDoc.QuerySelector("#login_form") is null
            || homeDoc.QuerySelector("a[href*=\"logout\"]") is not null)
        {
            IsLoggedIn = true;
            LoggedInUser = username;
            _logger.LogInformation("Logged in to Shinden as {User}.", username);
            return true;
        }

        _logger.LogWarning("Shinden: no login errors, but the home page looks logged out.");
        return false;
    }

    /// <summary>
    /// Session-scoped GET. Consumed by ShindenParser in Stage 3.
    /// </summary>
    public async Task<string> GetStringAsync(string relativeUrl, CancellationToken cancellationToken = default)
    {
        using var resp = await _http.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _http.Dispose();
            _disposed = true;
        }
    }
}
