#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"

cd "$task_project_root"

if [[ ! -d .git ]]; then
  printf 'Not a Git repository: %s\n' "$task_project_root" >&2
  exit 1
fi

git config core.hooksPath .githooks
printf 'Git hooks enabled from .githooks/.\n'
