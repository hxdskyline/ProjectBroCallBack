using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板 — 音量开关、确认/取消
/// </summary>
public class SettingsPanel : UIPanel
{
    [SerializeField] private Button _closeButton;
    [SerializeField] private Toggle _masterVolumeToggle;
    [SerializeField] private Toggle _sfxVolumeToggle;
    [SerializeField] private Toggle _bgmVolumeToggle;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private CanvasGroup _canvasGroup;

    public override void Initialize()
    {
        base.Initialize();

        var title = CreateText("Title", "设置", 32, Color.white);
        title.rectTransform.anchoredPosition = new Vector2(0, 150);

        var closeBtn = CreateButton("CloseButton", "关闭", OnClose);
        closeBtn.anchoredPosition = new Vector2(0, -100);
    }

    private void OnClose()
    {
        GameLogger.Log("SettingP", "Close");
        Debug.Log("[SettingsPanel] 关闭设置");
        Hide();
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 60);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        return txt;
    }

    private RectTransform CreateButton(string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 50);
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
        txt.fontSize = 22;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return rect;
    }
}
