namespace AniBridge.Controllers;

/// <summary>
/// Quality profiles and root folders of one *Arr, for settings-page dropdowns.
/// </summary>
public sealed class ArrOptions
{
    public ArrOptions()
    {
    }

    public ArrOptions(List<QualityProfileOption> qualityProfiles, List<string> rootFolders, string? error)
    {
        QualityProfiles = qualityProfiles;
        RootFolders = rootFolders;
        Error = error;
    }

    public List<QualityProfileOption> QualityProfiles { get; init; } = [];

    public List<string> RootFolders { get; init; } = [];

    /// <summary>
    /// Set when options could not be loaded (config/API error). UI keeps manual values then.
    /// </summary>
    public string? Error { get; init; }
}

public sealed class QualityProfileOption
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;
}
