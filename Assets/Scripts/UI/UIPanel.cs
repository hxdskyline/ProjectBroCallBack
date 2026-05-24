using UnityEngine;
using System.Collections;

/// <summary>
/// UI面板基类 - 所有UI面板都应继承这个类
/// </summary>
public class UIPanel : MonoBehaviour
{
    [SerializeField] protected CanvasGroup _canvasGroup;
    protected bool _isInitialized = false;

    /// <summary>
    /// 初始化面板
    /// </summary>
    public virtual void Initialize()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        _isInitialized = true;
        GameLogger.Log("UIP", $"Init:{gameObject.name}");
    }

    /// <summary>
    /// 显示面板
    /// </summary>
    public virtual void Show()
    {
        GameLogger.Log("UIP", $"Show:{gameObject.name}");
        gameObject.SetActive(true);
        _canvasGroup.alpha = 1;
        _canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public virtual void Hide()
    {
        GameLogger.Log("UIP", $"Hide:{gameObject.name}");
        _canvasGroup.alpha = 0;
        _canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 关闭面板
    /// </summary>
    public virtual void Close()
    {
        GameLogger.Log("UIP", $"Close:{gameObject.name}");
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示面板（带动画）
    /// </summary>
    public virtual void ShowWithAnimation(float duration = 0.3f)
    {
        StartCoroutine(ShowAnimation(duration));
    }

    /// <summary>
    /// 隐藏面板（带动画）
    /// </summary>
    public virtual void HideWithAnimation(float duration = 0.3f)
    {
        StartCoroutine(HideAnimation(duration));
    }

    private IEnumerator ShowAnimation(float duration)
    {
        gameObject.SetActive(true);
        _canvasGroup.alpha = 0;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = 1;
        _canvasGroup.blocksRaycasts = true;
    }

    private IEnumerator HideAnimation(float duration)
    {
        _canvasGroup.blocksRaycasts = false;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = 0;
        gameObject.SetActive(false);
    }
}