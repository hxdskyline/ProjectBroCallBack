using UnityEngine;
using UnityEditor;

public static class CopyFullPathMenu
{
    // --- Hierarchy / Scene 右键 ---
    [MenuItem("GameObject/Copy Full Path #F12", false, 0)]
    private static void CopyFullPath()
    {
        if (Selection.activeGameObject == null) return;

        Transform t = Selection.activeGameObject.transform;
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        EditorGUIUtility.systemCopyBuffer = path;
        Debug.Log($"[CopyFullPath] Copied: {path}");
    }

    [MenuItem("GameObject/Copy Full Path", true)]
    private static bool CopyFullPathValidate()
    {
        return Selection.activeGameObject != null;
    }

    // --- Project 窗口右键 ---
    [MenuItem("Assets/Copy Full Path", false, 20)]
    private static void CopyAssetFullPath()
    {
        string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(assetPath)) return;

        EditorGUIUtility.systemCopyBuffer = assetPath;
        Debug.Log($"[CopyFullPath] Copied: {assetPath}");
    }

    [MenuItem("Assets/Copy Full Path", true)]
    private static bool CopyAssetFullPathValidate()
    {
        return Selection.activeObject != null
            && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject));
    }
}
