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
cp NegativeEncoder.Desktop/Assets/app-icon.icns "$app_dir/Contents/Resources/app-icon.icns"
mkdir -p "$app_dir/Contents/Resources/zh-Hans.lproj"
cp NegativeEncoder.Desktop/Assets/zh-Hans.lproj/InfoPlist.strings "$app_dir/Contents/Resources/zh-Hans.lproj/"
app_version="$("$dotnet_bin" msbuild NegativeEncoder.Desktop/NegativeEncoder.Desktop.csproj -getProperty:Version)"
release_version="${app_version%%-*}"
bundle_version="${app_version/-alpha./a}"
cat > "$app_dir/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>io.github.zyzsdy.negativeencoder.preview</string>
<key>CFBundleName</key><string>NegativeEncoder</string>
<key>CFBundleDisplayName</key><string>NegativeEncoder</string>
<key>CFBundleDevelopmentRegion</key><string>en</string>
<key>CFBundleLocalizations</key><array><string>zh-Hans</string><string>en</string><string>ja</string></array>
<key>CFBundleExecutable</key><string>NegativeEncoder.Desktop</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$release_version</string>
<key>CFBundleVersion</key><string>$bundle_version</string>
<key>CFBundleGetInfoString</key><string>NegativeEncoder $app_version</string>
<key>CFBundleIconFile</key><string>app-icon.icns</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
printf 'Local app bundle: %s\n' "$app_dir"
printf 'Open with: open "%s"\n' "$app_dir"
