#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"
task_dotnet_root="$task_project_root/.tools/dotnet"
task_godot_executable="$task_project_root/.tools/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64"

if [[ ! -x "$task_godot_executable" || ! -x "$task_dotnet_root/dotnet" ]]; then
  printf 'Local Godot/.NET toolchain is missing. Run ./scripts/bootstrap-env.sh first.\n' >&2
  exit 1
fi

export DOTNET_ROOT="$task_dotnet_root"
export PATH="$task_dotnet_root:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export XDG_DATA_HOME="$task_project_root/.tools/godot-data"
exec "$task_godot_executable" "$@"
