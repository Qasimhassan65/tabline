#!/bin/bash
set -euo pipefail
task_root="$(cd "$(dirname "$0")" && pwd)"
cd "$task_root"
export MACOSX_DEPLOYMENT_TARGET=14.0
task_arch="${1:-$(uname -m)}"
case "$task_arch" in arm64|x86_64|universal) ;; *) echo 'Usage: build.sh [arm64|x86_64|universal]' >&2; exit 2;; esac
task_app="$task_root/dist/$task_arch/Tabline Companion.app"
mkdir -p "$task_app/Contents/MacOS"
if [[ "$task_arch" == universal ]]; then
  if [[ "${TABLINE_USE_PREBUILT:-0}" != 1 ]]; then
    bash "$task_root/build.sh" arm64
    bash "$task_root/build.sh" x86_64
  fi
  xcrun lipo -create "$task_root/dist/arm64/Tabline Companion.app/Contents/MacOS/TablineCompanion" "$task_root/dist/x86_64/Tabline Companion.app/Contents/MacOS/TablineCompanion" -output "$task_app/Contents/MacOS/TablineCompanion"
else
  swift build --configuration release --arch "$task_arch" --scratch-path ".build/$task_arch" --product TablineCompanion
  task_bin="$(swift build --configuration release --arch "$task_arch" --scratch-path ".build/$task_arch" --show-bin-path)"
  cp "$task_bin/TablineCompanion" "$task_app/Contents/MacOS/TablineCompanion"
fi
cp Info.plist "$task_app/Contents/Info.plist"
chmod +x "$task_app/Contents/MacOS/TablineCompanion"
plutil -lint "$task_app/Contents/Info.plist"
codesign --force --sign - "$task_app"
codesign --verify --strict "$task_app"
cp Install.command "$task_root/dist/$task_arch/Install.command"
chmod +x "$task_root/dist/$task_arch/Install.command"
cp Uninstall.command "$task_root/dist/$task_arch/Uninstall.command"
chmod +x "$task_root/dist/$task_arch/Uninstall.command"
if [[ -f README.md ]]; then cp README.md "$task_root/dist/$task_arch/README.md";fi
ditto -c -k --keepParent "$task_root/dist/$task_arch" "$task_root/dist/Tabline-Mac-$task_arch-development.zip"
if [[ "$task_arch" == universal ]]; then
  xcrun lipo "$task_app/Contents/MacOS/TablineCompanion" -verify_arch arm64 x86_64
  task_disk="$task_root/dist/disk-image"
  mkdir -p "$task_disk"
  ditto "$task_app" "$task_disk/Tabline Companion.app"
  hdiutil create -volname 'Install Tabline' -srcfolder "$task_disk" -format UDZO -ov "$task_root/dist/Tabline-Mac.dmg"
  shasum -a 256 "$task_root/dist/Tabline-Mac.dmg" > "$task_root/dist/Tabline-Mac.dmg.sha256"
fi
printf 'Built development app: %s\n' "$task_app"
printf 'This ad-hoc signed build is for testing; public distribution requires Developer ID signing and notarization.\n'
