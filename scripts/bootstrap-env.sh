#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"
task_tools_dir="$task_project_root/.tools"
task_cache_dir="$task_tools_dir/download-cache"
task_dotnet_dir="$task_tools_dir/dotnet"
task_godot_dir="$task_tools_dir/godot"

task_dotnet_version="10.0.302"
task_godot_version="4.7.1"
task_godot_folder="Godot_v${task_godot_version}-stable_mono_linux_x86_64"
task_godot_archive="${task_godot_folder}.zip"
task_godot_executable="$task_godot_dir/$task_godot_folder/Godot_v${task_godot_version}-stable_mono_linux.x86_64"
task_template_version="${task_godot_version}.stable.mono"
task_template_archive="Godot_v${task_godot_version}-stable_mono_export_templates.tpz"
task_template_dir="$task_tools_dir/godot-data/godot/export_templates/$task_template_version"

if [[ "$(uname -s)" != "Linux" || "$(uname -m)" != "x86_64" ]]; then
  printf 'This bootstrap currently supports Linux x86_64 only.\n' >&2
  exit 1
fi

for task_dependency in curl unzip sha512sum rg; do
  if ! command -v "$task_dependency" >/dev/null 2>&1; then
    printf 'Missing required bootstrap dependency: %s\n' "$task_dependency" >&2
    exit 1
  fi
done

mkdir -p "$task_cache_dir" "$task_dotnet_dir" "$task_godot_dir"

if [[ ! -x "$task_dotnet_dir/dotnet" ]] || [[ "$($task_dotnet_dir/dotnet --version)" != "$task_dotnet_version" ]]; then
  curl -fsSL 'https://dot.net/v1/dotnet-install.sh' -o "$task_cache_dir/dotnet-install.sh"
  bash "$task_cache_dir/dotnet-install.sh" \
    --version "$task_dotnet_version" \
    --install-dir "$task_dotnet_dir" \
    --no-path
fi

if [[ ! -x "$task_godot_executable" ]]; then
  curl -fL --continue-at - \
    "https://github.com/godotengine/godot/releases/download/${task_godot_version}-stable/$task_godot_archive" \
    -o "$task_cache_dir/$task_godot_archive"
  curl -fsSL \
    "https://github.com/godotengine/godot/releases/download/${task_godot_version}-stable/SHA512-SUMS.txt" \
    -o "$task_cache_dir/SHA512-SUMS.txt"

  (
    cd "$task_cache_dir"
    rg "^\\S+\\s+${task_godot_archive}$" SHA512-SUMS.txt | sha512sum -c -
  )

  unzip -q -o "$task_cache_dir/$task_godot_archive" -d "$task_godot_dir"
  chmod +x "$task_godot_executable"
fi

if [[ ! -f "$task_template_dir/version.txt" ]]; then
  curl -fL --continue-at - \
    "https://github.com/godotengine/godot/releases/download/${task_godot_version}-stable/$task_template_archive" \
    -o "$task_cache_dir/$task_template_archive"
  curl -fsSL \
    "https://github.com/godotengine/godot/releases/download/${task_godot_version}-stable/SHA512-SUMS.txt" \
    -o "$task_cache_dir/SHA512-SUMS.txt"

  (
    cd "$task_cache_dir"
    rg "^\\S+\\s+${task_template_archive}$" SHA512-SUMS.txt | sha512sum -c -
  )

  mkdir -p "$task_template_dir"
  unzip -q -j -o "$task_cache_dir/$task_template_archive" 'templates/*' -d "$task_template_dir"
fi

printf '.NET: '
"$task_dotnet_dir/dotnet" --version
printf 'Godot: '
"$task_godot_executable" --version
printf 'Export templates: %s\n' "$(<"$task_template_dir/version.txt")"
