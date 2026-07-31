#!/usr/bin/env bash
set -euo pipefail

task_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
task_project_root="$(cd "$task_script_dir/.." && pwd)"
task_failed=0

cd "$task_project_root"

task_fail()
{
  printf 'Documentation check failed: %s\n' "$1" >&2
  task_failed=1
}

for task_required_file in \
  README.md \
  AGENTS.md \
  docs/README.md \
  docs/tasks/README.md \
  docs/templates/任务记录模板.md; do
  [[ -f "$task_required_file" ]] || task_fail "missing $task_required_file"
done

while IFS= read -r task_naked_doc; do
  task_fail "Markdown files may not live directly under docs/: $task_naked_doc"
done < <(find docs -maxdepth 1 -type f -name '*.md' ! -name 'README.md' -print)

for task_category in product architecture rules platforms art tasks templates; do
  [[ -f "docs/$task_category/README.md" ]] || task_fail "missing docs/$task_category/README.md"
  rg -Fq "$task_category/README.md" docs/README.md || task_fail "docs/README.md does not index $task_category"
done

mapfile -t task_records < <(
  find docs/tasks -maxdepth 1 -type f \
    -regextype posix-extended \
    -regex '.*/[0-9]{4}-[0-9]{2}-[0-9]{2}-[0-9]{3}-.+\.md' \
    -print | sort
)

if [[ ${#task_records[@]} -eq 0 ]]; then
  task_fail 'docs/tasks contains no task records'
fi

for task_record in "${task_records[@]}"; do
  for task_heading in '## 目标' '## 范围' '## 关键决策' '## 实际改动' '## 验证' '## 遗留事项'; do
    rg -Fq "$task_heading" "$task_record" || task_fail "$task_record is missing $task_heading"
  done

  rg -q '^- 状态：(进行中|已完成|已阻塞)$' "$task_record" || task_fail "$task_record has no valid status"
  rg -Fq "$(basename "$task_record")" docs/tasks/README.md || task_fail "docs/tasks/README.md does not index $task_record"
done

for task_tree_entry in '.githooks/' 'docs/' 'game/' 'scripts/' 'src/' 'tests/' '美术概念/'; do
  rg -Fq "$task_tree_entry" README.md || task_fail "root README tree is missing $task_tree_entry"
done

rg -Fq '## 工程目录（必须同步维护）' README.md || task_fail 'root README has no mandatory project tree section'
rg -Fq '每个工程任务必须有一份记录' docs/tasks/README.md || task_fail 'task archive requirement is missing'

if [[ $task_failed -ne 0 ]]; then
  exit 1
fi

printf 'Documentation governance checks passed.\n'
