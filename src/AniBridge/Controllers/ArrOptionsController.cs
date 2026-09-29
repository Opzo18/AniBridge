using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AniBridge.Controllers;

/// <summary>
/// Settings-page helper: quality profiles and root folders from *Arr,
/// fetched server-side (browser never sees the API keys).
/// </summary>
[ApiController]
[Route("AniBridge")]
[Authorize]
public sealed class ArrOptionsController : ControllerBase
{
    [HttpGet("sonarr/options")]
    public Task<ActionResult<ArrOptions>> GetSonarrOptions(CancellationToken cancellationToken) =>
        GetOptionsAsync(isSonarr: true, cancellationToken);

    [HttpGet("radarr/options")]
    public Task<ActionResult<ArrOptions>> GetRadarrOptions(CancellationToken cancellationToken) =>
        GetOptionsAsync(isSonarr: false, cancellationToken);

    /// <summary>
    /// Tests unsaved form values (no need to save + reopen the page).
    /// </summary>
    [HttpPost("sonarr/test")]
    public Task<ActionResult<ArrOptions>> TestSonarr(
        [FromBody] ArrTestRequest request, CancellationToken cancellationToken) =>
        TestAsync(request, cancellationToken);

    [HttpPost("radarr/test")]
    public Task<ActionResult<ArrOptions>> TestRadarr(
        [FromBody] ArrTestRequest request, CancellationToken cancellationToken) =>
        TestAsync(request, cancellationToken);

    private static async Task<ActionResult<ArrOptions>> GetOptionsAsync(bool isSonarr, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var url = isSonarr ? config?.SonarrUrl : config?.RadarrUrl;
        var apiKey = isSonarr ? config?.SonarrApiKey : config?.RadarrApiKey;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new ArrOptions([], [], "URL or API key is not configured. Save settings first.");
        }

        return await FetchFromAsync(url, apiKey, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ActionResult<ArrOptions>> TestAsync(
        ArrTestRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Url) || string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new ArrOptions([], [], "Enter the URL and API key first, then Test.");
        }

        return await FetchFromAsync(request.Url, request.ApiKey, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ActionResult<ArrOptions>> FetchFromAsync(
        string url, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/api/v3/") };
            http.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
            var options = await ArrOptionsFetcher.FetchAsync(http, cancellationToken).ConfigureAwait(false);
            if (options.QualityProfiles.Count == 0 && options.RootFolders.Count == 0)
            {
                return new ArrOptions([], [], "Connected, but no quality profiles or root folders found.");
            }

            return options;
        }
        catch (UriFormatException)
        {
            return new ArrOptions([], [], "URL looks invalid — expected e.g. http://192.168.1.20:8989.");
        }
        catch (HttpRequestException ex)
        {
            return new ArrOptions(
                [], [],
                "Unreachable from the Jellyfin host (" + ex.Message + "). Check URL, port and firewall.");
        }
        catch (Exception ex)
        {
            return new ArrOptions([], [], ex.Message);
        }
    }
}

public sealed record ArrTestRequest(string Url, string ApiKey);
