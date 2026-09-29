using AniBridge.Configuration;
using AniBridge.Controllers;
using Xunit;

namespace AniBridge.Tests.Controllers;

public class StatusEvaluatorTests
{
    [Fact]
    public void Evaluate_EmptyConfig_AllMissingWithDetails()
    {
        var status = StatusEvaluator.Evaluate(new PluginConfiguration
        {
            ShindenEnabled = true,
            ShindenUsername = "",
            ShindenPassword = "",
            ShindenUserId = "",
        });

        Assert.False(status.ShindenOk);
        Assert.Contains("login", status.ShindenDetail, StringComparison.OrdinalIgnoreCase);
        Assert.False(status.SonarrOk);
        Assert.False(status.RadarrOk);
        Assert.NotNull(status.Hint);
    }

    [Fact]
    public void Evaluate_FullConfig_AllOkNoDetails()
    {
        var status = StatusEvaluator.Evaluate(new PluginConfiguration
        {
            ShindenEnabled = true,
            ShindenUsername = "u",
            ShindenPassword = "p",
            ShindenUserId = "1-x",
            SonarrEnabled = true,
            SonarrUrl = "http://h:8989",
            SonarrApiKey = "k",
            SonarrRootFolder = "/tv",
            RadarrEnabled = true,
            RadarrUrl = "http://h:7878",
            RadarrApiKey = "k",
            RadarrRootFolder = "/movies",
        });

        Assert.True(status.ShindenOk);
        Assert.True(status.SonarrOk);
        Assert.True(status.RadarrOk);
        Assert.Null(status.ShindenDetail);
        Assert.Null(status.SonarrDetail);
        Assert.Null(status.RadarrDetail);
        Assert.Null(status.Hint);
    }

    [Fact]
    public void Evaluate_BadSonarrUrl_PointsAtUrlFormat()
    {
        var status = StatusEvaluator.Evaluate(new PluginConfiguration
        {
            SonarrEnabled = true,
            SonarrUrl = "not a url",
            SonarrApiKey = "k",
            SonarrRootFolder = "/tv",
        });

        Assert.False(status.SonarrOk);
        Assert.Contains("URL", status.SonarrDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_NullConfig_ReturnsNotLoaded()
    {
        var status = StatusEvaluator.Evaluate(null);

        Assert.False(status.ShindenOk);
        Assert.Contains("not loaded", status.Hint, StringComparison.OrdinalIgnoreCase);
    }
}
