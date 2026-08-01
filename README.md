# 方寸牌社

《方寸牌社》是一款完全离线的像素二次元棋牌合集。首个纵向切片为经典三人叫地主斗地主，之后加入大众麻将、四川血战麻将和日式立直麻将。

## 当前状态

- Godot 4.7.1 .NET 项目骨架
- 规则层、应用层与表现层分离
- 可复现的确定性随机数
- 标准 54 张斗地主牌堆与洗牌
- 斗地主 14 类经典牌型识别、比较与完整合法出牌枚举
- 可从发牌运行到结算的斗地主叫抢/出牌状态机与基础 AI
- 本地豆子、免费补给、版本化 JSON 档案与确定性斗地主对局恢复
- 可从大厅进入、对两名 AI 完成叫抢与出牌、结算并再来一局的 Godot 斗地主试玩闭环
- 可从大厅进入、对三名 AI 完成摸打、吃碰杠胡、结算并再来一局的 Godot 大众麻将试玩闭环
- 可从大厅进入、对三名 AI 完成换三张、定缺、血战、刮风下雨与最终结算的 Godot 四川麻将试玩闭环
- 可从大厅进入、对三名 AI 完成整场东风战、连庄、流局与最终排名的 Godot 四人日麻试玩闭环
- 麻将 34 种牌、确定性牌墙、手牌/副露/牌河公共模型与和牌形分析
- 可完整运行的大众麻将吃碰杠胡、番型结算与基础 AI
- 可完整运行的四川麻将换三张、定缺、血战、刮风下雨、流局检查与基础 AI
- 可完整运行的四人日式立直麻将东风战、振听、宝牌、符番结算与基础 AI
- 三种麻将共用的应用/表现契约、合法操作选项与独立会话适配器
- xUnit 自动测试
- 960×540 逻辑画布下的大厅、可玩斗地主牌桌和三种可玩麻将桌
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

直接试玩可运行 `./scripts/godot.sh --path .`。大厅中的斗地主、大众麻将、四川血战和四人日麻均可完整玩到结算；点击真实手牌后使用“出牌”或“换三张”，鸣牌、定缺、立直弃牌和其他选项会直接按规则引擎的合法操作列出，也可以使用“提示”或“托管”。斗地主未完成牌局会自动保存并在下次进入时恢复；麻将恢复在下一阶段统一补齐。

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
│   │   ├── 斗地主规则.md           # 斗地主已冻结牌型与流程规则
│   │   ├── 大众麻将规则.md         # 大众麻将吃碰杠胡与结算规则
│   │   ├── 四川麻将规则.md         # 换三张、定缺、血战与流局规则
│   │   └── 日式立直麻将规则.md     # 东风战、振听、役种、符番与结算规则
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
│   │   ├── doudizhu/               # 斗地主可玩牌桌与结算界面
│   │   └── mahjong/                # 四人麻将桌面交互灰盒
│   └── scripts/
│       ├── doudizhu/               # 斗地主会话、手牌交互、AI 节拍与结算表现
│       ├── mahjong/                # 麻将桌面标定、透明 3D 牌层与交互
│       └── Bootstrap.cs            # 启动、本地档案与场景切换
├── scripts/
│   ├── bootstrap-env.sh            # 安装项目本地工具链
│   ├── check-docs.sh               # 文档治理检查
│   ├── check-gitignore.sh           # 忽略与必跟踪文件检查
│   ├── dotnet.sh                   # 项目本地 .NET 入口
│   ├── export-windows.sh           # Windows x64 导出
│   ├── godot.sh                    # 项目本地 Godot 入口
│   ├── setup-git-hooks.sh          # 启用版本化提交钩子
│   ├── smoke-mahjong.sh            # 三种麻将真实 Godot 全局托管烟测
│   └── verify.sh                   # 构建、测试与启动验证
├── src/
│   ├── Game.Core/                  # 通用命令、事件和确定性随机数
│   ├── Game.Application/
│   │   ├── Doudizhu/              # 斗地主玩家会话、AI 调度与命令重放恢复
│   │   ├── Mahjong/               # 麻将公共表现数据与三种独立会话适配器
│   │   ├── Profiles/               # 本地豆子、战绩与版本化 JSON 档案
│   │   └── Sessions/               # 通用对局会话边界
│   ├── Game.Doudizhu/
│   │   ├── AI/                     # 只读观察驱动的基础斗地主 AI
│   │   ├── Cards/                  # 扑克牌、牌点、牌堆与洗牌
│   │   ├── Commands/               # 叫抢、出牌与不出命令
│   │   ├── Events/                 # 已接受斗地主操作事件
│   │   ├── Moves/                  # 合法出牌值与枚举
│   │   ├── Patterns/               # 牌型识别与比较
│   │   ├── Settlement/             # 倍数、春天与三方零和结算
│   │   └── State/                  # 发牌、叫抢和出牌状态机
│   ├── Game.Mahjong/
│   │   ├── Analysis/               # 普通形、七对、国士与听牌分析
│   │   ├── Commands/               # 麻将通用摸打鸣牌命令框架
│   │   ├── Hands/                  # 手牌与吃碰杠副露
│   │   ├── Table/                  # 座位、牌墙、牌河与桌面状态原语
│   │   └── Tiles/                  # 34 种语义牌及 108/136 张实体牌组
│   ├── Game.Mahjong.Standard/      # 大众麻将状态机、番型、结算与 AI
│   ├── Game.Mahjong.Sichuan/       # 四川血战状态机、番型、结算与 AI
│   └── Game.Mahjong.Riichi/        # 日麻东风战、振听、符番、宝牌与 AI
├── tests/
│   ├── Game.Application.Tests/     # 档案、经济、恢复与麻将会话边界测试
│   ├── Game.Core.Tests/
│   ├── Game.Doudizhu.Tests/        # 牌堆、牌型、比较与合法出牌测试
│   ├── Game.Mahjong.Tests/         # 麻将公共牌、桌面与和牌形测试
│   ├── Game.Mahjong.Standard.Tests/ # 大众麻将规则与模拟测试
│   ├── Game.Mahjong.Sichuan.Tests/ # 四川麻将规则与模拟测试
│   └── Game.Mahjong.Riichi.Tests/  # 日麻规则、计分与整场模拟测试
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
