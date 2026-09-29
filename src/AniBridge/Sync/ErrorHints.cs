namespace AniBridge.Sync;

/// <summary>
/// User-friendly, actionable hints for common sync outcomes.
/// Shown on the settings page next to the raw detail, so users know what to check.
/// </summary>
public static class ErrorHints
{
    public static string? ForItem(SyncItem item) => ForDetail(item.Outcome, item.Detail);

    public static string? ForDetail(SyncOutcome outcome, string? detail)
    {
        var d = detail ?? string.Empty;
        return outcome switch
        {
            SyncOutcome.Failed when Contains(d, "401") || Contains(d, "unauthorized")
                => "Check the Sonarr/Radarr URL and API key.",
            SyncOutcome.Failed when Contains(d, "connection") || Contains(d, "refused")
                || Contains(d, "timed out") || Contains(d, "unreachable") || Contains(d, "network")
                => "Sonarr/Radarr is unreachable from the Jellyfin host. Check URL, port and firewall.",
            SyncOutcome.Failed when Contains(d, "root folder") || Contains(d, "rootfolder")
                => "Check the root folder setting on the Sonarr/Radarr page.",
            SyncOutcome.Failed when Contains(d, "quality profile") || Contains(d, "qualityprofile")
                => "Check the quality profile setting on the Sonarr/Radarr page.",
            SyncOutcome.Failed when Contains(d, "unrecognized title") || Contains(d, "no exact match")
                => "No confident match — not added on purpose, never guessed. Fix the title or add it manually to Sonarr/Radarr.",
            SyncOutcome.Skipped when Contains(d, "Sonarr/Radarr disabled")
                => "Enable Sonarr (TV) or Radarr (movies) on their settings pages.",
            SyncOutcome.Skipped when Contains(d, "status disabled")
                => "This list status is unchecked in Synced statuses.",
            SyncOutcome.Skipped when Contains(d, "Shinden") && Contains(d, "failed to fetch")
                => "Shinden session expired or list is not public. Check login/password and list visibility.",
            _ => null,
        };
    }

    public static string? ForFatal(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        if (Contains(error, "failed to fetch the Shinden list") || Contains(error, "Shinden"))
        {
            return "Check Shinden login/password and that the list is public.";
        }

        if (Contains(error, "AniList") || Contains(error, "429"))
        {
            return "AniList rate-limited the sync — this is normal for large lists, just run sync again later.";
        }

        return null;
    }

    private static bool Contains(string text, string fragment) =>
        text.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
