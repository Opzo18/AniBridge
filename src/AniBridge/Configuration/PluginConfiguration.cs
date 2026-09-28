using MediaBrowser.Model.Plugins;

namespace AniBridge.Configuration;

/// <summary>
/// Plugin configuration. Settings page (ConfigurationPage) in Stage 9.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Whether sync with Shinden is enabled.
    /// </summary>
    public bool ShindenEnabled { get; set; } = true;

    /// <summary>
    /// Login or email for Shinden.pl.
    /// </summary>
    public string ShindenUsername { get; set; } = string.Empty;

    /// <summary>
    /// Password for Shinden.pl. Stored in the Jellyfin configuration file, never logged.
    /// </summary>
    public string ShindenPassword { get; set; } = string.Empty;

    /// <summary>
    /// User list ID, e.g. "624951-nick" or just "624951".
    /// The list must be public (sample: https://shinden.pl/animelist/624951).
    /// </summary>
    public string ShindenUserId { get; set; } = string.Empty;

    public bool SonarrEnabled { get; set; }

    /// <summary>
    /// Base Sonarr URL, e.g. http://192.168.33.20:8989
    /// </summary>
    public string SonarrUrl { get; set; } = string.Empty;

    public string SonarrApiKey { get; set; } = string.Empty;

    public int SonarrQualityProfileId { get; set; } = 1;

    public string SonarrRootFolder { get; set; } = string.Empty;

    public bool RadarrEnabled { get; set; }

    /// <summary>
    /// Base Radarr URL, e.g. http://192.168.33.20:7878
    /// </summary>
    public string RadarrUrl { get; set; } = string.Empty;

    public string RadarrApiKey { get; set; } = string.Empty;

    public int RadarrQualityProfileId { get; set; } = 1;

    public string RadarrRootFolder { get; set; } = string.Empty;

    /// <summary>
    /// Which list statuses take part in sync (settings page in Stage 9).
    /// </summary>
    public bool SyncPlanned { get; set; } = true;

    public bool SyncWatching { get; set; } = true;

    public bool SyncCompleted { get; set; } = true;

    public bool SyncOnHold { get; set; } = true;

    public bool SyncDropped { get; set; }

    /// <summary>
    /// Dry run: full pass with logging, but nothing is added to Sonarr/Radarr.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
