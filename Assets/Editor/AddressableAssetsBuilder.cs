using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Addressable Assets 自动构建工具
/// 位置：Assets/Editor/AddressableAssetsBuilder.cs
/// 
/// 功能：
/// 1. 自动扫描 Assets/Bundle 下的资源
/// 2. 按类型和文件夹自动分组和命名
/// 3. 一键构建所有 Addressable Catalogs
/// 4. 一键清空所有 Addressable 数据
/// </summary>
public class AddressableAssetsBuilder
{
    private const string BUNDLE_ROOT_PATH = "Assets/Bundle";
    private const string ADDRESSABLE_DATA_FOLDER = "Assets/AddressableAssetsData";
    private const string ADDRESSABLE_SETTINGS_FILE = "AddressableAssetSettings.asset";

    /// <summary>
    /// 资源类型和对应的文件夹
    /// </summary>
    private static readonly Dictionary<string, string> RESOURCE_TYPES = new Dictionary<string, string>()
    {
        { "UI", "ui" },
        { "Audio/BGM", "audio_bgm" },
        { "Audio/SFX", "audio_sfx" },
        { "AvatarTemp", "avatar_temp" },
        { "Data", "data" },
        { "Sprites", "sprites" },
        { "Prefabs", "prefabs" },
        { "2DEffect", "2deffect" },
        { "Font", "font" },
        { "Map", "map" },
    };

    // ============= 主菜单项 =============

    [MenuItem("Tools/Addressable/Auto Setup All Resources")]
    public static void AutoSetupAllResources()
    {
        Debug.Log("[AddressableAssetsBuilder] ========================================");
        Debug.Log("[AddressableAssetsBuilder] 开始自动设置所有 Addressable 资源...");
        Debug.Log("[AddressableAssetsBuilder] ========================================");

        // 初始化 Addressable System
        InitializeAddressableSystem();

        // 获取 Addressable Settings
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressableAssetsBuilder] 无法获取 Addressable Settings");
            EditorUtility.DisplayDialog("错误", "无法获取 Addressable Settings", "OK");
            return;
        }

        // 清空现有的所有资源（可选）
        bool clearExisting = EditorUtility.DisplayDialog(
            "清空现有资源？",
            "是否清空现有的 Addressable 资源？\n\n点击 'Yes' 清空所有，'No' 保留现有资源",
            "Yes", "No"
        );

        if (clearExisting)
        {
            ClearAllAddressableGroups(settings);
        }

        // 扫描和添加资源
        int totalAdded = 0;
        foreach (var resourceType in RESOURCE_TYPES)
        {
            string folderName = resourceType.Key;
            string groupName = resourceType.Value;

            string folderPath = Path.Combine(BUNDLE_ROOT_PATH, folderName);

            if (Directory.Exists(folderPath))
            {
                int added = SetupResourcesInFolder(settings, folderPath, groupName);
                totalAdded += added;
                Debug.Log($"[AddressableAssetsBuilder] ✅ {folderName}: 添加了 {added} 个资源到组 '{groupName}'");
            }
            else
            {
                Debug.LogWarning($"[AddressableAssetsBuilder] ⚠️ 文件夹不存在: {folderPath}");
            }
        }

        // 保存设置
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[AddressableAssetsBuilder] ========================================");
        Debug.Log($"[AddressableAssetsBuilder] ✅ 完成！总共添加了 {totalAdded} 个资源");
        Debug.Log("[AddressableAssetsBuilder] ========================================");

        EditorUtility.DisplayDialog("成功", $"自动设置完成！\n总共添加了 {totalAdded} 个资源", "OK");
    }

    [MenuItem("Tools/Addressable/Build Catalogs")]
    public static void BuildAddressableCatalogs()
    {
        Debug.Log("[AddressableAssetsBuilder] ========================================");
        Debug.Log("[AddressableAssetsBuilder] 开始构建 Addressable Catalogs...");
        Debug.Log("[AddressableAssetsBuilder] ========================================");

        // 初始化 Addressable System
        InitializeAddressableSystem();

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressableAssetsBuilder] 无法找到 Addressable Settings");
            EditorUtility.DisplayDialog("错误", "无法找到 Addressable Settings", "OK");
            return;
        }

        // 执行构建
        AddressableAssetSettings.BuildPlayerContent();

        Debug.Log("[AddressableAssetsBuilder] ========================================");
        Debug.Log("[AddressableAssetsBuilder] ✅ Catalogs 构建成功！");
        Debug.Log("[AddressableAssetsBuilder] ========================================");

        EditorUtility.DisplayDialog("成功", "Addressable Catalogs 构建成功！", "OK");
    }

    [MenuItem("Tools/Addressable/Clear All Resources")]
    public static void ClearAllAddressableResources()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "确认清空",
            "确定要删除所有 Addressable 资源吗？\n这个操作无法撤销！",
            "Yes, Clear All", "Cancel"
        );

        if (!confirmed)
        {
            Debug.Log("[AddressableAssetsBuilder] 清空操作已取消");
            return;
        }

        Debug.Log("[AddressableAssetsBuilder] ========================================");
        Debug.Log("[AddressableAssetsBuilder] 开始清空所有 Addressable 资源...");
        Debug.Log("[AddressableAssetsBuilder] ========================================");

        // 初始化 Addressable System
        InitializeAddressableSystem();

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressableAssetsBuilder] 无法获取 Addressable Settings");
            EditorUtility.DisplayDialog("错误", "无法获取 Addressable Settings", "OK");
            return;
        }

        int cleared = ClearAllAddressableGroups(settings);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[AddressableAssetsBuilder] ========================================");
        Debug.Log($"[AddressableAssetsBuilder] ✅ 清空完成！共删除了 {cleared} 个资源");
        Debug.Log("[AddressableAssetsBuilder] ========================================");

        EditorUtility.DisplayDialog("成功", $"清空完成！\n删除了 {cleared} 个资源", "OK");
    }

    [MenuItem("Tools/Addressable/Show Addressable Groups Info")]
    public static void ShowAddressableGroupsInfo()
    {
        // 初始化 Addressable System
        InitializeAddressableSystem();

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressableAssetsBuilder] 无法找到 Addressable Settings");
            return;
        }

        Debug.Log("[AddressableAssetsBuilder] ========================================");
        Debug.Log("[AddressableAssetsBuilder] === Addressable Groups 信息 ===");
        Debug.Log("[AddressableAssetsBuilder] ========================================");

        int totalEntries = 0;
        foreach (AddressableAssetGroup group in settings.groups)
        {
            Debug.Log($"\n📦 Group: {group.Name}");
            Debug.Log($"   路径: {AssetDatabase.GetAssetPath(group)}");
            Debug.Log($"   条目数: {group.entries.Count}");

            foreach (AddressableAssetEntry entry in group.entries)
            {
                Debug.Log($"   ├─ {entry.address} ({entry.AssetPath})");
                totalEntries++;
            }
        }

        Debug.Log("\n[AddressableAssetsBuilder] ========================================");
        Debug.Log($"[AddressableAssetsBuilder] 总条目数: {totalEntries}");
        Debug.Log("[AddressableAssetsBuilder] ========================================");
    }

    // ============= 辅助方法 =============

    /// <summary>
    /// 初始化 Addressable System
    /// 确保 Settings 文件存在并正确初始化
    /// </summary>
    private static void InitializeAddressableSystem()
    {
        // 检查 AddressableAssetsData 文件夹是否存在
        if (!Directory.Exists(ADDRESSABLE_DATA_FOLDER))
        {
            Directory.CreateDirectory(ADDRESSABLE_DATA_FOLDER);
            Debug.Log($"[AddressableAssetsBuilder] 创建文件夹: {ADDRESSABLE_DATA_FOLDER}");
            AssetDatabase.Refresh();
        }

        // 获取现有的 Settings
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        // 如果没有 Settings，需要创建
        if (settings == null)
        {
            Debug.Log("[AddressableAssetsBuilder] 创建新的 Addressable Settings...");

            // 查找或创建 Settings 资源
            string settingsPath = Path.Combine(ADDRESSABLE_DATA_FOLDER, ADDRESSABLE_SETTINGS_FILE);

            // 使用 ScriptableObject.CreateInstance 创建
            AddressableAssetSettings newSettings = ScriptableObject.CreateInstance<AddressableAssetSettings>();

            // 初始化基本属性
            newSettings.name = ADDRESSABLE_SETTINGS_FILE;

            // 保存资源
            AssetDatabase.CreateAsset(newSettings, settingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 设置为默认 Settings
            AddressableAssetSettingsDefaultObject.Settings = newSettings;

            Debug.Log($"[AddressableAssetsBuilder] 创建新的 Settings: {settingsPath}");
        }
    }

    /// <summary>
    /// 设置指定文件夹中的资源
    /// </summary>
    private static int SetupResourcesInFolder(AddressableAssetSettings settings, string folderPath, string groupName)
    {
        int count = 0;

        // 获取或创建组
        AddressableAssetGroup group = GetOrCreateGroup(settings, groupName);
        if (group == null)
        {
            Debug.LogError($"[AddressableAssetsBuilder] 无法创建或获取组: {groupName}");
            return 0;
        }

        // 获取所有资源文件
        string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // 跳过文件夹和 .meta 文件
            if (AssetDatabase.IsValidFolder(assetPath) || assetPath.EndsWith(".meta"))
                continue;

            // 跳过已经是 Addressable 的资源
            if (IsAlreadyAddressable(settings, assetPath))
            {
                Debug.LogWarning($"[AddressableAssetsBuilder] ⚠️ 资源已经是 Addressable: {assetPath}");
                continue;
            }

            // 生成 Address
            string address = GenerateAddress(assetPath, groupName);

            // 添加到组
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            entry.address = address;

            count++;
            Debug.Log($"[AddressableAssetsBuilder]   ✓ {address}");
        }

        return count;
    }

    /// <summary>
    /// 生成 Address
    /// 规则：去掉前缀 Assets/Bundle/，并去掉文件扩展名
    /// 最终统一转换为全小写
    /// 例如：Assets/Bundle/UI/MainPanel.prefab -> ui/mainpanel
    ///      Assets/Bundle/Audio/BGM/bgm_main.mp3 -> audio/bgm/bgm_main
    /// </summary>
    private static string GenerateAddress(string assetPath, string groupName)
    {
        string normalizedAssetPath = assetPath.Replace('\\', '/');
        string normalizedBundleRoot = BUNDLE_ROOT_PATH.Replace('\\', '/').TrimEnd('/');

        if (normalizedAssetPath.StartsWith(normalizedBundleRoot + "/"))
        {
            normalizedAssetPath = normalizedAssetPath.Substring(normalizedBundleRoot.Length + 1);
        }

        string extension = Path.GetExtension(normalizedAssetPath);
        if (!string.IsNullOrEmpty(extension))
        {
            normalizedAssetPath = normalizedAssetPath.Substring(0, normalizedAssetPath.Length - extension.Length);
        }

        return normalizedAssetPath.ToLowerInvariant();
    }

    /// <summary>
    /// 检查资源是否已经是 Addressable
    /// </summary>
    private static bool IsAlreadyAddressable(AddressableAssetSettings settings, string assetPath)
    {
        foreach (AddressableAssetGroup group in settings.groups)
        {
            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry.AssetPath == assetPath)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取或创建组
    /// </summary>
    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
    {
        // 查找现有的组
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group.Name == groupName)
            {
                return group;
            }
        }

        // 创建新组
        AddressableAssetGroup newGroup = settings.CreateGroup(groupName, false, false, true, null);

        Debug.Log($"[AddressableAssetsBuilder] 创建新组: {groupName}");
        return newGroup;
    }

    /// <summary>
    /// 清空所有 Addressable 组中的资源（只保留 Default Local Group）
    /// </summary>
    private static int ClearAllAddressableGroups(AddressableAssetSettings settings)
    {
        int totalCleared = 0;

        // 获取所有组的列表（因为删除时会改变列表，所以先复制）
        List<AddressableAssetGroup> groupsToProcess = new List<AddressableAssetGroup>(settings.groups);

        foreach (AddressableAssetGroup group in groupsToProcess)
        {
            // 获取条目数量
            int entriesToRemove = group.entries.Count;

            // 清空条目：转换为列表再逐个删除
            List<AddressableAssetEntry> entriesToDelete = new List<AddressableAssetEntry>(group.entries);

            foreach (AddressableAssetEntry entry in entriesToDelete)
            {
                group.RemoveAssetEntry(entry);
            }

            totalCleared += entriesToRemove;

            // 只删除自定义的组，保留 "Default Local Group"
            if (group.Name != "Default Local Group")
            {
                settings.RemoveGroup(group);
                Debug.Log($"[AddressableAssetsBuilder] 删除组: {group.Name} ({entriesToRemove} 个条目)");
            }
            else
            {
                Debug.Log($"[AddressableAssetsBuilder] 清空组: {group.Name} ({entriesToRemove} 个条目)");
            }
        }

        return totalCleared;
    }
}