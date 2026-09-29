#!/usr/bin/env bash
#
# Full AniBridge release from the single VERSION file.
#
# Usage: ./scripts/release.sh ["changelog text"] [--dry-run] [--skip-push]
#
# Steps: VERSION -> dotnet test -> package.sh (ZIP + manifest.json)
#        -> sync README version line -> git tag v<VERSION>
#        -> gh release create + upload ZIP -> git push (unless --skip-push).
#        --dry-run stops before tag/release/push.
set -euo pipefail
cd "$(dirname "$0")/.."

export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

DRY_RUN=0
SKIP_PUSH=0
CHANGELOG=""
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY_RUN=1 ;;
        --skip-push) SKIP_PUSH=1 ;;
        *) CHANGELOG="$arg" ;;
    esac
done

VERSION=$(tr -d ' \t\r\n' < VERSION)
[ -n "$VERSION" ] || { echo "VERSION file is empty."; exit 1; }
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "VERSION '$VERSION' is not X.Y.Z."; exit 1; }
TAG="v$VERSION"
ZIP="dist/anibridge_$VERSION.zip"
CHANGELOG="${CHANGELOG:-see GitHub release $TAG}"

echo "Release $TAG (dry-run: $DRY_RUN)"

echo "==> dotnet test"
dotnet test AniBridge.slnx --nologo -v q

echo "==> package"
./scripts/package.sh "$CHANGELOG"

echo "==> sync README version line"
if grep -q '^Version \*\*' README.md; then
    sed -i -E "s/^Version \*\*[^*]*\*\*/Version **$VERSION**/" README.md
    echo "README.md version synced to $VERSION."
else
    echo "WARNING: no '^Version **' line in README.md, skipping."
fi

if [ "$DRY_RUN" = 1 ]; then
    echo "Dry run: stopping before tag/release/push."
    echo "Would run: git tag $TAG && gh release create $TAG $ZIP --title $TAG --notes \"$CHANGELOG\" && git push (+ --tags)"
    exit 0
fi

command -v gh >/dev/null || { echo "gh CLI not found. Install it or publish manually (see package.sh output)."; exit 1; }

echo "==> git tag $TAG"
git tag "$TAG" 2>/dev/null || echo "Tag $TAG already exists, reusing."

echo "==> gh release create $TAG"
gh release create "$TAG" "$ZIP" --title "$TAG" --notes "$CHANGELOG"

if [ "$SKIP_PUSH" = 0 ]; then
    echo "==> git push"
    git push
    git push --tags
else
    echo "Skipping git push (--skip-push). Run: git push && git push --tags"
fi

echo "Done. Commit manifest.json/README.md if not yet committed."
