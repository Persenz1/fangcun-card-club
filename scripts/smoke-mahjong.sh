#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"
task_mode="${1:-standard}"

case "$task_mode" in
    standard)
        task_seed=2026080101
        ;;
    sichuan)
        task_seed=2026080102
        ;;
    riichi)
        task_seed=2026080103
        ;;
    *)
        printf 'Unsupported Mahjong smoke mode: %s\n' "$task_mode" >&2
        exit 2
        ;;
esac

"$task_script_dir/godot.sh" \
    --headless \
    --path "$task_project_root" \
    -- \
    --preview=mahjong \
    "--mahjong-mode=$task_mode" \
    "--mahjong-seed=$task_seed" \
    --autoplay \
    --fast-autoplay \
    --quit-on-finish
