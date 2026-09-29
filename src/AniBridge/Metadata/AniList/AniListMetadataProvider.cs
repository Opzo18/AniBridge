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

    /// <summary>
    /// Max extra AniList queries per title, from Shinden alternate titles.
    /// </summary>
    private const int MaxAliasQueries = 5;

    private readonly AniListClient _client;
    private readonly ILogger<AniListMetadataProvider> _logger;
    private readonly Func<AnimeListItem, CancellationToken, Task<IReadOnlyList<string>>>? _aliases;
    private string? _lastMiss;
    private IReadOnlyList<AniListMedia>? _lastCandidates;

    public AniListMetadataProvider(
        AniListClient client,
        ILogger<AniListMetadataProvider> logger,
        Func<AnimeListItem, CancellationToken, Task<IReadOnlyList<string>>>? aliasProvider = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aliases = aliasProvider;
    }

    public string Name => ProviderName;

    public string? DescribeLastMiss() => _lastMiss;

    public async Task<ResolvedMedia?> ResolveAsync(AnimeListItem item, CancellationToken cancellationToken = default)
    {
        _lastMiss = null;
        _lastCandidates = null;
        var direct = await TryResolveAsync(item.Title, null, cancellationToken).ConfigureAwait(false);
        if (direct is not null)
        {
            _logger.LogInformation(
                "AniList: {Title} → {Type} (id {AniListId}).", item.Title, direct.Type, direct.AniListId);
            return direct;
        }

        if (_aliases is null)
        {
            _lastMiss = FormatMiss([item.Title], _lastCandidates);
            _logger.LogWarning("AniList: {Title} not found, marking as failed.", item.Title);
            return null;
        }

        var aliases = await _aliases(item, cancellationToken).ConfigureAwait(false);
        var wanted = Normalize(item.Title);
        var tried = new List<string> { item.Title };
        var triedCount = 0;
        foreach (var alias in aliases)
        {
            if (triedCount >= MaxAliasQueries)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(alias) || Normalize(alias) == wanted)
            {
                continue;
            }

            triedCount++;
            tried.Add(alias);
            var via = await TryResolveAsync(alias, alias, cancellationToken).ConfigureAwait(false);
            if (via is not null)
            {
                _logger.LogInformation(
                    "AniList: {Title} → via alias {Alias} → {Type} (id {AniListId}).",
                    item.Title, alias, via.Type, via.AniListId);
                return via;
            }
        }

        _lastMiss = FormatMiss(tried, _lastCandidates);
        _logger.LogWarning(
            "AniList: {Title} not found ({Tried} alias queries), marking as failed.", item.Title, triedCount);
        return null;
    }

    private static string? FormatMiss(IReadOnlyList<string> tried, IReadOnlyList<AniListMedia>? candidates)
    {
        var parts = new List<string>(2);
        var closest = (candidates ?? [])
            .Select(c => c.Title?.Romaji ?? c.Title?.English ?? c.Title?.Native)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Take(3)
            .ToList();
        if (closest.Count > 0)
        {
            parts.Add("closest: " + string.Join(", ", closest.Select(t => $"'{Truncate(t!, 60)}'")));
        }

        if (tried.Count > 1)
        {
            parts.Add("tried: " + string.Join(", ", tried.Take(6)));
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private async Task<ResolvedMedia?> TryResolveAsync(
        string query, string? matchedAlias, CancellationToken cancellationToken)
    {
        var candidates = await _client.SearchAnimeAsync(query, cancellationToken).ConfigureAwait(false);
        if (candidates.Count > 0)
        {
            _lastCandidates = candidates;
        }
        if (candidates.Count == 0)
        {
            return null;
        }

        var match = candidates.FirstOrDefault(c => IsTitleMatch(c, query))
            ?? UniqueStrippedMatch(candidates, query)
            ?? UniqueCompactMatch(candidates, query);
        if (match is null)
        {
            return null;
        }

        var type = MapFormat(match.Format);
        if (type is null)
        {
            return null;
        }

        var canonical = match.Title?.Romaji ?? match.Title?.English;
        var english = match.Title?.English;
        if (string.Equals(english, canonical, StringComparison.OrdinalIgnoreCase))
        {
            english = null;
        }

        IReadOnlyList<string>? synonyms = match.Synonyms?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        return new ResolvedMedia(
            query, type.Value, match.Id, null, match.StartDate?.Year, match.Episodes, matchedAlias,
            canonical, english, synonyms);
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
            || (candidate.Title.Native is not null && Normalize(candidate.Title.Native) == wanted)
            || (candidate.Synonyms is not null
                && candidate.Synonyms.Any(s => s is not null && Normalize(s) == wanted));
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
                || (c.Title.Native is not null && StripYear(Normalize(c.Title.Native)) == wanted)
                || (c.Synonyms is not null
                    && c.Synonyms.Any(s => s is not null && StripYear(Normalize(s)) == wanted));
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

    /// <summary>
    /// Last-resort fallback for spacing/punctuation variants ("Toukutsu Ou" vs
    /// synonym "Toukutsuou"). Compares with all non-letters/numbers stripped —
    /// and only when exactly one candidate matches, otherwise still ambiguous.
    /// </summary>
    public static AniListMedia? UniqueCompactMatch(IReadOnlyList<AniListMedia> candidates, string title)
    {
        var wanted = Compact(Normalize(title));
        if (wanted.Length == 0)
        {
            return null;
        }

        AniListMedia? found = null;
        foreach (var c in candidates)
        {
            var hit = (c.Title?.Romaji is not null && Compact(Normalize(c.Title.Romaji)) == wanted)
                || (c.Title?.English is not null && Compact(Normalize(c.Title.English)) == wanted)
                || (c.Title?.Native is not null && Compact(Normalize(c.Title.Native)) == wanted)
                || (c.Synonyms is not null
                    && c.Synonyms.Any(s => s is not null && Compact(Normalize(s)) == wanted));
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

    public static string Compact(string normalizedTitle) =>
        System.Text.RegularExpressions.Regex.Replace(normalizedTitle, @"[^\p{L}\p{N}]", "");

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
