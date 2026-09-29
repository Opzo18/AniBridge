using System.Text.Json;
using AngleSharp.Html.Parser;

namespace AniBridge.Providers.Shinden;

/// <summary>
/// Shinden list parser. Pure HTML → entries function, no HTTP.
/// Handles /animelist/{user}/all pages and status subpages
/// (in-progress, completed, plan, hold, dropped) — all use
/// the same table.title-list table.
/// </summary>
public static class ShindenParser
{
    public static async Task<IReadOnlyList<ShindenListEntry>> ParseAnimeListAsync(
        string html, CancellationToken cancellationToken = default)
    {
        var parser = new HtmlParser();
        var doc = await parser.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);

        var entries = new List<ShindenListEntry>();
        foreach (var row in doc.QuerySelectorAll("table.title-list tr.title-row"))
        {
            var entry = ParseRow(row);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static ShindenListEntry? ParseRow(AngleSharp.Dom.IElement row)
    {
        var idAttr = row.GetAttribute("data-title-id");
        if (!int.TryParse(idAttr, out var id))
        {
            return null;
        }

        var link = row.QuerySelector("td.title-col a");
        var href = link?.GetAttribute("href")?.Trim();
        var title = link?.TextContent.Trim();
        if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(title))
        {
            return null;
        }

        var cells = row.QuerySelectorAll("td");
        var status = cells.Length > 3 ? cells[3].TextContent.Trim() : string.Empty;
        var (watched, total) = ParseProgress(cells.Length > 4 ? cells[4].TextContent : string.Empty);
        var type = cells.Length > 5 ? cells[5].TextContent.Trim() : string.Empty;

        return new ShindenListEntry
        {
            ShindenId = id,
            Title = title,
            Url = href,
            StatusText = status,
            WatchedEpisodes = watched,
            TotalEpisodes = total,
            TypeText = type,
        };
    }

    private static (int Watched, int? Total) ParseProgress(string text)
    {
        // Format: "watched/total", e.g. "3/12". May be incomplete for movies or missing data.
        var parts = text.Trim().Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var watched))
        {
            return (0, null);
        }

        int? total = parts.Length == 2 && int.TryParse(parts[1], out var t) ? t : null;
        return (watched, total);
    }

    /// <summary>
    /// Alternate titles from a Shinden title page (/series/…), used as extra
    /// AniList queries when the list title does not match (e.g. "Dogulwang" →
    /// "도굴왕, 盗掘王, Tomb Raider King").
    /// Best-effort: unknown markup yields an empty list, never an exception.
    /// Strategies: div.title-other, JSON-LD alternateName, og:title,
    /// labeled "…tytuły" rows.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ParseTitleAliasesAsync(
        string html, CancellationToken cancellationToken = default)
    {
        var found = new List<string>();
        try
        {
            var parser = new HtmlParser();
            var doc = await parser.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);

            var other = doc.QuerySelector("div.title-other");
            if (other is not null)
            {
                foreach (var link in other.QuerySelectorAll("a"))
                {
                    link.Remove();
                }

                AddAliases(other.TextContent, found);
            }

            foreach (var block in doc.QuerySelectorAll("script[type=\"application/ld+json\"]"))
            {
                CollectJsonLdAliases(block.TextContent, found);
            }

            var og = doc.QuerySelector("meta[property=\"og:title\"]")?.GetAttribute("content");
            AddAliases(og, found);

            foreach (var el in doc.QuerySelectorAll("dt, th, .info-label, .label"))
            {
                var label = el.TextContent.Trim().ToLowerInvariant();
                if (!label.Contains("tytu", StringComparison.Ordinal))
                {
                    continue;
                }

                var sibling = el.NextElementSibling;
                var value = sibling?.TextContent
                    ?? el.ParentElement?.TextContent.Replace(el.TextContent, string.Empty);
                AddAliases(value, found);
            }
        }
        catch
        {
            // Best-effort by design.
        }

        return found
            .Select(a => a.Trim())
            .Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static void AddAliases(string? value, List<string> found)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var part in value.Split([',', '/', ';', '\n', '\r'], StringSplitOptions.TrimEntries))
        {
            if (part.Length > 0)
            {
                found.Add(part);
            }
        }
    }

    private static void CollectJsonLdAliases(string json, List<string> found)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            CollectJsonLdAliases(doc.RootElement, found);
        }
        catch
        {
            // Malformed JSON-LD: ignore this block.
        }
    }

    private static void CollectJsonLdAliases(JsonElement el, List<string> found)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddAliases(item.GetString(), found);
                    }
                    else
                    {
                        CollectJsonLdAliases(item, found);
                    }
                }

                break;
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.NameEquals("alternateName"))
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            AddAliases(prop.Value.GetString(), found);
                        }
                        else
                        {
                            CollectJsonLdAliases(prop.Value, found);
                        }
                    }
                    else
                    {
                        CollectJsonLdAliases(prop.Value, found);
                    }
                }

                break;
        }
    }
}
