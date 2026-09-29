# AniBridge

A Jellyfin plugin that syncs anime lists from external services (starting with **Shinden.pl**)
with **Sonarr** (TV shows) and **Radarr** (movies).

Flow: `Shinden → AniBridge → AniList → Sonarr/Radarr → download → Jellyfin`

AniBridge does **not** talk to qBittorrent — Sonarr/Radarr handle that themselves.

## Status

Version **0.4.3** live: miss diagnostics (`closest`/`tried` in skipped details),
spacing-proof fallback matching, Shinden aliases debug endpoint, chip headers. ✅
(`dotnet test`: 123/123 passed)

## Requirements

- Jellyfin Server **12.x** (`Jellyfin.*` packages **12.1.0**, target `net10.0`, SDK 10)
- Sonarr v3/v4 + Radarr v3/v5 (reachable from the Jellyfin host)
- A Shinden.pl account with a **public** list

## Building

```bash
export PATH="$HOME/.dotnet:$PATH"   # SDK 10, e.g. via dotnet-install.sh
dotnet build AniBridge.slnx -c Release
dotnet test AniBridge.slnx
```

## Install (repository — recommended)

1. In Jellyfin: Dashboard → Plugins → Repositories → add:
   `https://raw.githubusercontent.com/Opzo18/AniBridge/main/manifest.json`
2. Dashboard → Plugins → Catalog → **AniBridge** → Install. Updates show up automatically.

## Install (manual)

1. `dotnet publish src/AniBridge/AniBridge.csproj -c Release`
2. On the Jellyfin host, create a `plugins/AniBridge/` directory inside the configuration directory.
3. Copy **both** files from `bin/Release/net10.0/publish/` there: `AniBridge.dll` **and** `AngleSharp.dll`
   (do not copy the `Jellyfin.*` libraries — the server provides them).
4. Restart Jellyfin → Dashboard → Plugins → **AniBridge**, then open the plugin's settings page.

## New release (for the maintainer)

1. Bump `<Version>` in `src/AniBridge/AniBridge.csproj`.
2. `./scripts/package.sh "changelog text"` — builds the ZIP in `dist/`, computes the MD5, appends an entry to `manifest.json`.
3. Create a GitHub Release tagged `v<version>` and upload `dist/anibridge_<version>.zip`.
4. Commit `manifest.json` (and the code).

## Configuration (plugin pages)

- **AniBridge Shinden**: toggle, login/email, password, list ID (`420984-opzo`
  from `shinden.pl/animelist/…` — a pasted full URL works too), synced statuses,
  **dry run** (on by default: logs what would be added without adding anything),
  **Sync now** button.
- **AniBridge Sonarr / Radarr**: toggle, URL, API key (Settings → General → Security),
  quality profile and root folder picked from dropdowns loaded live from the *Arr
  (save the URL/key first, then reopen the page). If the *Arr is unreachable,
  saved values are kept and a warning is shown.
- **Statuses**: which list statuses take part in the sync (everything except Dropped by default).

## How it works

- Task `AniBridge: list sync` (Dashboard → Scheduled Tasks): daily at 04:00.
- Manually: the **Sync now** button on the Shinden page or Run next to the task.
- Results in the logs: `Scanned / Added / AlreadyExists / Skipped / Failed / WouldAdd`.
- The same summary plus per-title lists lives on the Shinden settings page
  ("Last sync" section, served from the previous run — no log digging).
- First full sync of a large list takes a while (AniList allows ~90 requests/min,
  AniBridge paces itself) — this is normal; watch the task progress.

Rules: TV → Sonarr, movies → Radarr; an uncertain title = skipped + warning (never guessing);
an existing entry = no action; nothing is ever deleted — neither entries nor files.

## Diagnostics

- `Sonarr/Radarr is not configured` in the logs → fill in the URL and API key, restart.
- `no exact match` → a Shinden title does not match Sonarr/Radarr/AniList;
  the entry is skipped, the rest of the list keeps syncing.
- `failed to fetch the Shinden list` → the session expired or Shinden changed its HTML;
  check the login/password and report with a log snippet.

## Security

Secrets live only in the Jellyfin configuration — never in code or logs.
License: GPL-3.0 (required by linking against the Jellyfin packages).
