using AniBridge.Providers.Models;
using Microsoft.Extensions.Logging;

namespace AniBridge.Metadata.AniList;

/// <summary>
/// Resolves titles via AniList. Requires an EXACT match (after normalization)
/// against romaji/english/native — AniList search is fuzzy and the first result
/// can be wrong (e.g. "Your Name" → a Suntory ad). No confidence = null + warning.
/// </summary>
public sealed class AniListMetadataProvider : IMetadataProvider
{
    public const string ProviderName = "AniList";

    private readonly AniListClient _client;
    private readonly ILogger<AniListMetadataProvider> _logger;

    public AniListMetadataProvider(AniListClient client, ILogger<AniListMetadataProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => ProviderName;

    public async Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default)
    {
        var candidates = await _client.SearchAnimeAsync(item.Title, cancellationToken).ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            _logger.LogWarning("AniList: {Title} not found, skipping.", item.Title);
            return null;
        }

        var match = candidates.FirstOrDefault(c => IsTitleMatch(c, item.Title))
            ?? UniqueStrippedMatch(candidates, item.Title);
        if (match is null)
        {
            _logger.LogWarning(
                "AniList: {Title} is ambiguous ({Count} candidates, no exact match), skipping.",
                item.Title, candidates.Count);
            return null;
        }

        var type = MapFormat(match.Format);
        if (type is null)
        {
            _logger.LogWarning(
                "AniList: {Title} has unsupported format {Format}, skipping.",
                item.Title, match.Format);
            return null;
        }

        _logger.LogInformation(
            "AniList: {Title} → {Type} (id {AniListId}).", item.Title, type, match.Id);
        return new ResolvedMedia(item.Title, type.Value, match.Id, null, match.StartDate?.Year, match.Episodes);
    }

    public static bool IsTitleMatch(AniListMedia candidate, string title)
    {
        if (candidate.Title is null)
        {
            return false;
        }

        var wanted = Normalize(title);
        return (candidate.Title.Romaji is not null && Normalize(candidate.Title.Romaji) == wanted)
            || (candidate.Title.English is not null && Normalize(candidate.Title.English) == wanted)
            || (candidate.Title.Native is not null && Normalize(candidate.Title.Native) == wanted);
    }

    /// <summary>
    /// Fallback for titles with a year suffix ("Bungou Stray Dogs (2016)").
    /// Used only when nothing matches exactly, and only when exactly one
    /// candidate matches after stripping — otherwise it stays ambiguous.
    /// </summary>
    public static AniListMedia? UniqueStrippedMatch(IReadOnlyList<AniListMedia> candidates, string title)
    {
        var wanted = StripYear(Normalize(title));
        AniListMedia? found = null;
        foreach (var c in candidates)
        {
            if (c.Title is null)
            {
                continue;
            }

            var hit = (c.Title.Romaji is not null && StripYear(Normalize(c.Title.Romaji)) == wanted)
                || (c.Title.English is not null && StripYear(Normalize(c.Title.English)) == wanted)
                || (c.Title.Native is not null && StripYear(Normalize(c.Title.Native)) == wanted);
            if (!hit)
            {
                continue;
            }

            if (found is not null)
            {
                return null; // more than one — still ambiguous
            }

            found = c;
        }

        return found;
    }

    public static string StripYear(string normalizedTitle) =>
        System.Text.RegularExpressions.Regex.Replace(normalizedTitle, @"\s*\(\d{4}\)$", "");

    public static MediaType? MapFormat(string? format) => format?.Trim().ToUpperInvariant() switch
    {
        "TV" or "TV_SHORT" or "SPECIAL" or "OVA" or "ONA" => MediaType.Tv,
        "MOVIE" => MediaType.Movie,
        _ => null,
    };

    /// <summary>
    /// Lowercase, unified apostrophes, collapsed whitespace, trailing sentence
    /// punctuation stripped ("Your Name." == "Your Name").
    /// </summary>
    public static string Normalize(string title)
    {
        var s = title.Trim().ToLowerInvariant()
            .Replace('’', '\'').Replace('‘', '\'').Replace('`', '\'');
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
        return s.TrimEnd('.', '!', '?', '…');
    }
}
