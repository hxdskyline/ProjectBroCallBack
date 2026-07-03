using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Camp;
using Combat;
using Combat.Avatar;
using UI.TribeBuild;

/// <summary>
/// 战斗准备面板 — 战场预览 + 底部单位栏 + 人口格 + 拖放部署
/// </summary>
public class BattlePreparePanel : UIPanel
{
    private int _battleNumber;
    private MapNodeType _nodeType;
    private bool _isBossBattle;

    // 战场
    private BattleFlowController _prepBattleFlow;
    private Combat.BattleManager _prepBattleManager;

    // 部署数据
    private List<DeployedUnitEntry> _deployedUnits = new List<DeployedUnitEntry>();
    private int _usedPopulation;
    private int _populationCap;
    private int _draggingPopulation; // 正在拖拽的人口

    // UI 引用
    private RectTransform _populationBar;
    private RectTransform _unitScrollView;
    private RectTransform _unitContent;
    private List<RectTransform> _populationSlots = new List<RectTransform>();
    private List<UnitSlotDragHandler> _unitSlots = new List<UnitSlotDragHandler>();

    private struct DeployedUnitEntry
    {
        public FighterData unitData;
        public FighterConfig config;
        public Vector3 worldPosition;
        public GameObject previewObject;
        public UnitSlotDragHandler slotHandler;
    }

    public override void Initialize()
    {
        base.Initialize();

        // 半透明背景（只覆盖底部区域）
        var bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0f); // 完全透明，让战场可见

        // 顶部标题栏
        CreateTopBar();

        // 人口格栏
        CreatePopulationBar();

        // 底部单位栏（支持横向滑动）
        CreateUnitBar();

        // 按钮
        var startBtn = CreateButton("StartButton", "开始战斗", OnStartBattle);
        startBtn.anchoredPosition = new Vector2(150, -470);

        var backBtn = CreateButton("BackButton", "返回", OnBack);
        backBtn.anchoredPosition = new Vector2(-150, -470);
    }

    public void Setup(int battleNumber, MapNodeType nodeType)
    {
        _battleNumber = battleNumber;
        _nodeType = nodeType;
        _isBossBattle = nodeType == MapNodeType.Boss;
        _usedPopulation = 0;
        _draggingPopulation = 0;
        _deployedUnits.Clear();

        _populationCap = GameManager.Instance.DataManager.GetPopulationCap();
        if (_isBossBattle) _populationCap = 999;

        InitializeBattlefield();
        RefreshPopulationBar();
        RefreshUnitBar();
        UpdateTopInfo();
    }

    // ====== 战场初始化 ======

    private void InitializeBattlefield()
    {
        // 清理旧的战场
        if (_prepBattleFlow != null)
        {
            _prepBattleFlow.StopAndDispose(null);
            _prepBattleFlow = null;
        }

        var campaign = GameManager.Instance.BattleCampaignRuntime;
        // 优先使用节点预生成的敌人
        int[] enemyIds = null;
        var gfc = GameFlowController.Instance;
        bool useNodeEnemyIds = campaign.GetEnemyUnitVariantsForBattle(_battleNumber) == null;
        if (useNodeEnemyIds && gfc != null)
        {
            var node = gfc.GetCurrentMapNode();
            if (node?.enemyUnitIds != null && node.enemyUnitIds.Length > 0)
                enemyIds = node.enemyUnitIds;
        }
        if (enemyIds == null)
            enemyIds = campaign.GetEnemyUnitIdsForBattle(_battleNumber);
        var enemyStats = campaign.GetEnemyStats(_battleNumber, DifficultyLevel.Normal);
        var scenarios = campaign.GetScenarioOptions(_battleNumber);
        var scenario = scenarios.Count > 0 ? scenarios[0] : default;
        int enemyCount = enemyIds?.Length ?? 3;

        // 加载通用敌方 avatar（作为回退）
        var enemyAvatar = GameFlowController.Instance.LoadAvatarDefinition("enemy");

        // 尝试从 fighter_config 构建每个敌人的独立定义（支持混合敌人类型）
        Combat.Fighter.BattleFighterSpawnDefinition[] enemyDefs = null;
        if (enemyIds != null && enemyIds.Length > 0)
        {
            var defs = new List<Combat.Fighter.BattleFighterSpawnDefinition>();
            foreach (int id in enemyIds)
            {
                var cfg = Camp.TribeConfigLoader.Instance?.GetFighterConfig(id);
                if (cfg != null)
                {
                    // 尝试加载该兵种的专属 avatar，失败则用通用 enemy avatar
                    string address = $"data/avatar/definitions/{cfg.avatarId.ToLower()}_avataranimdef";
                    var avatar = GameManager.Instance.ResourceManager.LoadResource<AvatarAnimationDefinition>(address);
                    if (avatar == null)
                        avatar = enemyAvatar;
                    defs.Add(new Combat.Fighter.BattleFighterSpawnDefinition(
                        cfg.fighterName, cfg.ToStaticAttributes(), avatar,
                        1.0f, (Camp.TribeType)cfg.tribeType, cfg.fighterId));
                }
            }
            if (defs.Count == enemyIds.Length)
                enemyDefs = defs.ToArray();
        }

        _prepBattleFlow = new BattleFlowController();
        bool hasEnemyBillboard = GameManager.Instance.BattleCampaignRuntime.HasEnemyBillboardForBattle(_battleNumber);
        _prepBattleFlow.StartBattlePrepare(
            levelId: _battleNumber,
            enemyDefinition: enemyAvatar,
            enemyFighterCount: enemyCount,
            enemyStats: enemyDefs == null ? enemyStats : null,
            terrain: scenario.terrain,
            weather: scenario.weather,
            enemyDefinitions: enemyDefs,
            hasEnemyBillboard: hasEnemyBillboard);

        _prepBattleManager = _prepBattleFlow.BattleManager;
    }

    // ====== UI 创建 ======

    private void CreateTopBar()
    {
        // 顶部深色背景条
        var topBarGo = new GameObject("TopBar");
        topBarGo.transform.SetParent(transform, false);
        var topBarRect = topBarGo.AddComponent<RectTransform>();
        topBarRect.anchorMin = new Vector2(0, 1);
        topBarRect.anchorMax = new Vector2(1, 1);
        topBarRect.pivot = new Vector2(0.5f, 1);
        topBarRect.anchoredPosition = new Vector2(0, 0);
        topBarRect.sizeDelta = new Vector2(0, 60);
        var topBarImg = topBarGo.AddComponent<Image>();
        topBarImg.color = new Color(0, 0, 0, 0.7f);

        // 标题文字
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(topBarGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.sizeDelta = Vector2.zero;
        var titleTxt = titleGo.AddComponent<Text>();
        titleTxt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        titleTxt.fontSize = 28;
        titleTxt.color = Color.white;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.raycastTarget = false;
    }

    private void CreatePopulationBar()
    {
        // 人口格背景
        var popBarGo = new GameObject("PopulationBar");
        popBarGo.transform.SetParent(transform, false);
        _populationBar = popBarGo.AddComponent<RectTransform>();
        _populationBar.anchorMin = new Vector2(0, 0);
        _populationBar.anchorMax = new Vector2(1, 0);
        _populationBar.pivot = new Vector2(0.5f, 0);
        _populationBar.anchoredPosition = new Vector2(0, 240);
        _populationBar.sizeDelta = new Vector2(0, 40);
        var popBarImg = popBarGo.AddComponent<Image>();
        popBarImg.color = new Color(0, 0, 0, 0.6f);
    }

    private void CreateUnitBar()
    {
        // 底部单位栏背景
        var unitBarGo = new GameObject("UnitBar");
        unitBarGo.transform.SetParent(transform, false);
        var unitBarRect = unitBarGo.AddComponent<RectTransform>();
        unitBarRect.anchorMin = new Vector2(0, 0);
        unitBarRect.anchorMax = new Vector2(1, 0);
        unitBarRect.pivot = new Vector2(0.5f, 0);
        unitBarRect.anchoredPosition = new Vector2(0, 0);
        unitBarRect.sizeDelta = new Vector2(0, 200);
        var unitBarImg = unitBarGo.AddComponent<Image>();
        unitBarImg.color = new Color(0.1f, 0.1f, 0.2f, 0.85f);

        // ScrollRect 容器
        var viewGo = new GameObject("ScrollView");
        viewGo.transform.SetParent(unitBarGo.transform, false);
        _unitScrollView = viewGo.AddComponent<RectTransform>();
        _unitScrollView.anchorMin = Vector2.zero;
        _unitScrollView.anchorMax = Vector2.one;
        _unitScrollView.sizeDelta = new Vector2(-20, -20);
        _unitScrollView.anchoredPosition = new Vector2(0, 0);

        // Content
        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewGo.transform, false);
        _unitContent = contentGo.AddComponent<RectTransform>();
        _unitContent.anchorMin = new Vector2(0, 0.5f);
        _unitContent.anchorMax = new Vector2(0, 0.5f);
        _unitContent.pivot = new Vector2(0, 0.5f);
        _unitContent.anchoredPosition = new Vector2(0, 0);
        _unitContent.sizeDelta = new Vector2(0, 150);

        // ScrollRect
        var scroll = unitBarGo.AddComponent<ScrollRect>();
        scroll.viewport = _unitScrollView;
        scroll.content = _unitContent;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    // ====== 刷新 UI ======

    private void UpdateTopInfo()
    {
        var titleTxt = transform.Find("TopBar/TitleText")?.GetComponent<Text>();
        if (titleTxt != null)
        {
            string title = GetTitleText();
            var campaign = GameManager.Instance.BattleCampaignRuntime;
            var scenarios = campaign.GetScenarioOptions(_battleNumber);
            if (scenarios.Count > 0)
            {
                var s = scenarios[0];
                title += $"   地形: {s.terrain}  天气: {s.weather}";
            }
            titleTxt.text = title;
        }
    }

    private void RefreshPopulationBar()
    {
        // 清除旧格子
        for (int i = _populationBar.childCount - 1; i >= 0; i--)
            Destroy(_populationBar.GetChild(i).gameObject);
        _populationSlots.Clear();

        // 计算布局
        int slots = _isBossBattle ? Mathf.Min(_populationCap, 20) : _populationCap;
        float slotSize = 36f;
        float gap = 4f;
        float totalWidth = slots * (slotSize + gap) - gap;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < slots; i++)
        {
            var slotGo = new GameObject($"PopSlot_{i}");
            slotGo.transform.SetParent(_populationBar, false);
            var slotRect = slotGo.AddComponent<RectTransform>();
            slotRect.pivot = new Vector2(0, 0.5f);
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2(startX + i * (slotSize + gap), 0);
            slotRect.sizeDelta = new Vector2(slotSize, slotSize);

            var slotImg = slotGo.AddComponent<Image>();
            slotImg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f); // 空格子

            _populationSlots.Add(slotRect);
        }

        UpdatePopulationDisplay();
    }

    private void RefreshUnitBar()
    {
        // 清除旧单位槽
        for (int i = _unitContent.childCount - 1; i >= 0; i--)
            Destroy(_unitContent.GetChild(i).gameObject);
        _unitSlots.Clear();

        var dataManager = GameManager.Instance?.DataManager;
        if (dataManager == null) return;

        var tribes = dataManager.GetTribes();
        float slotWidth = 120f;
        float slotHeight = 140f;
        float gap = 10f;
        int index = 0;

        foreach (var tribe in tribes)
        {
            foreach (var unit in tribe.units)
            {
                var zone = unit.GetZone();
                // 显示待上阵和已上阵的单位（不显示生产区）
                if (zone != UnitZone.Deployed && zone != UnitZone.Standby) continue;

                var config = TribeConfigLoader.Instance.GetFighterConfig(unit.fighterId);
                if (config == null) continue;

                CreateUnitSlot(unit, config, index, slotWidth, slotHeight, gap);
                index++;
            }
        }

        // 更新 Content 宽度
        float contentWidth = Mathf.Max(1920f, index * (slotWidth + gap) + gap);
        _unitContent.sizeDelta = new Vector2(contentWidth, 150);
    }

    private void CreateUnitSlot(FighterData unit, FighterConfig config, int index, float slotWidth, float slotHeight, float gap)
    {
        var slotGo = new GameObject($"UnitSlot_{unit.fighterId}_{index}");
        slotGo.transform.SetParent(_unitContent, false);
        var slotRect = slotGo.AddComponent<RectTransform>();
        slotRect.pivot = new Vector2(0, 0.5f);
        slotRect.anchorMin = new Vector2(0, 0.5f);
        slotRect.anchorMax = new Vector2(0, 0.5f);
        slotRect.anchoredPosition = new Vector2(gap + index * (slotWidth + gap), 0);
        slotRect.sizeDelta = new Vector2(slotWidth, slotHeight);

        var slotImg = slotGo.AddComponent<Image>();
        slotImg.color = new Color(0.2f, 0.3f, 0.5f, 0.9f);

        // 头像
        var avatarGo = new GameObject("Avatar");
        avatarGo.transform.SetParent(slotGo.transform, false);
        var avatarRect = avatarGo.AddComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0, 0.3f);
        avatarRect.anchorMax = new Vector2(1, 1);
        avatarRect.sizeDelta = Vector2.zero;
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.color = Color.white;
        string spriteAddress = $"avatartemp/{config.avatarId}1";
        var sprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>(spriteAddress);
        if (sprite != null)
            avatarImg.sprite = sprite;

        // 名字
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(slotGo.transform, false);
        var nameRect = nameGo.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0.3f);
        nameRect.sizeDelta = Vector2.zero;
        var nameTxt = nameGo.AddComponent<Text>();
        nameTxt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        nameTxt.fontSize = 16;
        nameTxt.color = Color.white;
        nameTxt.alignment = TextAnchor.MiddleCenter;
        nameTxt.text = config.fighterName;
        nameTxt.raycastTarget = false;

        // 人口费角标（背景和文字分开，避免同 GameObject 上两个 Graphic）
        int popCost = config.populationCost > 0 ? config.populationCost : 1;
        var costBgGo = new GameObject("PopCostBg");
        costBgGo.transform.SetParent(slotGo.transform, false);
        var costBgRect = costBgGo.AddComponent<RectTransform>();
        costBgRect.anchorMin = new Vector2(1, 1);
        costBgRect.anchorMax = new Vector2(1, 1);
        costBgRect.pivot = new Vector2(1, 1);
        costBgRect.sizeDelta = new Vector2(30, 24);
        costBgRect.anchoredPosition = new Vector2(-4, -4);
        var costBgImg = costBgGo.AddComponent<Image>();
        costBgImg.color = new Color(0.8f, 0.6f, 0.1f);

        var costTxtGo = new GameObject("PopCostTxt");
        costTxtGo.transform.SetParent(costBgGo.transform, false);
        var costTxtRect = costTxtGo.AddComponent<RectTransform>();
        costTxtRect.anchorMin = Vector2.zero;
        costTxtRect.anchorMax = Vector2.one;
        costTxtRect.sizeDelta = Vector2.zero;
        var costTxt = costTxtGo.AddComponent<Text>();
        costTxt.font = GameManager.Instance?.ResourceManager?.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        costTxt.fontSize = 16;
        costTxt.color = Color.white;
        costTxt.alignment = TextAnchor.MiddleCenter;
        costTxt.text = popCost.ToString();
        costTxt.raycastTarget = false;

        // 拖放组件
        var handler = slotGo.AddComponent<UnitSlotDragHandler>();
        handler.Setup(unit, config, this);
        _unitSlots.Add(handler);
    }

    // ====== 人口管理 ======

    public bool CanDeployPopulation(int popCost)
    {
        return _usedPopulation + _draggingPopulation + popCost <= _populationCap;
    }

    public void OnDragStart(int popCost)
    {
        _draggingPopulation += popCost;
        UpdatePopulationDisplay();
    }

    /// <summary>
    /// 拖拽过程中实时更新区域高亮
    /// </summary>
    public void OnDragUpdate(Vector3 worldPos, FighterConfig config)
    {
        if (_prepBattleManager == null) return;

        int zone = Combat.BattleManager.GetDeployZone(worldPos);
        bool canDeploy = (zone & config.deployZones) != 0;
        _prepBattleManager.SetDragZoneHighlight(zone, canDeploy);
    }

    public void OnDragEnd()
    {
        _draggingPopulation = 0;
        UpdatePopulationDisplay();

        if (_prepBattleManager != null)
            _prepBattleManager.HideAllOverlays();
    }

    private void UpdatePopulationDisplay()
    {
        // 已部署人口的头像填充
        int slotIndex = 0;
        for (int i = 0; i < _deployedUnits.Count; i++)
        {
            int cost = _deployedUnits[i].config.populationCost > 0 ? _deployedUnits[i].config.populationCost : 1;
            for (int j = 0; j < cost && slotIndex < _populationSlots.Count; j++)
            {
                var slot = _populationSlots[slotIndex];
                var img = slot.GetComponent<Image>();
                if (img != null)
                {
                    img.color = new Color(0.6f, 0.9f, 1f, 0.9f);
                    // 设置头像
                    string spriteAddr = $"avatartemp/{_deployedUnits[i].config.avatarId}1";
                    var sp = GameManager.Instance.ResourceManager.LoadResource<Sprite>(spriteAddr);
                    if (sp != null) img.sprite = sp;
                }
                slotIndex++;
            }
        }

        // 拖拽中的人口（黄色占位）
        for (int i = 0; i < _draggingPopulation && slotIndex < _populationSlots.Count; i++)
        {
            var slot = _populationSlots[slotIndex];
            var img = slot.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0.8f, 0.8f, 0.2f, 0.7f);
                img.sprite = null;
            }
            slotIndex++;
        }

        // 剩余空格
        while (slotIndex < _populationSlots.Count)
        {
            var slot = _populationSlots[slotIndex];
            var img = slot.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                img.sprite = null;
            }
            slotIndex++;
        }
    }

    // ====== 部署管理 ======

    public void TryDeployUnit(FighterData unitData, FighterConfig config, Vector3 worldPos,
        UnitSlotDragHandler slotHandler, GameObject previewObj)
    {
        int cost = config.populationCost > 0 ? config.populationCost : 1;
        if (_usedPopulation + cost > _populationCap)
        {
            Destroy(previewObj);
            return;
        }

        _usedPopulation += cost;

        var entry = new DeployedUnitEntry
        {
            unitData = unitData,
            config = config,
            worldPosition = worldPos,
            previewObject = previewObj,
            slotHandler = slotHandler
        };
        _deployedUnits.Add(entry);

        // 隐藏底部栏槽位
        slotHandler.gameObject.SetActive(false);

        // 给预览对象添加交互功能（拖拽移动 + 单击取消）
        var clickHandler = previewObj.AddComponent<DeployedUnitHandler>();
        clickHandler.Setup(this, _deployedUnits.Count - 1, config);

        // 预览变为不透明（表示已放置）
        var sr = previewObj.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = new Color(0.6f, 0.9f, 1f, 1f);

        UpdatePopulationDisplay();
        GameLogger.Log("BattlePrep", $"部署 {config.fighterName} at ({worldPos.x:F1}, {worldPos.y:F1}) 人口:{cost}");
    }

    public void UpdateDeployedPosition(int entryIndex, Vector3 newPos)
    {
        if (entryIndex < 0 || entryIndex >= _deployedUnits.Count) return;
        var entry = _deployedUnits[entryIndex];
        entry.worldPosition = newPos;
        _deployedUnits[entryIndex] = entry;
    }

    public void UndeployUnit(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= _deployedUnits.Count) return;

        var entry = _deployedUnits[entryIndex];
        int cost = entry.config.populationCost > 0 ? entry.config.populationCost : 1;
        _usedPopulation -= cost;

        // 销毁世界预览
        if (entry.previewObject != null)
            Destroy(entry.previewObject);

        // 恢复底部栏槽位
        if (entry.slotHandler != null)
            entry.slotHandler.gameObject.SetActive(true);

        _deployedUnits.RemoveAt(entryIndex);

        // 重新编号所有 click handler
        for (int i = 0; i < _deployedUnits.Count; i++)
        {
            var handler = _deployedUnits[i].previewObject?.GetComponent<DeployedUnitHandler>();
            if (handler != null) handler.SetIndex(i);
        }

        UpdatePopulationDisplay();
        GameLogger.Log("BattlePrep", $"取消部署 {entry.config.fighterName}");
    }

    // ====== 按钮 ======

    private void OnStartBattle()
    {
        if (_deployedUnits.Count == 0)
        {
            GameLogger.Log("BattlePrep", "至少部署一个单位");
            return;
        }

        // 收集部署位置
        var deployedPositions = new List<(FighterData unit, Vector3 worldPos)>();
        foreach (var entry in _deployedUnits)
        {
            deployedPositions.Add((entry.unitData, entry.worldPosition));
        }

        GameLogger.Log("BattlePrep", $"StartBattle bn={_battleNumber} deployed={deployedPositions.Count}");

        // 销毁拖放预览（正式单位将由 BattleManager 创建）
        foreach (var entry in _deployedUnits)
        {
            if (entry.previewObject != null)
                Destroy(entry.previewObject);
        }
        _deployedUnits.Clear();

        GameFlowController.Instance.EnterBattlePhaseFromPreparation(
            _prepBattleFlow,
            _prepBattleManager,
            deployedPositions,
            _battleNumber);

        _prepBattleFlow = null;
        _prepBattleManager = null;

        Hide();
    }

    private void OnBack()
    {
        GameLogger.Log("BattlePrep", "Back");

        // 清理战场
        if (_prepBattleFlow != null)
        {
            _prepBattleFlow.StopAndDispose(null);
            _prepBattleFlow = null;
            _prepBattleManager = null;
        }

        // 清理拖放预览
        foreach (var entry in _deployedUnits)
        {
            if (entry.previewObject != null)
                Destroy(entry.previewObject);
        }
        _deployedUnits.Clear();

        Hide();
        var uiManager = GameManager.Instance?.UIManager;
        uiManager?.ShowPanel<TribeBuildPanel>(UIManager.UILayer.Normal);
    }

    public override void Hide()
    {
        base.Hide();
    }

    private string GetTitleText()
    {
        switch (_nodeType)
        {
            case MapNodeType.Boss: return $"BOSS战 - 第 {_battleNumber} 关";
            case MapNodeType.EliteBattle: return $"精英战 - 第 {_battleNumber} 关";
            default: return $"第 {_battleNumber} 关";
        }
    }

    private RectTransform CreateButton(string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 60);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.5f, 0.8f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.onClick.AddListener(onClick);
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var txt = textGo.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return rect;
    }
}

/// <summary>
/// 已部署单位交互处理器 — 拖拽移动位置，单击取消部署
/// </summary>
public class DeployedUnitHandler : MonoBehaviour
{
    private BattlePreparePanel _panel;
    private int _index;
    private bool _isDragging;
    private Vector3 _dragStartPos;
    private Vector3 _mouseWorldStart;
    private float _dragThreshold = 0.3f; // 超过此距离视为拖拽
    private FighterConfig _config;

    public void Setup(BattlePreparePanel panel, int index, FighterConfig config)
    {
        _panel = panel;
        _index = index;
        _config = config;

        var col = gameObject.GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        var sr = gameObject.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float ppu = sr.sprite.pixelsPerUnit > 0 ? sr.sprite.pixelsPerUnit : 100f;
            col.size = new Vector2(sr.sprite.rect.width / ppu, sr.sprite.rect.height / ppu);
        }
        else
        {
            col.size = new Vector2(4f, 4f);
        }
    }

    public void SetIndex(int index)
    {
        _index = index;
    }

    private void OnMouseDown()
    {
        _isDragging = false;
        _dragStartPos = transform.position;
        _mouseWorldStart = Camera.main.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        _mouseWorldStart.z = 0f;
    }

    private void OnMouseDrag()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        mouseWorld.z = 0f;

        Vector3 delta = mouseWorld - _mouseWorldStart;
        transform.position = _dragStartPos + delta;

        if (!_isDragging && delta.magnitude > _dragThreshold)
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            _panel?.OnDragUpdate(transform.position, _config);
        }
    }

    private void OnMouseUp()
    {
        if (_isDragging)
        {
            // 拖拽结束：更新位置，保持在战场范围内
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, -6.5f, 6.5f);
            pos.y = Mathf.Clamp(pos.y, -3.5f, 3.5f);
            pos.z = 0f;
            transform.position = pos;
            _panel?.UpdateDeployedPosition(_index, pos);

            _panel?.OnDragEnd();
        }
        else
        {
            // 单击：取消部署
            _panel?.UndeployUnit(_index);
        }
    }
}
