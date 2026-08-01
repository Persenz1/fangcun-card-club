# 文档中心

本目录是《方寸牌社》的唯一工程文档入口。除本文件外，Markdown 文档不得直接放在 `docs/` 根目录，必须进入对应分类。

## 分类索引

| 分类 | 用途 | 入口 |
| --- | --- | --- |
| 产品 | 产品定义、范围、路线图和体验目标 | [product/README.md](product/README.md) |
| 架构 | 系统分层、模块边界、数据流和 2D/3D 技术方案 | [architecture/README.md](architecture/README.md) |
| 规则 | 斗地主、麻将规则冻结与牌例 | [rules/README.md](rules/README.md) |
| 平台 | Windows、Android 等平台适配与发布 | [platforms/README.md](platforms/README.md) |
| 美术 | 资产规格、内容包约定和美术流程 | [art/README.md](art/README.md) |
| 提示词 | 按美术资产类型维护的生成提示词 | [prompts/README.md](prompts/README.md) |
| 任务档案 | 每个工程任务的目标、改动、验证和遗留项 | [tasks/README.md](tasks/README.md) |
| 模板 | 新文档和任务记录的标准模板 | [templates/README.md](templates/README.md) |

近期新增设计：[麻将混合渲染](architecture/麻将混合渲染.md)；近期冻结规则：[斗地主规则](rules/斗地主规则.md)、[大众麻将规则](rules/大众麻将规则.md)、[四川麻将规则](rules/四川麻将规则.md)、[日式立直麻将规则](rules/日式立直麻将规则.md)；近期任务：[四川血战 Godot 试玩闭环](tasks/2026-08-01-016-四川血战Godot试玩闭环.md)。

## 强制维护规则

1. 每个实施、修复、重构、调研或发布任务都必须在 `docs/tasks/` 留档。
2. 任务记录必须包含目标、范围、关键决策、实际改动、验证结果和遗留事项。
3. 新增长期有效的知识时，还要同步更新对应分类文档，任务记录不能代替正式设计文档。
4. 新建、删除或移动工程文件时，必须同步更新根目录 [README](../README.md) 中的工程目录。
5. 新增、删除或重命名文档时，必须同步更新本索引及相应分类 README。
6. 提交前运行 `./scripts/verify.sh`；Git 提交钩子会检查任务档案、目录同步和文档布局。

详细执行约束同时写在仓库根目录的 [AGENTS.md](../AGENTS.md)，供开发者和自动化代理共同遵守。
