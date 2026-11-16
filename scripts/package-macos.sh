#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if command -v dotnet >/dev/null 2>&1; then
  dotnet_bin="$(command -v dotnet)"
elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
  dotnet_bin="$HOME/.dotnet/dotnet"
else
  echo 'Install the .NET 10 SDK first.' >&2
  exit 1
fi
runtime="${1:-osx-arm64}"
case "$runtime" in osx-arm64|osx-x64) ;; *) echo 'Use osx-arm64 or osx-x64.' >&2; exit 1 ;; esac
app_dir="$repo_dir/artifacts/$runtime/NegativeEncoder.app"
mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"
cd "$repo_dir"
"$dotnet_bin" publish NegativeEncoder.Desktop/NegativeEncoder.Desktop.csproj -c Release -r "$runtime" --self-contained true -o "$app_dir/Contents/MacOS"
cp NegativeEncoder/Resources/icon.png "$app_dir/Contents/Resources/icon.png"
cat > "$app_dir/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>io.github.zyzsdy.negativeencoder.preview</string>
<key>CFBundleName</key><string>NegativeEncoder</string>
<key>CFBundleDisplayName</key><string>消极压制</string>
<key>CFBundleExecutable</key><string>NegativeEncoder.Desktop</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>6.0.0</string>
<key>CFBundleVersion</key><string>6.0.0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
printf 'Local preview bundle: %s\n' "$app_dir"
printf 'Open with: open "%s"\n' "$app_dir"
