#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"

cd "$task_project_root"

task_must_ignore=(
  '.tools/dotnet/dotnet'
  '.packages/example/package.bin'
  '.godot/imported/example.ctex'
  'src/Game.Core/bin/Debug/Game.Core.dll'
  'src/Game.Core/obj/project.assets.json'
  'TestResults/results.trx'
  'coverage/report.coveragexml'
  'artifacts/windows/FangcunCardClub.exe'
  'exports/FangcunCardClub.pck'
  'logs/game.log'
  'user-data/profile.json'
  '.env'
  '.env.local'
  'export_credentials.cfg'
  'signing/release.keystore'
  '.vscode/settings.json'
  '.vscode/launch.json'
)

task_must_track=(
  '.env.example'
  '.vscode/tasks.json'
  '.vscode/extensions.json'
  'export_presets.cfg'
  'project.godot'
  'game/scripts/Bootstrap.cs'
  'game/scripts/Bootstrap.cs.uid'
  '美术概念/大厅dark.png.import'
)

for task_path in "${task_must_ignore[@]}"; do
  if ! git check-ignore --no-index -q "$task_path"; then
    printf 'Git ignore check failed: expected ignored path %s\n' "$task_path" >&2
    exit 1
  fi
done

for task_path in "${task_must_track[@]}"; do
  if git check-ignore --no-index -q "$task_path"; then
    printf 'Git ignore check failed: expected trackable path %s\n' "$task_path" >&2
    exit 1
  fi
done

printf '.gitignore checks passed.\n'
