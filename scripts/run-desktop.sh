#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if command -v dotnet >/dev/null 2>&1; then
  dotnet_bin="$(command -v dotnet)"
elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
  dotnet_bin="$HOME/.dotnet/dotnet"
  export DOTNET_ROOT="$HOME/.dotnet"
else
  echo 'Install the .NET 10 SDK, then run this script again.' >&2
  exit 1
fi
cd "$repo_dir"
exec "$dotnet_bin" run --project NegativeEncoder.Desktop/NegativeEncoder.Desktop.csproj "$@"
