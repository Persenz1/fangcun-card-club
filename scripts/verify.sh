#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"

cd "$task_project_root"

"$task_script_dir/check-docs.sh"
"$task_script_dir/check-gitignore.sh"
"$task_script_dir/dotnet.sh" restore FangcunCardClub.sln
"$task_script_dir/dotnet.sh" build FangcunCardClub.sln --no-restore
"$task_script_dir/dotnet.sh" test FangcunCardClub.sln --no-build
"$task_script_dir/godot.sh" --headless --editor --path "$task_project_root" --quit
"$task_script_dir/godot.sh" --headless --path "$task_project_root" --quit-after 2
"$task_script_dir/godot.sh" --headless --path "$task_project_root" --quit-after 2 -- --preview=doudizhu
"$task_script_dir/godot.sh" --headless --path "$task_project_root" --quit-after 2 -- --preview=mahjong
