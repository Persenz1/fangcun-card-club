# 方寸牌社

《方寸牌社》是一款完全离线的像素二次元棋牌合集。首个纵向切片为经典三人叫地主斗地主，之后加入大众麻将、四川血战麻将和日式立直麻将。

## 当前状态

- Godot 4.7.1 .NET 项目骨架
- 规则层、应用层与表现层分离
- 可复现的确定性随机数
- 标准 54 张斗地主牌堆与洗牌
- 斗地主 14 类经典牌型识别、比较与完整合法出牌枚举
- 可从发牌运行到结算的斗地主叫抢/出牌状态机与基础 AI
- xUnit 自动测试
- 960×540 逻辑画布下的大厅、斗地主和麻将交互灰盒
- 16:9 核心安全区与 20:9 手机宽屏延展策略
- 按背景、交互 UI 和人物分类的美术提示词库
- 已接入大厅、斗地主和麻将三张深夜主题背景候选
- 麻将采用 2D 桌面背景与透明 3D 麻将牌层的固定视角原型
- 可替换角色与背景主题定义
- Windows x64 导出链路
- 强制任务留档和目录同步检查

## 快速开始

环境脚本仅修改仓库内被忽略的 `.tools/`，不会安装系统软件：

```bash
./scripts/bootstrap-env.sh
./scripts/verify.sh
./scripts/godot.sh --editor --path .
```

生成 Windows x64 测试包：

```bash
./scripts/export-windows.sh
```

导出结果位于被 Git 忽略的 `artifacts/windows/`。

单独使用本地 .NET：

```bash
./scripts/dotnet.sh test FangcunCardClub.sln
```

## 工程目录（必须同步维护）

最后更新：2026-08-01。

任何工程文件或目录的新增、删除、移动都必须在同一任务、同一提交中更新下面的目录。版本化 Git 钩子会对结构变化进行检查。

```text
fangcun-card-club/
├── .githooks/
│   └── pre-commit                  # 强制任务留档与目录同步
├── .vscode/
│   ├── extensions.json             # 推荐 VS Code 扩展
│   └── tasks.json                  # 构建、验证和 Godot 任务
├── docs/
│   ├── README.md                   # 文档总索引与维护规则
│   ├── product/README.md           # 产品定义与路线图
│   ├── architecture/
│   │   ├── README.md               # 架构文档索引
│   │   ├── 程序实现规划.md         # 分层、模块与里程碑
│   │   └── 麻将混合渲染.md         # 2D 桌面与透明 3D 牌层
│   ├── rules/
│   │   ├── README.md               # 玩法规则文档索引
│   │   └── 斗地主规则.md           # 斗地主已冻结牌型与流程规则
│   ├── platforms/                  # 平台适配和发布约束
│   ├── art/                        # 美术资产约定与界面背景规格
│   ├── prompts/                    # 按资产类型分类的美术生成提示词
│   ├── tasks/                      # 每项任务的永久档案与索引
│   └── templates/                  # 文档和任务模板
├── game/
│   ├── assets/
│   │   ├── backgrounds/            # 生成背景候选与后续正式母版
│   │   └── ui/                     # 可缩放的共享 Godot 界面主题
│   ├── content/
│   │   ├── characters/             # 可替换角色定义
│   │   └── themes/                 # 可替换背景与主题定义
│   ├── scenes/
│   │   ├── boot/                   # 启动、宽屏背景与中央安全区
│   │   ├── lobby/                  # 大厅交互灰盒
│   │   ├── doudizhu/               # 斗地主桌面交互灰盒
│   │   └── mahjong/                # 四人麻将桌面交互灰盒
│   └── scripts/
│       ├── mahjong/                # 麻将桌面标定、透明 3D 牌层与交互
│       └── Bootstrap.cs            # 灰盒入口与界面联动
├── scripts/
│   ├── bootstrap-env.sh            # 安装项目本地工具链
│   ├── check-docs.sh               # 文档治理检查
│   ├── check-gitignore.sh           # 忽略与必跟踪文件检查
│   ├── dotnet.sh                   # 项目本地 .NET 入口
│   ├── export-windows.sh           # Windows x64 导出
│   ├── godot.sh                    # 项目本地 Godot 入口
│   ├── setup-git-hooks.sh          # 启用版本化提交钩子
│   └── verify.sh                   # 构建、测试与启动验证
├── src/
│   ├── Game.Core/                  # 通用命令、事件和确定性随机数
│   ├── Game.Application/           # 对局编排边界
│   └── Game.Doudizhu/
│       ├── AI/                     # 只读观察驱动的基础斗地主 AI
│       ├── Cards/                  # 扑克牌、牌点、牌堆与洗牌
│       ├── Commands/               # 叫抢、出牌与不出命令
│       ├── Events/                 # 已接受斗地主操作事件
│       ├── Moves/                  # 合法出牌值与枚举
│       ├── Patterns/               # 牌型识别与比较
│       ├── Settlement/             # 倍数、春天与三方零和结算
│       └── State/                  # 发牌、叫抢和出牌状态机
├── tests/
│   ├── Game.Core.Tests/
│   └── Game.Doudizhu.Tests/        # 牌堆、牌型、比较与合法出牌测试
├── 美术概念/                       # 当前概念图，不是最终拆分素材
├── .editorconfig                   # 通用文本与 C# 格式
├── .gitignore                      # 缓存、产物和凭据忽略规则
├── AGENTS.md                       # 全仓库强制工作规则
├── Directory.Build.props           # 全解决方案 C# 构建配置
├── FangcunCardClub.Game.csproj     # Godot C# 主项目
├── FangcunCardClub.sln             # .NET 解决方案
├── NuGet.Config                    # 仓库本地 NuGet 缓存配置
├── export_presets.cfg              # Godot Windows 导出预设
├── global.json                     # 固定 .NET SDK 版本
├── project.godot                   # Godot 项目配置
└── README.md
```

本地生成且不进入 Git 的目录包括 `.tools/`、`.packages/`、`.godot/`、`artifacts/`、`exports/` 和 `logs/`。

## 文档与提交规则

- 每个开发、调研、修复、重构或发布任务都必须在 `docs/tasks/` 建立记录。
- `docs/` 根目录除索引 README 外不允许裸放 Markdown 文档。
- 新增长期有效的决定时，必须更新对应分类文档，不能只留在聊天或任务记录中。
- 工程结构变化必须同步更新上面的工程目录。
- 提交前必须运行 `./scripts/verify.sh` 并检查 `.gitignore`。

## 工程原则

- 只实现当前需求和已经确认的扩展边界，不为假设中的复杂规模提前搭框架。
- 禁止堆叠重复校验、状态哈希、镜像状态、多层兜底和没有实际用途的抽象。
- 同一条规则只在一个权威边界验证；界面、AI、提示和托管复用规则层结果，不复制判断。
- 测试覆盖真实业务规则、模块边界和容易出错的状态转换，不追求没有风险依据的重复覆盖。
- 模块必须职责单一、高内聚低耦合，不允许表现层越界实现规则，也不允许规则层依赖 Godot 或具体美术资源。
- 只有安全下载、存档迁移、明确的数据损坏风险等存在具体故障模型时，才允许增加校验和恢复机制，并在任务档案中写明原因。

详细规则见 [文档中心](docs/README.md) 和 [项目工作规则](AGENTS.md)，架构见 [程序实现规划](docs/architecture/程序实现规划.md)。
