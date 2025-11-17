#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
THIS_SCRIPT="$SCRIPT_DIR/$(basename "${BASH_SOURCE[0]}")"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$PROJECT_ROOT/GitVersionTree/GitVersionTree.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"
FRAMEWORK="${FRAMEWORK:-net8.0}"
RUNTIME_IDENTIFIER="${1:-osx-arm64}"
SKIP_RESTORE="${SKIP_RESTORE:-false}"
APP_NAME="GitVersionTree"
DIST_ROOT="$PROJECT_ROOT/dist/macos-$RUNTIME_IDENTIFIER"
APP_DIR="$DIST_ROOT/${APP_NAME}.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

if [[ "$RUNTIME_IDENTIFIER" == "osx-all" ]]; then
  for rid in osx-arm64 osx-x64 osx-universal; do
    echo "==== Building $rid ===="
    if [[ "$rid" == "osx-universal" ]]; then
      PRESERVE_INTERMEDIATES=true REUSE_PUBLISH=true "$THIS_SCRIPT" "$rid"
    else
      "$THIS_SCRIPT" "$rid"
    fi
  done
  exit 0
fi

publish_runtime() {
  local rid="$1"
  echo "Publishing $APP_NAME for $rid ..."
  local args=(
    "$PROJECT"
    -c "$CONFIGURATION"
    -f "$FRAMEWORK"
    -r "$rid"
    -p:SelfContained=true
    -p:UseMacOSEntryPoint=true
    --nologo
  )
  if [[ "$SKIP_RESTORE" == "true" ]]; then
    args+=("--no-restore")
  fi
  dotnet publish "${args[@]}"
}

if [[ "$RUNTIME_IDENTIFIER" == "osx-universal" ]]; then
  BASE_RID="osx-arm64"
  SECONDARY_RID="osx-x64"
  CLEAN_INTERMEDIATES=true

  if [[ "${REUSE_PUBLISH:-false}" == "true" ]]; then
    if [[ ! -d "$PROJECT_ROOT/GitVersionTree/bin/$CONFIGURATION/$FRAMEWORK/osx-arm64/publish" ]] || \
       [[ ! -d "$PROJECT_ROOT/GitVersionTree/bin/$CONFIGURATION/$FRAMEWORK/osx-x64/publish" ]]; then
      publish_runtime "osx-arm64"
      publish_runtime "osx-x64"
    fi
  else
    publish_runtime "osx-arm64"
    publish_runtime "osx-x64"
  fi
else
  publish_runtime "$RUNTIME_IDENTIFIER"
  BASE_RID="$RUNTIME_IDENTIFIER"
  SECONDARY_RID=""
  CLEAN_INTERMEDIATES=false
fi

if [[ "${PRESERVE_INTERMEDIATES:-false}" == "true" ]]; then
  CLEAN_INTERMEDIATES=false
fi

PUBLISH_DIR="$PROJECT_ROOT/GitVersionTree/bin/$CONFIGURATION/$FRAMEWORK/$BASE_RID/publish"
SECONDARY_PUBLISH_DIR=""
if [[ -n "$SECONDARY_RID" ]]; then
  SECONDARY_PUBLISH_DIR="$PROJECT_ROOT/GitVersionTree/bin/$CONFIGURATION/$FRAMEWORK/$SECONDARY_RID/publish"
fi

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "Unable to find publish directory at $PUBLISH_DIR" >&2
  exit 1
fi
if [[ -n "$SECONDARY_RID" && ! -d "$SECONDARY_PUBLISH_DIR" ]]; then
  echo "Unable to find publish directory at $SECONDARY_PUBLISH_DIR" >&2
  exit 1
fi

echo "Assembling .app bundle ..."
rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"
cp -R "$PUBLISH_DIR/." "$MACOS_DIR/"

merge_universal_binaries() {
  local primary_dir="$1"
  local secondary_dir="$2"
  if [[ ! -d "$secondary_dir" ]]; then
    return
  fi

  if ! command -v lipo >/dev/null; then
    echo "lipo not found; cannot create universal binaries."
    return
  fi

  while IFS= read -r -d '' file; do
    local rel="${file#$primary_dir/}"
    local other="$secondary_dir/$rel"
    if [[ -f "$other" ]]; then
      if file "$file" | grep -q "Mach-O"; then
        local info
        info="$(lipo -info "$file" 2>/dev/null || true)"
        if echo "$info" | grep -q "are:"; then
          continue
        fi
        local tmp="$file.universal"
        if lipo -create "$file" "$other" -output "$tmp" >/dev/null 2>&1; then
          mv "$tmp" "$file"
        else
          rm -f "$tmp"
          echo "Failed to lipo $rel"
        fi
      fi
    fi
  done < <(find "$primary_dir" -type f -print0)
}

if [[ -n "$SECONDARY_PUBLISH_DIR" ]]; then
  merge_universal_binaries "$MACOS_DIR" "$SECONDARY_PUBLISH_DIR"
fi

VERSION="$(
  python3 - "$PROJECT" <<'PY'
import sys
from xml.etree import ElementTree as ET
tree = ET.parse(sys.argv[1])
root = tree.getroot()
version = ''
for tag in ('Version', 'VersionPrefix'):
    element = root.find(f'.//{tag}')
    if element is not None and element.text:
        version = element.text.strip()
        break
print(version, end='')
PY
)"

if [ -z "$VERSION" ]; then
  VERSION="2.0.1-alpha"
fi

cat > "$CONTENTS_DIR/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
  <dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>net.gitversiontree.app</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
  </dict>
</plist>
EOF

generate_macos_icon() {
  local icon_source="$PROJECT_ROOT/GitVersionTree/main.ico"
  if [ ! -f "$icon_source" ]; then
    echo "main.ico not found; skipping icon conversion."
    return
  fi

  if ! command -v sips >/dev/null || ! command -v python3 >/dev/null; then
    echo "sips or python3 not available; cannot convert main.ico to AppIcon.icns."
    return
  fi

  local temp_dir
  temp_dir="$(mktemp -d)"
  local base_png="$temp_dir/source.png"
  if ! sips -s format png "$icon_source" --out "$base_png" >/dev/null; then
    echo "Failed to convert $icon_source to PNG."
    rm -rf "$temp_dir"
    return
  fi

  local manifest="$temp_dir/manifest.txt"
  >"$manifest"
  local sizes=(16 32 64 128 256 512 1024)
  for size in "${sizes[@]}"; do
    local out_png="$temp_dir/icon_${size}.png"
    if ! sips -s format png -z "$size" "$size" "$base_png" --out "$out_png" >/dev/null; then
      echo "Failed to convert icon size ${size}x${size}"
      rm -rf "$temp_dir"
      return
    fi
    printf "%s:%s\n" "$size" "$out_png" >> "$manifest"
  done

  if ! python3 "$PROJECT_ROOT/scripts/write_icns.py" "$manifest" "$RESOURCES_DIR/AppIcon.icns"; then
    echo "Failed to build AppIcon.icns"
    rm -rf "$temp_dir"
    return
  fi

  rm -rf "$temp_dir"
}

generate_macos_icon

echo "macOS app bundle created at $APP_DIR"

ZIP_FILE="$DIST_ROOT/${APP_NAME}_macos-${RUNTIME_IDENTIFIER}.zip"
rm -f "$ZIP_FILE"
(cd "$DIST_ROOT" && zip -qr "$(basename "$ZIP_FILE")" "${APP_NAME}.app")
echo "Bundle archived at $ZIP_FILE"

cleanup_intermediates() {
  if [[ "$CLEAN_INTERMEDIATES" != "true" ]]; then
    return
  fi
  echo "Cleaning intermediate RID outputs ..."
  for rid in osx-arm64 osx-x64; do
    local dir="$PROJECT_ROOT/GitVersionTree/bin/$CONFIGURATION/$FRAMEWORK/$rid"
    rm -rf "$dir"
  done
}

cleanup_intermediates
