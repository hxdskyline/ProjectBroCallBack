using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 资源管理器 - 负责所有资源的加载、缓存和卸载（使用 Addressable Asset System）
/// </summary>
public class ResourceManager : MonoBehaviour
{
    private const string BundleRootPrefix = "assets/bundle/";

    // 缓存已加载的资源
    private Dictionary<string, object> _resourceCache = new Dictionary<string, object>();

    // 缓存 AsyncOperationHandle，用于卸载
    private Dictionary<string, AsyncOperationHandle> _asyncHandleCache = new Dictionary<string, AsyncOperationHandle>();
    private Dictionary<string, int> _resourceRefCounts = new Dictionary<string, int>();

    public void Initialize()
    {
        Debug.Log("[ResourceManager] Initialized with Addressable Asset System");
    }

    /// <summary>
    /// 同步加载资源
    /// </summary>
    public T LoadResource<T>(string address) where T : Object
    {
        string resolvedAddress = NormalizeAddress(address);

        // 检查缓存
        if (_resourceCache.ContainsKey(resolvedAddress))
        {
            RetainResource(resolvedAddress);
            return _resourceCache[resolvedAddress] as T;
        }

        try
        {
            // 同步加载
            var handle = Addressables.LoadAssetAsync<T>(resolvedAddress);
            handle.WaitForCompletion();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                T resource = handle.Result;
                _resourceCache[resolvedAddress] = resource;
                _asyncHandleCache[resolvedAddress] = handle;
                _resourceRefCounts[resolvedAddress] = 1;

                Debug.Log($"[ResourceManager] Loaded resource: {resolvedAddress}");
                return resource;
            }
            else
            {
                Debug.LogError($"[ResourceManager] Failed to load resource: {resolvedAddress}");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ResourceManager] Error loading resource {resolvedAddress}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 异步加载资源
    /// </summary>
    public void LoadResourceAsync<T>(string address, System.Action<T> onComplete) where T : Object
    {
        string resolvedAddress = NormalizeAddress(address);

        // 检查缓存
        if (_resourceCache.ContainsKey(resolvedAddress))
        {
            RetainResource(resolvedAddress);
            onComplete?.Invoke(_resourceCache[resolvedAddress] as T);
            return;
        }

        try
        {
            var handle = Addressables.LoadAssetAsync<T>(resolvedAddress);
            handle.Completed += (asyncHandle) =>
            {
                if (asyncHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    T resource = asyncHandle.Result;
                    _resourceCache[resolvedAddress] = resource;
                    _asyncHandleCache[resolvedAddress] = asyncHandle;
                    _resourceRefCounts[resolvedAddress] = 1;

                    Debug.Log($"[ResourceManager] Loaded resource async: {resolvedAddress}");
                    onComplete?.Invoke(resource);
                }
                else
                {
                    Debug.LogError($"[ResourceManager] Failed to load resource async: {resolvedAddress}");
                    onComplete?.Invoke(null);
                }
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ResourceManager] Error loading resource async {resolvedAddress}: {e.Message}");
            onComplete?.Invoke(null);
        }
    }

    /// <summary>
    /// 加载预制体
    /// </summary>
    public GameObject LoadPrefab(string address)
    {
        return LoadResource<GameObject>(address);
    }

    /// <summary>
    /// 异步加载预制体
    /// </summary>
    public void LoadPrefabAsync(string address, System.Action<GameObject> onComplete)
    {
        LoadResourceAsync<GameObject>(address, onComplete);
    }

    /// <summary>
    /// 实例化预制体
    /// </summary>
    public GameObject InstantiatePrefab(string address, Transform parent = null)
    {
        GameObject prefab = LoadPrefab(address);
        if (prefab == null)
        {
            Debug.LogError($"[ResourceManager] Prefab not found: {address}");
            return null;
        }

        GameObject instance = Instantiate(prefab, parent);
        instance.name = prefab.name;
        return instance;
    }

    /// <summary>
    /// 异步实例化预制体
    /// </summary>
    public void InstantiatePrefabAsync(string address, Transform parent, System.Action<GameObject> onComplete)
    {
        LoadPrefabAsync(address, (prefab) =>
        {
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, parent);
                instance.name = prefab.name;
                onComplete?.Invoke(instance);
            }
            else
            {
                onComplete?.Invoke(null);
            }
        });
    }

    /// <summary>
    /// 加载精灵/贴图
    /// </summary>
    public Sprite LoadSprite(string address)
    {
        return LoadResource<Sprite>(address);
    }

    /// <summary>
    /// 异步加载精灵/贴图
    /// </summary>
    public void LoadSpriteAsync(string address, System.Action<Sprite> onComplete)
    {
        LoadResourceAsync<Sprite>(address, onComplete);
    }

    /// <summary>
    /// 加载音频
    /// </summary>
    public AudioClip LoadAudio(string address)
    {
        return LoadResource<AudioClip>(address);
    }

    /// <summary>
    /// 异步加载音频
    /// </summary>
    public void LoadAudioAsync(string address, System.Action<AudioClip> onComplete)
    {
        LoadResourceAsync<AudioClip>(address, onComplete);
    }

    /// <summary>
    /// 卸载资源
    /// </summary>
    public void UnloadResource(string address)
    {
        string resolvedAddress = NormalizeAddress(address);

        if (!ReleaseResourceReference(resolvedAddress))
        {
            return;
        }

        // 从缓存移除
        if (_resourceCache.ContainsKey(resolvedAddress))
        {
            _resourceCache.Remove(resolvedAddress);
        }

        // 释放 AsyncOperationHandle
        if (_asyncHandleCache.ContainsKey(resolvedAddress))
        {
            Addressables.Release(_asyncHandleCache[resolvedAddress]);
            _asyncHandleCache.Remove(resolvedAddress);
            Debug.Log($"[ResourceManager] Unloaded resource: {resolvedAddress}");
        }
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void ClearCache()
    {
        // 释放所有 handle
        foreach (var handle in _asyncHandleCache.Values)
        {
            Addressables.Release(handle);
        }

        _resourceCache.Clear();
        _asyncHandleCache.Clear();
        _resourceRefCounts.Clear();

        Debug.Log("[ResourceManager] Cache cleared");
    }

    private void RetainResource(string resolvedAddress)
    {
        if (string.IsNullOrEmpty(resolvedAddress))
        {
            return;
        }

        if (_resourceRefCounts.ContainsKey(resolvedAddress))
        {
            _resourceRefCounts[resolvedAddress]++;
        }
        else
        {
            _resourceRefCounts[resolvedAddress] = 1;
        }
    }

    private bool ReleaseResourceReference(string resolvedAddress)
    {
        if (string.IsNullOrEmpty(resolvedAddress))
        {
            return false;
        }

        if (!_resourceRefCounts.ContainsKey(resolvedAddress))
        {
            return true;
        }

        _resourceRefCounts[resolvedAddress]--;
        if (_resourceRefCounts[resolvedAddress] > 0)
        {
            return false;
        }

        _resourceRefCounts.Remove(resolvedAddress);
        return true;
    }

    private static string NormalizeAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return address;
        }

        string normalizedAddress = address.Replace('\\', '/').ToLowerInvariant();

        if (normalizedAddress.StartsWith(BundleRootPrefix))
        {
            normalizedAddress = normalizedAddress.Substring(BundleRootPrefix.Length);
        }

        string extension = System.IO.Path.GetExtension(normalizedAddress);
        if (!string.IsNullOrEmpty(extension))
        {
            normalizedAddress = normalizedAddress.Substring(0, normalizedAddress.Length - extension.Length);
        }

        return normalizedAddress;
    }

    /// <summary>
    /// 获取资源信息（调试用）
    /// </summary>
    public void PrintResourceInfo()
    {
        Debug.Log($"[ResourceManager] Cached Resources: {_resourceCache.Count}");
        foreach (string address in _resourceCache.Keys)
        {
            Debug.Log($"  - {address}");
        }
    }
}