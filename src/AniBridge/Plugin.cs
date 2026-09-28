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

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = "anibridge",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
            },
        ];
    }
}
