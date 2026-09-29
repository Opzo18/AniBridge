using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AniBridge.Controllers;

/// <summary>
/// Setup checklist for the settings pages: what is configured, what is missing.
/// </summary>
[ApiController]
[Route("AniBridge")]
[Authorize]
public sealed class StatusController : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<PluginStatus> GetStatus()
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return new PluginStatus(false, false, false, true, "Plugin configuration not loaded.");
        }

        var shindenOk = config.ShindenEnabled
            && !string.IsNullOrWhiteSpace(config.ShindenUsername)
            && !string.IsNullOrWhiteSpace(config.ShindenPassword)
            && !string.IsNullOrWhiteSpace(config.ShindenUserId);
        var sonarrOk = config.SonarrEnabled
            && IsHttpUrl(config.SonarrUrl)
            && !string.IsNullOrWhiteSpace(config.SonarrApiKey)
            && !string.IsNullOrWhiteSpace(config.SonarrRootFolder);
        var radarrOk = config.RadarrEnabled
            && IsHttpUrl(config.RadarrUrl)
            && !string.IsNullOrWhiteSpace(config.RadarrApiKey)
            && !string.IsNullOrWhiteSpace(config.RadarrRootFolder);

        string? hint = null;
        if (!shindenOk)
        {
            hint = "Fill in Shinden login, password and list ID.";
        }
        else if (!sonarrOk && !radarrOk)
        {
            hint = "Enable at least Sonarr (TV) or Radarr (movies): URL + API key + root folder.";
        }

        return new PluginStatus(shindenOk, sonarrOk, radarrOk, config.DryRun, hint);
    }

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public sealed record PluginStatus(
    bool ShindenOk,
    bool SonarrOk,
    bool RadarrOk,
    bool DryRun,
    string? Hint);
