# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在本仓库中工作时提供指引。

## 核心规则

- **需求唯一来源**：`正式文档/` 目录下的设计文档。所有功能的设计、数值、流程以这些文档为准，不得自行编造或假设
- **UI 构建方式**：使用代码动态创建 GameObject 和组件（`new GameObject()` + `AddComponent<T>()`），不依赖预制体拖拽。参考 `UIManager` 中 Canvas/EventSystem/层级的创建方式
- **状态机驱动**：整个游戏必须在 `GameFlowController` 状态机的管理下运行。`GameFlowController.cs` 和 `GameInitializer.cs` 是可参考、可修改的核心文件。所有游戏流程都由状态机统一调度，新功能应作为状态机的一部分接入
- **遵循 Framework 规范**：所有实现必须遵守 `Assets/Scripts/Framework/` 中已有代码的架构要求和编码约定
- **不过度判空**：不要写过多的 null 检查和保护逻辑，让问题尽早暴露（NullReferenceException 比静默失败更容易定位）。只在外部输入、资源加载等真正不可控的边界处判空
- **查 bug 必看日志**：游戏运行日志文件位于 `persistentDataPath/Logs/` 目录下（Windows 实际路径为 `C:\Users\<用户名>\AppData\LocalLow\<公司名>\<项目名>\Logs\`），文件名格式 `game_yyyyMMdd_HHmmss.log`。排查 bug 时必须先读取日志文件，以日志内容为重要依据

## 项目概述

**ProjectBroCallBack** 是一款以猫为主题的肉鸽自动战斗游戏，灵感来源于《杀戮尖塔》。玩家在 3 个地区（每个地区 15 关，共 45 关）中推进，每关核心循环：战斗准备 → 战斗 → 构筑 → 选关。

- **引擎**：Unity 2022.3.62f3c1 (LTS)
- **语言**：C# 9.0 / .NET Standard 2.1
- **构建目标**：StandaloneWindows64
- **代码与文档语言**：中文

## 构建与开发命令

- **Tools > Build AssetBundles** — 构建 AssetBundle 到 StreamingAssets
- **Tools > Clear AssetBundles** — 清理已构建的 Bundle
- **Tools > Show AssetBundles Info** — 列出已构建的 Bundle 文件
- **Tools > Addressable > Auto Setup All Resources** — 扫描 `Assets/Bundle/` 并自动配置 Addressable 分组

资源管线：项目使用 Unity **Addressable Asset System**。所有游戏资源位于 `Assets/Bundle/` 下。JSON 配置位于 `Assets/StreamingAssets/Tables/`。

## 架构

### 目录结构与命名空间

```
Assets/Scripts/
  Framework/          — 框架层（全局命名空间）
  Game/
    Camp/             — namespace Camp（战斗外/局内管理）
      Defines/        — 共享枚举、属性、数据模型
      Buff/           — Buff 子系统
      Config/         — 配置加载（TribeConfigLoader）
      Map/            — 地图生成
      *.cs            — 平铺的服务类（RoundManager, TribeZoneService, etc.）
    Combat/           — namespace Combat（战斗内）
      Avatar/         — namespace Combat.Avatar
      Fighter/        — namespace Combat.Fighter
    EffectSystem/     — namespace Camp（GameEffect 枚举）
    Legacy/           — namespace Legacy（局外/元进度）
    GameFlowController.cs
    GameInitializer.cs
  UI/
    Panels/           — MainPanel, VictoryPanel
    TribeBuild/       — TribeBuildPanel, HotSpringPanel, BattlePreparePanel
    Map/              — MapPanel
```

### 三个领域

| 领域 | 命名空间 | 目录 | 说明 |
|------|----------|------|------|
| 战斗外（Camp） | `Camp` | `Game/Camp/` | 两次战斗间的管理系统：三区、地图、招募、Buff、配置、数据模型 |
| 战斗内（Combat） | `Combat` | `Game/Combat/` | 即时战斗：BattleManager、BattleSimulation、Fighter、Avatar |
| 局外（Legacy） | `Legacy` | `Game/Legacy/` | 跨局元进度：声望系统 |

### Framework 编码约定（必须遵守）

1. **管理器生命周期**：每个管理器实现 `Initialize()` 方法，由 `GameManager.InitializeManagers()` 按依赖顺序调用
2. **管理器挂载方式**：作为组件添加到 GameManager 所在的 GameObject 上（`AddComponent<T>()`），非管理器类使用纯 C# 构造
3. **单例模式**：`GameManager`、`GameFlowController`、`SceneManager` 使用单例 + `DontDestroyOnLoad`
4. **资源加载**：统一通过 `GameManager.Instance.ResourceManager` 加载，地址自动规范化（小写、去前缀、去扩展名）
5. **数据持久化**：通过 `DataManager` 以 JSON 存取 `persistentDataPath/PlayerData/playerdata.json`
6. **接口解耦**：`CurrencyManager` 通过 `ICurrencyStorage` 与 `DataManager` 解耦
7. **配置加载**：`TribeConfigLoader`（单例）从 `StreamingAssets/Tables/` 加载 JSON 配置，使用 `LitJson` 解析

### 初始化链

`GameInitializer.Start()` → 协程等待 → `GameManager` 就绪 → `DataManager.LoadPlayerData()` → `TribeConfigLoader.LoadAllConfigs()` → `GameFlowController.Initialize()` 接管

### 游戏状态机（`GameFlowController`）

5 个状态：`Uninitialized` → `MapSelection` → `RoundPreparation` → `BattlePhase` → `GameOver`

新游戏阶段应作为状态机扩展接入（增加状态枚举和转换方法）。

### UI 系统

- 动态创建：`new GameObject()` + `AddComponent<Image/Text/Button>()`，不使用预制体
- 面板基类：`UIPanel`（`Initialize/Show/Hide/Close` 及动画版本），子类自动继承基类日志
- 面板显示：`UIManager.ShowPanel<T>(UILayer)` 动态创建，无需 Addressable 地址
- UI 层级：Background / Normal / Top / PopUp / Alert
- Canvas：ScreenSpaceOverlay，1920x1080
- 字体：统一使用 `FZY3K_GBK`，通过 `GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk")` 加载，禁止使用 `Resources.GetBuiltinResource<Font>`
- 按钮文字：所有 `Button` 内的 `Text` 组件必须设置 `raycastTarget = false`，防止拦截点击
- `CreateButton` 方法返回 `RectTransform`（非 `Button`），调用方直接用 `.anchoredPosition` 定位

### 日志系统

- 使用 `GameLogger.Log(tag, msg)` 而非 `Debug.Log`，tag 为短缩写
- 总开关：`GameLogger.Enabled = false` 可全局关闭
- 日志写入文件：`persistentDataPath/Logs/game_yyyyMMdd_HHmmss.log`
  - Windows 实际路径：`C:\Users\<用户名>\AppData\LocalLow\<公司名>\<项目名>\Logs\`
  - **排查 bug 时必须先读日志**，以日志内容为依据定位问题
- Tag 约定：`GM`(GameManager) / `GFC`(GameFlowController) / `UIP`(UIPanel) / `Data`(DataManager) / `UIM`(UIManager) / `Init`(GameInitializer) / 各面板缩写

### 游戏启动流程

- 无初始族群选择，新游戏直接通过 `SetupDefaultStartUnits()` 给默认角色：橘猫族长(2000) + 矛猫(1001)
- 初始化链：`GameManager.Awake()` → `GameLogger.Initialize()` → 各管理器 → `GameInitializer.Start()` → 加载配置 → `GameFlowController.Initialize()`

### 数据类型约定

- 持久化字段（`PlayerData`、`FighterData` 等）中枚举值存为 `int`
- 代码中比较时需 cast：`(TribeType)fighter.tribeType`、`(BuffApplyType)choice.buffApplyType` 等
- `BuffEffectItem.statType` 是 `string` 类型（非 `StatType` 枚举），用 `eff.GetStatType()` 转换

## 配置文件（`StreamingAssets/Tables/`）

| 文件 | 内容 |
|------|------|
| `fighter_config.json` | 战斗单位定义（属性、天赋 Buff、标签） |
| `buff_config.json` | Buff 定义（效果、参数、gameEffectType） |
| `tribe_config.json` | 4 个族群定义（族长、兵种、部署费用） |
| `tribe_aura_config.json` | 族群光环配置（每族每级选项） |
| `affix_config.json` | 词缀系统（撸铁） |
| `levels_config.json` | 45 关配置（敌人、场景、难度） |
| `artifact_config.json` | 20 个奇物定义 |
| `shop_config.json` | 商店定价 |
| `map_config.json` | 地图生成参数 |
| `status_effect_config.json` | 状态效果参数（毒、流血、燃烧等） |
| `ritual_config.json` | 命运/祈福配置 |
| `recruitment_config.json` | 招募配置 |
| `leader_skill_config.json` | 领袖技能 |

## 设计文档（需求唯一来源）

所有功能需求以 `正式文档/` 目录下的设计文档为准。实现任何功能前，先阅读对应的设计文档。

## 注意事项

- 单场景（`Assets/Scenes/Main.unity`），所有内容动态加载
- `BattleManager.cs` 是最大的文件（约 1100 行）
- JSON 配置使用 `LitJson` 库解析（非 `JsonUtility`）
- 实现任何功能前，先阅读 `正式文档/` 中对应的设计文档，确认需求细节
