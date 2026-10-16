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
echo 'Developer ID signing and notarization verified. Release ZIP is ready for publication.'
