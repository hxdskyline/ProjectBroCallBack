using UnityEngine;
using UnityEditor;
using UnityEditor.ShortcutManagement;

/// <summary>
/// 复制选中物体的路径到剪贴板
///
/// Hierarchy 中选中 → 复制 GameObject 路径（如 "Root/Child/Target"）
/// Project 中选中 → 复制资源路径（如 "Assets/Prefabs/UI.prefab"）
/// </summary>
[InitializeOnLoad]
public static class CopyPathShortcut
{
    [MenuItem("Tools/Copy Path %&c", false, 100)]
    [Shortcut("Copy Path/CopySelectedPath", KeyCode.C, ShortcutModifiers.Control | ShortcutModifiers.Shift)]
    static void CopySelectedPath()
    {
        // 优先判断是否为资源（Project 面板中的 prefab/asset）
        // 场景物体的 GetAssetPath 返回空，资源物体返回 "Assets/..."
        if (Selection.activeObject != null)
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(path))
            {
                EditorGUIUtility.systemCopyBuffer = path;
                Debug.Log($"[CopyPath] Asset: {path}");
                return;
            }
        }

        // 场景中的 GameObject（Hierarchy 面板）
        if (Selection.activeGameObject != null)
        {
            string path = GetGameObjectPath(Selection.activeGameObject);
            EditorGUIUtility.systemCopyBuffer = path;
            Debug.Log($"[CopyPath] Hierarchy: {path}");
            return;
        }

        Debug.LogWarning("[CopyPath] 没有选中任何物体");
    }

    private static string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
