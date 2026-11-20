#!/usr/bin/env bash
set -euo pipefail
runtime=osx-arm64
if [[ "$(uname -s)" != Darwin || $# -gt 1 || "${1-$runtime}" != "$runtime" ]]; then
  echo 'Usage on macOS: package-macos.sh [osx-arm64]' >&2
  exit 1
fi
repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if command -v dotnet >/dev/null 2>&1; then
  dotnet_bin="$(command -v dotnet)"
elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
  dotnet_bin="$HOME/.dotnet/dotnet"
else
  echo 'Install the .NET 10 SDK first.' >&2
  exit 1
fi
identity="${MACOS_SIGN_IDENTITY:--}"
profile="${NOTARY_KEYCHAIN_PROFILE:-}"
if [[ "$identity" != - && -z "$profile" ]]; then
  echo 'Set NOTARY_KEYCHAIN_PROFILE to the notarytool Keychain profile for this Developer ID.' >&2
  exit 1
fi

cd "$repo_dir"
project=NegativeEncoder.Desktop/NegativeEncoder.Desktop.csproj
app_version="$("$dotnet_bin" msbuild "$project" -nologo -p:Configuration=Release -getProperty:Version)"
release_version="${app_version%%-*}"
bundle_version="${app_version/-alpha./a}"
bundle_version="${bundle_version/-beta./b}"
bundle_version="${bundle_version/-rc./fc}"
output="$repo_dir/artifacts/$runtime"
mkdir -p "$output"
stage="$(mktemp -d "$output/.package-XXXXXXXX")"
trap 'rm -rf -- "$stage"' EXIT
app_dir="$stage/image/NegativeEncoder.app"
mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources/zh-Hans.lproj"
"$dotnet_bin" publish "$project" -c Release -r "$runtime" --self-contained true \
  -p:PublishSingleFile=true -p:DebugType=none -o "$app_dir/Contents/MacOS"
cp NegativeEncoder.Desktop/Assets/app-icon.icns "$app_dir/Contents/Resources/"
cp NegativeEncoder.Desktop/Assets/zh-Hans.lproj/InfoPlist.strings "$app_dir/Contents/Resources/zh-Hans.lproj/"
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
chmod -R a+rX "$app_dir"
sign=(codesign --force --sign "$identity")
if [[ "$identity" != - ]]; then
  sign+=(--timestamp)
fi

sign_code=("${sign[@]}")
if [[ "$identity" != - ]]; then
  sign_code+=(--options runtime)
fi

# Sign native dependencies inside-out. The final bundle signature covers the apphost.
find "$app_dir/Contents/MacOS" -type f ! -path "$app_dir/Contents/MacOS/NegativeEncoder.Desktop" -print0 > "$stage/files"
while IFS= read -r -d '' path; do
  kind="$(file -b "$path")"
  if [[ "$kind" == Mach-O* ]]; then
    "${sign_code[@]}" "$path"
  fi
done < "$stage/files"
"${sign_code[@]}" --entitlements scripts/macos/entitlements.plist "$app_dir"
codesign --verify --deep --strict "$app_dir"
if [[ "$identity" != - ]]; then
  # Developer ID Application leaf certificate, including when selected by SHA-1.
  codesign --verify -R='anchor apple generic and certificate leaf[field.1.2.840.113635.100.6.1.13] exists' "$app_dir"
fi
ln -s /Applications "$stage/image/Applications"
name="NegativeEncoder-$app_version-$runtime"
if [[ "$identity" == - ]]; then
  name="$name-adhoc"
fi
dmg="$stage/$name.dmg"
hdiutil create -volname NegativeEncoder -fs HFS+ -format UDZO -srcfolder "$stage/image" "$dmg"
"${sign[@]}" --identifier io.github.zyzsdy.negativeencoder.preview.dmg "$dmg"
codesign --verify --strict "$dmg"

if [[ -e "$output/NegativeEncoder.app" ]]; then
  mv "$output/NegativeEncoder.app" "$stage/previous.app"
fi
mv "$app_dir" "$output/"
printf 'App: %s/NegativeEncoder.app\n' "$output"
if [[ "$identity" == - ]]; then
  mv -f "$dmg" "$output/"
  printf 'Ad-hoc DMG: %s/%s.dmg\n' "$output" "$name"
  exit 0
fi

# Keep the exact submitted image and receipt together across builds and retries.
pending="$(mktemp -d "$output/notary-XXXXXXXX")"
mv "$dmg" "$pending/"
cd "$pending"
shasum -a 256 "$name.dmg" > image.sha256
printf 'Submitting: %s/%s.dmg\n' "$pending" "$name"
xcrun notarytool submit "$name.dmg" --keychain-profile "$profile" --no-wait --output-format plist > submission.plist
id="$(plutil -extract id raw submission.plist)"
printf 'Check status: xcrun notarytool info %q --keychain-profile %q\n' "$id" "$profile"
printf 'After Accepted: NOTARY_KEYCHAIN_PROFILE=%q %q %q\n' "$profile" \
  "$repo_dir/scripts/staple-macos.sh" "$pending/$name.dmg"
