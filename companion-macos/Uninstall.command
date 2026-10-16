#!/bin/bash
set -euo pipefail
printf 'Disconnect the Tabline Mac companion for this Chrome user? [y/N] '
read -r task_answer
[[ "$task_answer" == y || "$task_answer" == Y ]] || exit 0
for task_browser in Google/Chrome Google/ChromeForTesting Chromium;do
task_manifest="$HOME/Library/Application Support/$task_browser/NativeMessagingHosts/com.tabline.companion.json"
if [[ -f "$task_manifest" ]];then
  task_executable="$(plutil -extract path raw -o - "$task_manifest")"
  if [[ "$task_executable" != "$HOME/Applications/Tabline Companion.app/Contents/MacOS/TablineCompanion" ]];then
    echo 'Registration points to a different installation. Leaving it intact.';exit 1
  fi
  rm "$task_manifest"
fi
done
echo 'Disconnected. Quit Chrome, then move Tabline Companion from your user Applications folder to Trash if desired.'
echo 'Saved screenshots and the clipboard are not removed.'
