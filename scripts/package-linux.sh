#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != Linux ]]; then
  echo 'Run this script on Linux.' >&2
  exit 1
fi
if (( $# > 1 )); then
  echo 'Usage: package-linux.sh [linux-x64|linux-arm64]' >&2
  exit 1
fi
case "${1-$(uname -m)}" in
  x86_64|linux-x64) arch=x86_64; runtime=linux-x64 ;;
  aarch64|linux-arm64) arch=aarch64; runtime=linux-arm64 ;;
  *) echo 'Use linux-x64 or linux-arm64.' >&2; exit 1 ;;
esac

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"
if command -v dotnet >/dev/null 2>&1; then
  dotnet_bin=dotnet
elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
  dotnet_bin="$HOME/.dotnet/dotnet"
else
  echo 'Install the .NET 10 SDK first.' >&2
  exit 1
fi
if ! command -v appimagetool >/dev/null 2>&1; then
  echo 'Install appimagetool on PATH first.' >&2
  exit 1
fi

project=NegativeEncoder.Desktop/NegativeEncoder.Desktop.csproj
version="$("$dotnet_bin" msbuild "$project" -nologo -p:Configuration=Release -getProperty:Version)"
name="NegativeEncoder-$version-$arch.AppImage"
output="$repo_dir/artifacts/$runtime"
mkdir -p "$output"
stage="$(mktemp -d "$output/.appimage-XXXXXXXX")"
trap 'rm -rf -- "$stage"' EXIT
app_dir="$stage/NegativeEncoder.AppDir"

"$dotnet_bin" publish "$project" -c Release -r "$runtime" --self-contained true \
  -p:DebugType=none -o "$app_dir/usr/bin"
ln -s usr/bin/NegativeEncoder.Desktop "$app_dir/AppRun"
install -Dm644 LICENSE "$app_dir/usr/share/doc/NegativeEncoder/LICENSE"
install -m644 NegativeEncoder.Desktop/Assets/app-icon.png "$app_dir/negativeencoder.png"
ln -s negativeencoder.png "$app_dir/.DirIcon"
cat > "$app_dir/negativeencoder.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=NegativeEncoder
Name[zh_CN]=消极压制
Exec=NegativeEncoder.Desktop
Icon=negativeencoder
Categories=AudioVideo;Video;
Terminal=false
DESKTOP

# NuGet assets may grant execute permission only to the build user.
chmod -R a+rX "$app_dir"
ARCH="$arch" VERSION="$version" appimagetool "$app_dir" "$stage/$name"
mv -f -- "$stage/$name" "$output/$name"
printf 'AppImage: %s\n' "$output/$name"
