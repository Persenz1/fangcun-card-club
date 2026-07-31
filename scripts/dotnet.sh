#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"
task_dotnet_root="$task_project_root/.tools/dotnet"

if [[ ! -x "$task_dotnet_root/dotnet" ]]; then
  printf 'Local .NET SDK is missing. Run ./scripts/bootstrap-env.sh first.\n' >&2
  exit 1
fi

export DOTNET_ROOT="$task_dotnet_root"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
exec "$task_dotnet_root/dotnet" "$@"
