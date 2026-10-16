#!/bin/bash
set -euo pipefail
task_root="$(cd "$(dirname "$0")" && pwd)"
: "${TABLINE_SIGN_IDENTITY:?Set TABLINE_SIGN_IDENTITY to your Developer ID Application signing identity}"
: "${TABLINE_NOTARY_PROFILE:?Set TABLINE_NOTARY_PROFILE to an existing notarytool keychain profile}"
task_app="$task_root/dist/universal/Tabline Companion.app"
[[ -x "$task_app/Contents/MacOS/TablineCompanion" ]] || { echo 'Run bash build.sh universal first.' >&2; exit 1; }
codesign --force --options runtime --timestamp --sign "$TABLINE_SIGN_IDENTITY" "$task_app"
codesign --verify --strict --verbose=2 "$task_app"
task_archive="$task_root/dist/Tabline-Mac-notarization.zip"
ditto -c -k --keepParent "$task_app" "$task_archive"
xcrun notarytool submit "$task_archive" --keychain-profile "$TABLINE_NOTARY_PROFILE" --wait
xcrun stapler staple "$task_app"
xcrun stapler validate "$task_app"
spctl --assess --type execute --verbose "$task_app"
ditto -c -k --keepParent "$task_root/dist/universal" "$task_root/dist/Tabline-Mac-universal.zip"
task_disk="$task_root/dist/notarized-disk-image"
mkdir -p "$task_disk"
ditto "$task_app" "$task_disk/Tabline Companion.app"
hdiutil create -volname 'Install Tabline' -srcfolder "$task_disk" -format UDZO -ov "$task_root/dist/Tabline-Mac.dmg"
codesign --timestamp --sign "$TABLINE_SIGN_IDENTITY" "$task_root/dist/Tabline-Mac.dmg"
xcrun notarytool submit "$task_root/dist/Tabline-Mac.dmg" --keychain-profile "$TABLINE_NOTARY_PROFILE" --wait
xcrun stapler staple "$task_root/dist/Tabline-Mac.dmg"
xcrun stapler validate "$task_root/dist/Tabline-Mac.dmg"
shasum -a 256 "$task_root/dist/Tabline-Mac.dmg" > "$task_root/dist/Tabline-Mac.dmg.sha256"
echo 'Developer ID signing and notarization verified. Release ZIP is ready for publication.'
