#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
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

echo "Publishing $APP_NAME for $RUNTIME_IDENTIFIER ..."
PUBLISH_ARGS=(
  "$PROJECT"
  -c "$CONFIGURATION"
  -f "$FRAMEWORK"
  -r "$RUNTIME_IDENTIFIER"
  -p:SelfContained=true
  -p:UseMacOSEntryPoint=true
  --nologo
)
if [[ "$SKIP_RESTORE" == "true" ]]; then
  PUBLISH_ARGS+=("--no-restore")
fi
dotnet publish "${PUBLISH_ARGS[@]}"

PUBLISH_DIR="$PROJECT_ROOT/GitVersionTree/bin/$CONFIGURATION/$FRAMEWORK/$RUNTIME_IDENTIFIER/publish"

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "Unable to find publish directory at $PUBLISH_DIR" >&2
  exit 1
fi

echo "Assembling .app bundle ..."
rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"
cp -R "$PUBLISH_DIR/." "$MACOS_DIR/"

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
  VERSION="1.0.0"
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
