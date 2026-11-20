#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != Darwin || $# != 1 || "$1" != *.dmg ]]; then
  echo 'Usage on macOS: staple-macos.sh <submitted.dmg>' >&2
  exit 1
fi
profile="${NOTARY_KEYCHAIN_PROFILE:?Set NOTARY_KEYCHAIN_PROFILE to the profile used for submission.}"
cd "$(dirname "$1")"
name="$(basename "$1")"
id="$(plutil -extract id raw submission.plist)"
status="$(xcrun notarytool info "$id" --keychain-profile "$profile" --output-format plist | plutil -extract status raw -)"
if [[ "$status" != Accepted ]]; then
  printf 'Submission %s: %s\n' "$id" "$status" >&2
  if [[ "$status" == 'In Progress' ]]; then
    printf 'Check again: xcrun notarytool info %q --keychain-profile %q\n' "$id" "$profile" >&2
  else
    printf 'Submission log: xcrun notarytool log %q --keychain-profile %q\n' "$id" "$profile" >&2
  fi
  exit 1
fi

# Stapling modifies the DMG. Verify and staple a copy so the submission is retryable.
stage="$(mktemp -d .staple-XXXXXXXX)"
trap 'rm -rf -- "$stage"' EXIT
cp "$name" "$stage/"
(cd "$stage" && shasum -a 256 -c ../image.sha256)
xcrun stapler staple "$stage/$name"
xcrun stapler validate "$stage/$name"
codesign --verify --strict "$stage/$name"
mv -f "$stage/$name" "../$name"
printf 'Notarized DMG: %s/%s\n' "$(cd .. && pwd)" "$name"
