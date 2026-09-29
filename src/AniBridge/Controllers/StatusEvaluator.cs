using AniBridge.Configuration;

namespace AniBridge.Controllers;

/// <summary>
/// Pure setup-checklist evaluation, testable without a Plugin instance.
/// </summary>
public static class StatusEvaluator
{
    public static PluginStatus Evaluate(PluginConfiguration? config)
    {
        if (config is null)
        {
            return new PluginStatus(false, false, false, true, "Plugin configuration not loaded.", null, null, null);
        }

        var (shindenOk, shindenDetail) = EvaluateShinden(config);
        var (sonarrOk, sonarrDetail) = EvaluateArr(
            config.SonarrEnabled, config.SonarrUrl, config.SonarrApiKey, config.SonarrRootFolder, "8989");
        var (radarrOk, radarrDetail) = EvaluateArr(
            config.RadarrEnabled, config.RadarrUrl, config.RadarrApiKey, config.RadarrRootFolder, "7878");

        string? hint = null;
        if (!shindenOk)
        {
            hint = shindenDetail;
        }
        else if (!sonarrOk && !radarrOk)
        {
            hint = "Enable at least Sonarr (TV) or Radarr (movies): URL + API key + root folder.";
        }

        return new PluginStatus(
            shindenOk, sonarrOk, radarrOk, config.DryRun, hint,
            shindenOk ? null : shindenDetail,
            sonarrOk ? null : sonarrDetail,
            radarrOk ? null : radarrDetail);
    }

    private static (bool Ok, string? Detail) EvaluateShinden(PluginConfiguration config)
    {
        if (!config.ShindenEnabled)
        {
            return (false, "Sync disabled.");
        }

        var missing = new List<string>(3);
        if (string.IsNullOrWhiteSpace(config.ShindenUsername)) { missing.Add("login"); }
        if (string.IsNullOrWhiteSpace(config.ShindenPassword)) { missing.Add("password"); }
        if (string.IsNullOrWhiteSpace(config.ShindenUserId)) { missing.Add("list ID"); }
        if (missing.Count > 0)
        {
            return (false, "Missing: " + string.Join(", ", missing) + ".");
        }

        return (true, null);
    }

    private static (bool Ok, string? Detail) EvaluateArr(
        bool enabled, string? url, string? apiKey, string? rootFolder, string examplePort)
    {
        if (!enabled)
        {
            return (false, "Disabled.");
        }

        if (!IsHttpUrl(url))
        {
            return (false, "URL must look like http://192.168.1.20:" + examplePort + ".");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "API key required.");
        }

        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            return (false, "Root folder required — open the page and Test.");
        }

        return (true, null);
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
    string? Hint,
    string? ShindenDetail = null,
    string? SonarrDetail = null,
    string? RadarrDetail = null);
