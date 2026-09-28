using System.Net.Http.Json;
using System.Text.Json;

namespace AniBridge.Controllers;

/// <summary>
/// Reads quality profiles and root folders from Sonarr/Radarr API v3.
/// Both Arrs expose GET qualityprofile and GET rootfolder with the same shapes.
/// </summary>
public static class ArrOptionsFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<ArrOptions> FetchAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        var profiles = await GetAsync<List<QualityProfileOption>>(
            http, "qualityprofile", cancellationToken).ConfigureAwait(false);
        var folders = await GetAsync<List<RootFolderDto>>(
            http, "rootfolder", cancellationToken).ConfigureAwait(false);

        return new ArrOptions(
            profiles ?? [],
            folders?.Select(f => f.Path ?? string.Empty).Where(p => p.Length > 0).ToList() ?? [],
            null);
    }

    private static async Task<T?> GetAsync<T>(HttpClient http, string path, CancellationToken cancellationToken)
        where T : class
    {
        using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private sealed class RootFolderDto
    {
        public string? Path { get; init; }
    }
}
