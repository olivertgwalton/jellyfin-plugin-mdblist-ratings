#!/bin/sh
# ./release.sh 1.0.0.0 "What changed" — builds, publishes a GitHub release, and lists it in manifest.json.
set -e
cd "$(dirname "$0")"
VERSION="$1"; CHANGELOG="${2:-}"
REPO=olivertgwalton/jellyfin-plugin-mdblist-ratings
ZIP="mdblist-ratings_$VERSION.zip"
dotnet build src -c Release -p:Version="$VERSION"
rm -f "$ZIP" && zip -j -q "$ZIP" src/bin/Release/net10.0/Jellyfin.Plugin.MdbListRatings.dll
python3 - "$VERSION" "$CHANGELOG" "$ZIP" "$REPO" <<'PY'
import hashlib, json, sys, datetime
version, changelog, zip_path, repo = sys.argv[1:]
manifest = json.load(open("manifest.json"))
entry = {
    "version": version,
    "changelog": changelog,
    "targetAbi": "12.0.0.0",
    "sourceUrl": f"https://github.com/{repo}/releases/download/v{version}/{zip_path}",
    "checksum": hashlib.md5(open(zip_path, "rb").read()).hexdigest(),
    "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}
plugin = manifest[0]
plugin["versions"] = [entry] + [v for v in plugin["versions"] if v["version"] != version]
json.dump(manifest, open("manifest.json", "w"), indent=2)
PY
gh release create "v$VERSION" "$ZIP" --repo "$REPO" --title "$VERSION" --notes "$CHANGELOG"
rm -f "$ZIP"
git add manifest.json && git commit -qm "Release $VERSION" && git push -q
