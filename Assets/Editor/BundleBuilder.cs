using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// AssetBundle 构建工具
/// 使用方法：菜单 Tools → Build AssetBundles
/// </summary>
public class BundleBuilder
{
    [MenuItem("Tools/Build AssetBundles")]
    public static void BuildAssetBundles()
    {
        // 输出路径：StreamingAssets/AssetBundles
        string outputPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");

        // 创建目录（如果不存在）
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
            Debug.Log($"[BundleBuilder] Created directory: {outputPath}");
        }

        try
        {
            // 构建 AssetBundle（针对当前平台）
            BuildPipeline.BuildAssetBundles(
                outputPath,
                BuildAssetBundleOptions.None,
                EditorUserBuildSettings.activeBuildTarget
            );

            Debug.Log($"[BundleBuilder] ✅ AssetBundles built successfully!");
            Debug.Log($"[BundleBuilder] Output path: {outputPath}");

            // 刷新资源
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BundleBuilder] ❌ Error building AssetBundles: {e.Message}");
        }
    }

    [MenuItem("Tools/Clear AssetBundles")]
    public static void ClearAssetBundles()
    {
        string outputPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");

        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
            Debug.Log($"[BundleBuilder] ✅ AssetBundles cleared!");

            // 删除 .meta 文件
            string metaPath = outputPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }
        else
        {
            Debug.LogWarning("[BundleBuilder] AssetBundles folder not found");
        }
    }

    [MenuItem("Tools/Show AssetBundles Info")]
    public static void ShowAssetBundlesInfo()
    {
        string outputPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");

        if (!Directory.Exists(outputPath))
        {
            Debug.LogWarning("[BundleBuilder] AssetBundles folder not found");
            return;
        }

        Debug.Log("[BundleBuilder] === AssetBundles Info ===");
        Debug.Log($"Output path: {outputPath}");

        // 显示所有文件
        string[] files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
        Debug.Log($"Total files: {files.Length}");

        foreach (string file in files)
        {
            string relativePath = file.Replace(outputPath, "").TrimStart('\\', '/');
            long fileSize = new FileInfo(file).Length;
            Debug.Log($"  - {relativePath} ({fileSize} bytes)");
        }
    }
}