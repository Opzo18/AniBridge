#!/usr/bin/env bash
#
# Packages the AniBridge release and updates manifest.json.
#
# Usage: ./scripts/package.sh ["changelog text"]
#
# Steps: publish (Release) -> dist/anibridge_<version>.zip (AniBridge.dll + AngleSharp.dll)
#        -> MD5 -> entry in manifest.json. Upload the ZIP manually to the GitHub Release tagged v<version>,
#        commit manifest.json. The version comes from the repo-root VERSION file
#        (single source of truth, via Directory.Build.props).
set -euo pipefail
cd "$(dirname "$0")/.."

export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

VERSION=$(tr -d ' \t\r\n' < VERSION)
ABI3=$(sed -n 's/.*Jellyfin.Controller" Version="\([^"]*\)".*/\1/p' src/AniBridge/AniBridge.csproj | head -1)
[ -n "$VERSION" ] && [ -n "$ABI3" ] || { echo "Could not read VERSION file or ABI from csproj."; exit 1; }

MANIFEST_VERSION="$VERSION.0"
ABI="$ABI3.0"
TAG="v$VERSION"
ZIP="anibridge_$VERSION.zip"
REPO=$(git remote get-url origin | sed -E -e 's/\.git$//' -e 's#.*github\.com[:/]##')
SOURCE_URL="https://github.com/$REPO/releases/download/$TAG/$ZIP"
CHANGELOG="${1:-see GitHub release $TAG}"
TIMESTAMP=$(date '+%F %T')

echo "Version: $VERSION (manifest: $MANIFEST_VERSION, ABI: $ABI)"

dotnet publish src/AniBridge/AniBridge.csproj -c Release --nologo -v q
rm -rf dist && mkdir -p dist
(
    cd src/AniBridge/bin/Release/net10.0/publish
    zip -j "$OLDPWD/dist/$ZIP" AniBridge.dll AngleSharp.dll > /dev/null
)
CHECKSUM=$(md5sum "dist/$ZIP" | cut -d' ' -f1)
echo "ZIP: dist/$ZIP (md5: $CHECKSUM)"

TMP=$(mktemp)
jq --arg ver "$MANIFEST_VERSION" \
   --arg sum "$CHECKSUM" \
   --arg url "$SOURCE_URL" \
   --arg ts "$TIMESTAMP" \
   --arg abi "$ABI" \
   --arg cl "$CHANGELOG" \
   '(.[0].versions |= (map(select(.version != $ver)) | [{
        version: $ver, changelog: $cl, targetAbi: $abi,
        sourceUrl: $url, checksum: $sum, timestamp: $ts
      }] + .))' \
   manifest.json > "$TMP" && mv "$TMP" manifest.json
echo "manifest.json updated."

echo
echo "Next steps:"
echo "  1. Create a GitHub Release tagged $TAG and upload dist/$ZIP."
echo "  2. Commit manifest.json."
echo "  3. In Jellyfin: Dashboard -> Plugins -> Repositories -> add"
echo "     https://raw.githubusercontent.com/$REPO/main/manifest.json"
