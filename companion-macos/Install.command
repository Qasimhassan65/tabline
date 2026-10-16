#!/bin/bash
set -euo pipefail
task_dir="$(cd "$(dirname "$0")" && pwd)"
task_source="$task_dir/Tabline Companion.app"
task_parent="$HOME/Applications"
task_destination="$task_parent/Tabline Companion.app"
if [[ ! -x "$task_source/Contents/MacOS/TablineCompanion" ]]; then echo 'Keep Install.command beside Tabline Companion.app.';read -r;exit 1;fi
printf 'Install Tabline Companion for this Mac user in %s? [y/N] ' "$task_parent"
read -r task_answer
[[ "$task_answer" == y || "$task_answer" == Y ]] || exit 0
if pgrep -f '/Tabline Companion[.]app/Contents/MacOS/TablineCompanion' >/dev/null;then echo 'Quit Chrome and Tabline Companion, then run the installer again.';read -r;exit 1;fi
mkdir -p "$task_parent"
if [[ -e "$task_destination" ]]; then
  task_backup="$task_parent/Tabline Companion.previous.$(date +%Y%m%d%H%M%S).app"
  mv "$task_destination" "$task_backup"
  printf 'Previous app preserved at %s\n' "$task_backup"
fi
ditto "$task_source" "$task_destination"
open "$task_destination"
printf 'In the setup window, paste the Chrome extension ID and allow Screen Recording.\n'
