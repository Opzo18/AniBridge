using AniBridge.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace AniBridge;

/// <summary>
/// Main AniBridge plugin class: identity, configuration, and settings page.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Paths = applicationPaths;
    }

    /// <inheritdoc />
    public override string Name => "AniBridge";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("befab610-6eca-4d99-8e12-cdb4e26114aa");

    /// <inheritdoc />
    public override string Description => "Sync anime lists (Shinden.pl) with Sonarr/Radarr.";

    /// <summary>
    /// Current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Application paths captured at startup (for the sync report directory).
    /// </summary>
    public static IApplicationPaths? Paths { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = "AniBridge Shinden",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.shinden.html",
            },
            new PluginPageInfo
            {
                Name = "AniBridge Sonarr",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.sonarr.html",
            },
            new PluginPageInfo
            {
                Name = "AniBridge Radarr",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.radarr.html",
            },
        ];
    }
}
