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

    private static async Task<ActionResult<ArrOptions>> GetOptionsAsync(bool isSonarr, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var url = isSonarr ? config?.SonarrUrl : config?.RadarrUrl;
        var apiKey = isSonarr ? config?.SonarrApiKey : config?.RadarrApiKey;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new ArrOptions([], [], "URL or API key is not configured. Save settings first.");
        }

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/api/v3/") };
            http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            return await ArrOptionsFetcher.FetchAsync(http, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new ArrOptions([], [], ex.Message);
        }
    }
}
