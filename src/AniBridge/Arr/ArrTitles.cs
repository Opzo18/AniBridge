using AniBridge.Metadata;

namespace AniBridge.Arr;

/// <summary>
/// Shared title handling for Sonarr/Radarr catalog lookup.
/// Matching stays exact-after-normalization (never fuzzy): normalization only
/// forgives punctuation/case, and season suffixes are stripped as extra
/// candidates so anime sequels ("… 2nd Season", "… Final Season") match the
/// base series Sonarr/Radarr holds as one entry.
/// </summary>
public static class ArrTitles
{
    /// <summary>
    /// Lowercase, apostrophes dropped, every other non-letter/number turned
    /// into a space, whitespace collapsed. "Re:Zero…" == "re zero".
    /// </summary>
    public static string Normalize(string title)
    {
        var s = title.Trim().ToLowerInvariant();
        s = System.Text.RegularExpressions.Regex.Replace(s, @"['’‘`´]", string.Empty);
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[^\p{L}\p{N} ]", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }

    /// <summary>
    /// Returns the title with a trailing season marker removed
    /// ("Season 2", "2nd Season", "Final Season", "Part 2", "III"),
    /// or null when there is no such suffix.
    /// </summary>
    public static string? StripSeason(string title)
    {
        var s = title.Trim();
        if (s.Length < 4)
        {
            return null;
        }

        string[] patterns =
        [
            @"\s+season\s+\d{1,2}\s*$",
            @"\s+\d{1,2}(st|nd|rd|th)\s+season\s*$",
            @"\s+final\s+season\s*$",
            @"\s+part\s+\d{1,2}\s*$",
            @"\s+(ii|iii|iv|vi{0,3}|vii)\s*$",
        ];

        foreach (var pattern in patterns)
        {
            var stripped = System.Text.RegularExpressions.Regex.Replace(
                s, pattern, string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim().TrimEnd('.', '!', '?', '…', '-', '–', '—', ':');
            if (stripped.Length >= 3
                && !string.Equals(Normalize(stripped), Normalize(s), StringComparison.Ordinal))
            {
                return stripped;
            }
        }

        return null;
    }

    /// <summary>
    /// Lookup candidates in order: canonical, english, list title, matched
    /// alias, AniList synonyms, then season-stripped bases of all of those.
    /// </summary>
    public static IReadOnlyList<string> CandidateTitles(ResolvedMedia media)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>(10);
        void Add(string? t)
        {
            if (string.IsNullOrWhiteSpace(t))
            {
                return;
            }

            var trimmed = t.Trim();
            if (trimmed.Length == 0)
            {
                return;
            }

            if (seen.Add(Normalize(trimmed)))
            {
                list.Add(trimmed);
            }
        }

        Add(media.CanonicalTitle);
        Add(media.EnglishTitle);
        Add(media.Title);
        Add(media.MatchedAlias);
        if (media.Synonyms is not null)
        {
            foreach (var s in media.Synonyms)
            {
                Add(s);
            }
        }

        var direct = list.Count;
        for (var i = 0; i < direct; i++)
        {
            var stripped = StripSeason(list[i]);
            if (stripped is not null)
            {
                Add(stripped);
            }
        }

        return list;
    }
}
