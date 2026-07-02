# CODELY.md — ProjectBroCallBack

## Project Overview

**ProjectBroCallBack** 是一款以猫为主题的肉鸽自动战斗游戏，灵感来源于《杀戮尖塔》。玩家在 3 个地区（每个地区 15 关，共 45 关）中推进，每关核心循环：战斗准备 → 战斗 → 构筑 → 选关。

- **引擎**：Unity 2022.3.62f2c1 (LTS) / Tuanjie 团结引擎
- **语言**：C# 9.0 / .NET Standard 2.1
- **构建目标**：StandaloneWindows64
- **代码与文档语言**：中文
- **渲染管线**：Built-in（Legacy）
- **资源系统**：Unity Addressable Asset System

## Key Scenes & Entry Point

| 场景文件 | 说明 |
|----------|------|
| `Assets/Scenes/Main.unity` | 唯一场景，所有内容运行时动态加载 |

场景中挂载 `DemoStarter`（ MonoBehaviour），其 `Start()` 调用 `GameManager.Instance.LoadGame()` 触发初始化链。

## Core Assets

### Prefabs
- `Assets/Prefabs/Battle/Fighters/BattleFighter_Base.prefab` — 战斗单位基座预制体（项目中唯一的预制体）

### 资源目录 (`Assets/Bundle/`)
| 子目录 | 内容 |
|--------|------|
| `2DEffect/` | 2D 特效 |
| `Audio/` | 音频（BGM/SFX） |
| `AvatarTemp/` | 角色立绘/序列帧动画 |
| `Data/Avatar/` | AvatarAnimationDefinition 资源（角色动画定义） |
| `Font/` | 字体（`FZY3K_GBK`） |
| `Map/` | 地图相关资源 |
| `UI/` | UI 资源 |

### 配置文件 (`Assets/StreamingAssets/Tables/`)
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
| `relic_config.json` | 圣物配置 |
| `choice_config.json` | 抉择事件配置 |
| `hot_spring_config.json` | 温泉配置 |
| `cross_tribe_synergy_config.json` | 跨族群协同配置 |
| `quality_config.json` | 品质配置 |
| `rarity_spawn_config.json` | 稀有度生成配置 |
| `affix_draw_table.json` | 词缀抽取表 |
| `cat_stats_table.json` | 猫属性表 |

## Design Documents (需求唯一来源)

所有功能需求以 `正式文档/` 目录下的设计文档为准。实现任何功能前，必须先阅读对应的设计文档。

| 文档 | 内容 |
|------|------|
| `01_基础_游戏流程.md` | 游戏整体流程 |
| `02_基础_数值流向.md` | 数值体系 |
| `03_基础_战斗技能Buff系统.md` | 战斗与 Buff 系统设计 |
| `100_系统_选关.md` | 选关系统 |
| `101_系统_战斗准备.md` | 战斗准备系统 |
| `102_系统_战斗.md` | 战斗系统 |
| `103_系统_招募.md` | 招募系统 |
| `104_系统_猫市.md` | 猫市（商店）系统 |
| `105_系统_命运.md` | 命运系统 |
| `106_系统_抉择.md` | 抉择系统 |
| `201_物品_奇物设计.md` | 奇物设计 |
| `202_物品_消耗品设计.md` | 消耗品设计 |
| `301_技能_buff设计.md` | 技能与 Buff 设计 |
| `401_单位_兵种设计.md` | 兵种设计 |
| `更新日志_系统实现.md` | 系统实现更新日志 |

## Architecture

### 目录结构与命名空间

```
Assets/Scripts/
  Framework/               — 框架层（全局命名空间）
    GameManager.cs            游戏总管理器（单例，挂载所有子管理器）
    ResourceManager.cs        Addressable 资源加载/缓存/卸载
    UIManager.cs              UI 面板管理（动态创建，无预制体）
    AudioManager.cs           音频管理
    DataManager.cs            玩家数据持久化（JSON）
    CurrencyManager.cs        货币系统（通过 ICurrencyStorage 解耦）
    SceneManager.cs           场景加载
    GameLogger.cs             日志系统（文件写入 + 总开关）
  Game/
    GameFlowController.cs     游戏状态机（核心流程控制）
    GameInitializer.cs        启动初始化协程
    Camp/                     namespace Camp — 战斗外管理
      Defines/                  共享枚举、属性、数据模型
      Buff/                     Buff 子系统
      Config/                   配置加载（TribeConfigLoader）
      Map/                      地图生成
      *.cs                      平铺服务类（RoundManager, TribeZoneService 等）
    Combat/                  namespace Combat — 战斗内
      Avatar/                    namespace Combat.Avatar
      Fighter/                   namespace Combat.Fighter
      BattleManager.cs           战斗管理器（~1100 行，最大文件）
      BattleSimulation.cs        战斗模拟
      BattleFlowController.cs    战斗流程控制
      BattleCampaignRuntime.cs   战役运行时数据
    EffectSystem/            namespace Camp — GameEffect 枚举
    Legacy/                  namespace Legacy — 局外/元进度
  UI/
    UIPanel.cs               面板基类
    UIManager.cs              面板管理器
    Panels/                   MainPanel, VictoryPanel, BattleResultPanel 等
    TribeBuild/               TribeBuildPanel, HotSpringPanel, BattlePreparePanel
    Map/                      MapPanel
  DemoStarter.cs             场景入口
Assets/Editor/
  BundleBuilder.cs           AssetBundle 构建/清理工具
  AddressableAssetsBuilder.cs Addressable 自动分组/构建工具
  SettingsPanelPrefabCreator.cs
  CopyFullPathMenu.cs
  CopyPathShortcut.cs
```

### 三个领域

| 领域 | 命名空间 | 目录 | 说明 |
|------|----------|------|------|
| 战斗外（Camp） | `Camp` | `Game/Camp/` | 两次战斗间管理：三区、地图、招募、Buff、配置、数据模型 |
| 战斗内（Combat） | `Combat` | `Game/Combat/` | 即时战斗：BattleManager、BattleSimulation、Fighter、Avatar |
| 局外（Legacy） | `Legacy` | `Game/Legacy/` | 跨局元进度：声望系统 |

### 管理器生命周期

1. 每个管理器实现 `Initialize()` 方法，由 `GameManager.InitializeManagers()` 按依赖顺序调用
2. 管理器作为组件添加到 GameManager 所在的 GameObject 上（`AddComponent<T>()`）
3. 非管理器类使用纯 C# 构造（如 `RoundManager`、`MapGenerator`、`TribeZoneService`）
4. `GameManager`、`GameFlowController` 使用单例 + `DontDestroyOnLoad`

### 初始化链

```
DemoStarter.Start()
  → GameManager.Instance.LoadGame()
    → GameManager.Awake()                    (单例 + DontDestroyOnLoad)
      → GameLogger.Initialize()               (最先)
      → ResourceManager.Initialize()           (Addressable)
      → DataManager.Initialize()              (持久化路径)
      → AudioManager.Initialize()
      → SceneManager.Initialize()
      → UIManager.Initialize()                (Canvas + EventSystem)
      → BattleCampaignRuntime()               (new)
      → GameFlowController (AddComponent)
    → DataManager.LoadPlayerData()
    → TribeConfigLoader.Instance.LoadAllConfigs()  (StreamingAssets/Tables/)
    → GameFlowController.Initialize()          (状态机接管)
```

### 游戏状态机（`GameFlowController`）

```
Uninitialized → MapSelection → RoundPreparation → BattlePhase → GameOver
                      ↑                                    │
                      └────────────────────────────────────┘
                        (战斗胜利 → 构筑 → 选关循环)
```

5 个状态：
- `Uninitialized` — 未初始化
- `MapSelection` — 选关（地图选择）
- `RoundPreparation` — 回合准备（族群构筑、命运、抉择、猫市）
- `BattlePhase` — 战斗阶段
- `GameOver` — 游戏结束

新游戏阶段应作为状态机扩展接入（增加状态枚举和转换方法）。

### 资源加载

- 统一通过 `GameManager.Instance.ResourceManager` 加载
- 地址规范化规则：去掉 `assets/bundle/` 前缀 → 去掉文件扩展名 → 全小写
- 示例：`Assets/Bundle/UI/MainPanel.prefab` → 地址 `ui/mainpanel`
- 支持 `LoadResource<T>(address)` 同步和 `LoadResourceAsync<T>()` 异步
- 内部维护引用计数和缓存，通过 `UnloadResource(address)` 释放

### 数据持久化

- 通过 `DataManager` 以 JSON 存取
- 存档路径：`persistentDataPath/PlayerData/playerdata.json`
- 使用 `JsonUtility` 序列化 `PlayerData`
- JSON 配置文件使用 `LitJson` 库解析（非 `JsonUtility`）

### UI 系统

- **动态创建**：`new GameObject()` + `AddComponent<Image/Text/Button>()`，不使用预制体
- 面板基类：`UIPanel`（`Initialize/Show/Hide/Close` 及动画版本）
- 面板显示：`UIManager.ShowPanel<T>(UILayer)` 动态创建
- UI 层级：Background / Normal / Top / PopUp / Alert
- Canvas：ScreenSpaceOverlay，1920×1080
- 字体：统一使用 `FZY3K_GBK`，通过 `ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk")` 加载
- `Button` 内的 `Text` 组件必须设置 `raycastTarget = false`

### 日志系统

- 使用 `GameLogger.Log(tag, msg)` 而非 `Debug.Log`
- Tag 约定：`GM`(GameManager) / `GFC`(GameFlowController) / `UIP`(UIPanel) / `Data`(DataManager) / `UIM`(UIManager) / `Init`(GameInitializer) / 各面板缩写
- 总开关：`GameLogger.Enabled = false` 可全局关闭
- 日志文件：`persistentDataPath/Logs/game_yyyyMMdd_HHmmss.log`
  - Windows 路径：`C:\Users\<用户名>\AppData\LocalLow\<公司名>\<项目名>\Logs\`
  - **排查 bug 时必须先读日志文件**

## Building & Running

### 编辑器运行
1. 用 Unity 2022.3.62f2c1 打开项目
2. 打开 `Assets/Scenes/Main.unity`
3. 按 **Play**

### 资源构建工具（Editor 菜单）

| 菜单项 | 说明 |
|--------|------|
| `Tools > Build AssetBundles` | 构建 AssetBundle 到 `StreamingAssets/AssetBundles` |
| `Tools > Clear AssetBundles` | 清理已构建的 Bundle |
| `Tools > Show AssetBundles Info` | 列出已构建的 Bundle 文件 |
| `Tools > Addressable > Auto Setup All Resources` | 扫描 `Assets/Bundle/` 并自动配置 Addressable 分组 |
| `Tools > Addressable > Build Catalogs` | 构建 Addressable Catalogs |
| `Tools > Addressable > Clear All Resources` | 清空所有 Addressable 资源 |
| `Tools > Addressable > Show Addressable Groups Info` | 列出 Addressable 分组信息 |

### Addressable 资源分组

| 文件夹 | 组名 |
|--------|------|
| `Assets/Bundle/UI/` | `ui` |
| `Assets/Bundle/Audio/BGM/` | `audio_bgm` |
| `Assets/Bundle/Audio/SFX/` | `audio_sfx` |
| `Assets/Bundle/AvatarTemp/` | `avatar_temp` |
| `Assets/Bundle/Data/` | `data` |
| `Assets/Bundle/2DEffect/` | `2deffect` |
| `Assets/Bundle/Font/` | `font` |
| `Assets/Bundle/Map/` | `map` |

### CLI/Batchmode 构建

```bash
Unity -batchmode -quit -projectPath . \
       -buildTarget StandaloneWindows64 -logFile build.log
```

> 注意：项目无自定义 `BuildScript.cs`，使用 Unity 默认构建流程。

## Development Conventions

### 核心规则

1. **需求唯一来源**：`正式文档/` 目录下的设计文档。所有功能的设计、数值、流程以这些文档为准，不得自行编造或假设
2. **UI 构建方式**：使用代码动态创建 GameObject 和组件，不依赖预制体拖拽
3. **状态机驱动**：整个游戏在 `GameFlowController` 状态机管理下运行，新功能应作为状态机的一部分接入
4. **遵循 Framework 规范**：所有实现必须遵守 `Assets/Scripts/Framework/` 中已有代码的架构要求和编码约定
5. **不过度判空**：不要写过多的 null 检查和保护逻辑，让问题尽早暴露（NullReferenceException 比静默失败更容易定位）。只在外部输入、资源加载等真正不可控的边界处判空
6. **查 bug 必看日志**：排查 bug 时必须先读取日志文件，以日志内容为重要依据

### 命名约定

- 脚本类名：`PascalCase`（如 `GameManager`、`BattleFlowController`）
- 命名空间：与目录结构对应（`Camp`、`Combat`、`Combat.Avatar`、`Combat.Fighter`、`Legacy`）
- 配置文件：`snake_case`（如 `fighter_config.json`）
- Addressable 地址：全小写，去前缀和扩展名

### 数据类型约定

- 持久化字段（`PlayerData`、`FighterData` 等）中枚举值存为 `int`
- 代码中比较时需 cast：`(TribeType)fighter.tribeType`、`(BuffApplyType)choice.buffApplyType`
- `BuffEffectItem.statType` 是 `string` 类型（非 `StatType` 枚举），用 `eff.GetStatType()` 转换

### 接口解耦

- `CurrencyManager` 通过 `ICurrencyStorage` 接口与 `DataManager` 解耦
- `IHasBuffs` 接口供 `FighterData` 等实现 Buff 操作

### 第三方库

| 库 | 位置 | 用途 |
|----|------|------|
| LitJson | `Assets/ThirdParty/LitJson/` | JSON 配置文件解析 |

### 无 Assembly Definition

项目未使用 `*.asmdef` 文件，所有脚本在默认程序集内编译。

## Package & Dependency List

| 包 | 版本 | 用途 |
|----|------|------|
| `com.unity.addressables` | 1.22.3 | Addressable 资源系统 |
| `com.unity.textmeshpro` | 3.0.7 | 文本渲染 |
| `com.unity.timeline` | 1.7.7 | 时间线 |
| `com.unity.ugui` | 1.0.0 | UI 系统 |
| `com.unity.visualscripting` | 1.9.4 | 可视化脚本 |
| `com.unity.feature.development` | 1.0.1 | 开发工具集 |
| `com.unity.collab-proxy` | 2.12.4 | 版本控制协作 |
| `cn.tuanjie.codely.bridge` | 1.0.63 | Tuanjie/Codely 桥接 |
| `com.unity.modules.*` | 1.0.0 | Unity 内置模块（AI、Animation、Audio、Physics、Particles、UI 等） |

## Version Control

### 应纳入版本控制
- `Assets/`（源代码、资源、配置、meta 文件）
- `ProjectSettings/`
- `Packages/manifest.json`
- `正式文档/`
- `CLAUDE.md`、`CODELY.md`

### 应忽略（已在 .gitignore 中配置）
- `Library/`、`Temp/`、`obj/`、`Build/`、`Builds/`
- `Logs/`、`UserSettings/`
- `*.csproj`、`*.sln`、`*.user`
- `*.apk`、`*.aab`、`*.unitypackage`
- `Assets/AddressableAssetsData/*/*.bin*`（Packed Addressables）
- `Assets/StreamingAssets/aa/*`（Addressable 运行时数据）

## Game Startup Flow

- 无初始族群选择，新游戏直接通过 `SetupDefaultStartUnits()` 给默认角色：橘猫族长(2000) + 矛猫(1001)
- 初始化完成后进入 `MapSelection`，显示地图面板
- 每关循环：选关 → 回合准备（TribeBuildPanel）→ 战斗 → 结算 → 招募 → 构筑 → 回到选关
- Boss 关：全员上阵，胜利后获得稀有兵种三选一 + Boss 圣物，然后切到下一地区

## TODO / Open Questions

- 无自定义构建脚本（`BuildScript.cs`），CLI 批量构建使用 Unity 默认流程
- 无测试框架配置（未见 Unity Test Framework 或 Play Mode/Edit Mode 测试文件）
- 无 CI/CD 配置
