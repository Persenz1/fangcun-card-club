#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"
task_output_dir="$task_project_root/artifacts/windows"
task_bundle_name="FangcunCardClub-win64-playtest"
task_bundle_dir="$task_output_dir/$task_bundle_name"
task_zip_path="$task_output_dir/$task_bundle_name.zip"
task_staging_dir=""
task_verify_dir=""
task_working_zip=""

cleanup() {
  if [[ -n "$task_staging_dir" && -d "$task_staging_dir" ]]; then
    rm -rf -- "$task_staging_dir"
  fi
  if [[ -n "$task_verify_dir" && -d "$task_verify_dir" ]]; then
    rm -rf -- "$task_verify_dir"
  fi
  if [[ -n "$task_working_zip" && -f "$task_working_zip" ]]; then
    rm -f -- "$task_working_zip"
  fi
}

trap cleanup EXIT

mkdir -p "$task_output_dir"
for task_command in zip unzip diff; do
  if ! command -v "$task_command" >/dev/null 2>&1; then
    printf 'Required packaging command is unavailable: %s\n' "$task_command" >&2
    exit 1
  fi
done

task_staging_dir="$(mktemp -d "$task_output_dir/.windows-export-XXXXXX")"
task_verify_dir="$(mktemp -d "$task_output_dir/.windows-verify-XXXXXX")"
cd "$task_project_root"

"$task_script_dir/dotnet.sh" build FangcunCardClub.Game.csproj --configuration Release
"$task_script_dir/godot.sh" \
  --headless \
  --path "$task_project_root" \
  --export-release "Windows x64" \
  "$task_staging_dir/FangcunCardClub.exe"

task_exe_path="$task_staging_dir/FangcunCardClub.exe"
task_pck_path="$task_staging_dir/FangcunCardClub.pck"
if [[ ! -s "$task_exe_path" || ! -s "$task_pck_path" ]]; then
  printf 'Windows export is missing a non-empty EXE or PCK.\n' >&2
  exit 1
fi

mapfile -t task_runtime_dirs < <(
  find "$task_staging_dir" \
    -mindepth 1 \
    -maxdepth 1 \
    -type d \
    -name 'data_*_windows_x86_64' \
    -print
)
if [[ "${#task_runtime_dirs[@]}" -ne 1 ]]; then
  printf 'Expected one exported .NET runtime directory, found %s.\n' "${#task_runtime_dirs[@]}" >&2
  exit 1
fi

task_runtime_dir="${task_runtime_dirs[0]}"
task_required_runtime_files=(
  FangcunCardClub.Game.deps.json
  FangcunCardClub.Game.dll
  FangcunCardClub.Game.runtimeconfig.json
  Game.Application.dll
  Game.Core.dll
  Game.Doudizhu.dll
  Game.Mahjong.dll
  Game.Mahjong.Standard.dll
  Game.Mahjong.Sichuan.dll
  Game.Mahjong.Riichi.dll
  GodotSharp.dll
  System.Private.CoreLib.dll
  clrjit.dll
  coreclr.dll
  hostfxr.dll
  hostpolicy.dll
)
for task_runtime_file in "${task_required_runtime_files[@]}"; do
  if [[ ! -s "$task_runtime_dir/$task_runtime_file" ]]; then
    printf 'Exported .NET runtime is incomplete: %s is missing.\n' "$task_runtime_file" >&2
    exit 1
  fi
done

task_runtime_name="$(basename "$task_runtime_dir")"
task_zip_filename="$task_bundle_name.zip"
(
  cd "$task_staging_dir"
  zip -q -r \
    "$task_zip_filename" \
    FangcunCardClub.exe \
    FangcunCardClub.pck \
    "$task_runtime_name"
)
task_working_zip="$task_output_dir/.$task_bundle_name-${BASHPID}.zip"
mv "$task_staging_dir/$task_zip_filename" "$task_working_zip"
unzip -q "$task_working_zip" -d "$task_verify_dir"
diff -qr "$task_staging_dir" "$task_verify_dir"

if [[ -e "$task_bundle_dir" ]]; then
  rm -rf -- "$task_bundle_dir"
fi
mv "$task_verify_dir" "$task_bundle_dir"
task_verify_dir=""
mv -f "$task_working_zip" "$task_zip_path"
task_working_zip=""

task_runtime_file_count="$(find "$task_bundle_dir/$task_runtime_name" -type f | wc -l)"
printf 'Windows bundle created at %s\n' "$task_bundle_dir"
printf 'Windows package created at %s\n' "$task_zip_path"
printf '.NET runtime directory contains %s files.\n' "$task_runtime_file_count"
