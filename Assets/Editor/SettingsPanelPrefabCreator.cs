using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// 自动创建 SettingsPanel 预制体的 Editor 工具
/// 使用方法：Unity 菜单 Tools -> Create SettingsPanel Prefab
/// </summary>
public static class SettingsPanelPrefabCreator
{
    [MenuItem("Tools/Create SettingsPanel Prefab")]
    public static void Create()
    {
        // 根对象
        var root = new GameObject("SettingsPanel");
        var rootRect = root.AddComponent<RectTransform>();
        root.AddComponent<CanvasGroup>();
        var rootImage = root.AddComponent<Image>();
        rootImage.color = new Color(0, 0, 0, 0.6f); // 半透明遮罩
        rootImage.raycastTarget = true;

        // Stretch 全屏
        SetStretch(rootRect);

        // 添加 SettingsPanel 脚本
        var settingsPanel = root.AddComponent<SettingsPanel>();

        // === ContentPanel ===
        var content = CreateChild("ContentPanel", root.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(600, 400);
        contentRect.anchoredPosition = Vector2.zero;
        var contentImg = content.AddComponent<Image>();
        contentImg.color = new Color(0.12f, 0.12f, 0.15f, 0.98f);

        // === CloseButton (右上角) ===
        var closeBtnObj = CreateChild("CloseButton", content.transform);
        var closeBtnRect = closeBtnObj.GetComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1, 1);
        closeBtnRect.anchorMax = new Vector2(1, 1);
        closeBtnRect.pivot = new Vector2(1, 1);
        closeBtnRect.sizeDelta = new Vector2(40, 40);
        closeBtnRect.anchoredPosition = new Vector2(-10, -10);
        var closeBtnImg = closeBtnObj.AddComponent<Image>();
        closeBtnImg.color = new Color(0.7f, 0.25f, 0.25f, 1f);
        var closeBtn = closeBtnObj.AddComponent<Button>();
        // × 文字
        var closeLabel = CreateChild("Label", closeBtnObj.transform);
        var closeLabelRect = closeLabel.GetComponent<RectTransform>();
        SetStretch(closeLabelRect);
        closeLabelRect.offsetMin = Vector2.zero;
        closeLabelRect.offsetMax = Vector2.zero;
        var closeLabelText = closeLabel.AddComponent<Text>();
        closeLabelText.text = "×";
        closeLabelText.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        closeLabelText.fontSize = 24;
        closeLabelText.alignment = TextAnchor.MiddleCenter;
        closeLabelText.color = Color.white;
        closeLabelText.raycastTarget = false;

        // === 左栏 LeftPanel ===
        var leftPanel = CreateChild("LeftPanel", content.transform);
        var leftRect = leftPanel.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0, 0);
        leftRect.anchorMax = new Vector2(0.5f, 1);
        leftRect.offsetMin = new Vector2(20, 60);
        leftRect.offsetMax = new Vector2(-10, -50);
        var leftVlg = leftPanel.AddComponent<VerticalLayoutGroup>();
        leftVlg.spacing = 20;
        leftVlg.padding = new RectOffset(20, 20, 20, 20);
        leftVlg.childAlignment = TextAnchor.MiddleCenter;
        leftVlg.childControlWidth = true;
        leftVlg.childControlHeight = false;
        leftVlg.childForceExpandWidth = true;
        leftVlg.childForceExpandHeight = false;

        // 三个 Toggle
        var masterToggle = CreateToggle("MasterVolumeToggle", leftPanel.transform, "音量总开关");
        var sfxToggle = CreateToggle("SfxVolumeToggle", leftPanel.transform, "音效开关");
        var bgmToggle = CreateToggle("BgmVolumeToggle", leftPanel.transform, "BGM开关");

        // === 右栏 RightPanel ===
        var rightPanel = CreateChild("RightPanel", content.transform);
        var rightRect = rightPanel.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.5f, 0);
        rightRect.anchorMax = new Vector2(1, 1);
        rightRect.offsetMin = new Vector2(10, 60);
        rightRect.offsetMax = new Vector2(-20, -50);

        var tipsText = CreateChild("TipsText", rightPanel.transform);
        var tipsRect = tipsText.GetComponent<RectTransform>();
        SetStretch(tipsRect);
        tipsRect.offsetMin = Vector2.zero;
        tipsRect.offsetMax = Vector2.zero;
        var tipsTextComp = tipsText.AddComponent<Text>();
        tipsTextComp.text = "祝你好运";
        tipsTextComp.font = Font.CreateDynamicFontFromOSFont("Arial", 28);
        tipsTextComp.fontSize = 28;
        tipsTextComp.alignment = TextAnchor.MiddleCenter;
        tipsTextComp.color = new Color(1f, 0.9f, 0.3f, 1f);
        tipsTextComp.raycastTarget = false;

        // === ButtonBar 底部按钮栏 ===
        var buttonBar = CreateChild("ButtonBar", content.transform);
        var barRect = buttonBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0, 0);
        barRect.anchorMax = new Vector2(1, 0);
        barRect.pivot = new Vector2(0.5f, 0);
        barRect.sizeDelta = new Vector2(0, 50);
        barRect.anchoredPosition = new Vector2(0, 5);
        var barHlg = buttonBar.AddComponent<HorizontalLayoutGroup>();
        barHlg.spacing = 60;
        barHlg.childAlignment = TextAnchor.MiddleCenter;
        barHlg.childControlWidth = true;
        barHlg.childControlHeight = true;
        barHlg.childForceExpandWidth = false;
        barHlg.childForceExpandHeight = false;
        var barCsf = buttonBar.AddComponent<ContentSizeFitter>();
        barCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        barCsf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ConfirmButton
        var confirmBtn = CreateButton("ConfirmButton", buttonBar.transform, "确认",
            new Color(0.2f, 0.5f, 0.3f, 1f), new Vector2(120, 40));

        // CancelButton
        var cancelBtn = CreateButton("CancelButton", buttonBar.transform, "取消",
            new Color(0.5f, 0.25f, 0.2f, 1f), new Vector2(120, 40));

        // === 绑定 SettingsPanel 字段 ===
        var serializedObj = new SerializedObject(settingsPanel);

        SetField(serializedObj, "_closeButton", closeBtn);
        SetField(serializedObj, "_masterVolumeToggle", masterToggle);
        SetField(serializedObj, "_sfxVolumeToggle", sfxToggle);
        SetField(serializedObj, "_bgmVolumeToggle", bgmToggle);
        SetField(serializedObj, "_confirmButton", confirmBtn);
        SetField(serializedObj, "_cancelButton", cancelBtn);

        // CanvasGroup
        SetField(serializedObj, "_canvasGroup", root.GetComponent<CanvasGroup>());

        serializedObj.ApplyModifiedProperties();

        // === 保存为预制体 ===
        string folder = "Assets/Bundle/UI";
        if (!AssetDatabase.IsValidFolder(folder))
            System.IO.Directory.CreateDirectory(folder);

        string prefabPath = folder + "/SettingsPanel.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[SettingsPanelPrefabCreator] Prefab created at {prefabPath}");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
    }

    // --- 辅助方法 ---

    static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Toggle CreateToggle(string name, Transform parent, string label)
    {
        var toggleObj = CreateChild(name, parent);
        var toggleRect = toggleObj.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(0, 40);

        var toggleBg = toggleObj.AddComponent<Image>();
        toggleBg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var toggle = toggleObj.AddComponent<Toggle>();
        toggle.isOn = true;

        // Checkmark
        var checkObj = CreateChild("Checkmark", toggleObj.transform);
        SetStretch(checkObj.GetComponent<RectTransform>());
        checkObj.GetComponent<RectTransform>().offsetMin = new Vector2(4, 4);
        checkObj.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -4);
        var checkImg = checkObj.AddComponent<Image>();
        checkImg.color = new Color(0.3f, 0.7f, 0.4f, 1f);
        checkImg.raycastTarget = false;
        toggle.graphic = checkImg;
        toggle.targetGraphic = toggleBg;

        // Label
        var labelObj = CreateChild("Label", toggleObj.transform);
        var labelRect = labelObj.GetComponent<RectTransform>();
        SetStretch(labelRect);
        labelRect.offsetMin = new Vector2(40, 0);
        labelRect.offsetMax = Vector2.zero;
        var labelComp = labelObj.AddComponent<Text>();
        labelComp.text = label;
        labelComp.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
        labelComp.fontSize = 18;
        labelComp.alignment = TextAnchor.MiddleLeft;
        labelComp.color = Color.white;
        labelComp.raycastTarget = false;

        return toggle;
    }

    static Button CreateButton(string name, Transform parent, string label, Color color, Vector2 size)
    {
        var btnObj = CreateChild(name, parent);
        var btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.sizeDelta = size;
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = color;
        var btn = btnObj.AddComponent<Button>();

        // Label
        var labelObj = CreateChild("Label", btnObj.transform);
        SetStretch(labelObj.GetComponent<RectTransform>());
        labelObj.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        labelObj.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        var labelComp = labelObj.AddComponent<Text>();
        labelComp.text = label;
        labelComp.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
        labelComp.fontSize = 18;
        labelComp.alignment = TextAnchor.MiddleCenter;
        labelComp.color = Color.white;
        labelComp.raycastTarget = false;

        return btn;
    }

    static void SetField(SerializedObject obj, string fieldName, Object value)
    {
        var prop = obj.FindProperty(fieldName);
        if (prop != null)
            prop.objectReferenceValue = value;
        else
            Debug.LogWarning($"[SettingsPanelPrefabCreator] Field not found: {fieldName}");
    }
}
