#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"
task_output_dir="$task_project_root/artifacts/windows"

mkdir -p "$task_output_dir"
cd "$task_project_root"

"$task_script_dir/dotnet.sh" build FangcunCardClub.Game.csproj --configuration Release
"$task_script_dir/godot.sh" \
  --headless \
  --path "$task_project_root" \
  --export-release "Windows x64" \
  "$task_output_dir/FangcunCardClub.exe"

printf 'Windows build created at %s\n' "$task_output_dir"
