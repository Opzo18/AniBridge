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
}
