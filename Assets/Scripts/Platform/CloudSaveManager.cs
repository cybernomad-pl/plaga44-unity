// CloudSaveManager.cs
// CYBERNOMAD -- Oculus CloudStorage wrappers: Save(key, data), Load(key).
// Serializacja do JSON. Bucket: "plaga44_save".
// CloudStorage2 API (v62+) -- dla Meta XR SDK v81.
// Fallback na PlayerPrefs w edytorze / bez HAS_META_XR.

using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

#if HAS_META_XR_PLATFORM
using Oculus.Platform;
using Oculus.Platform.Models;
#endif

namespace Plaga44.Platform
{
    /// <summary>
    /// Cloud save bucket name for PLAGA '44.
    /// Must be registered in Meta Developer Dashboard > Cloud Storage.
    /// </summary>
    public static class CloudSaveBuckets
    {
        public const string MainSave = "plaga44_save";
    }

    /// <summary>
    /// Wraps Oculus CloudStorage2 API.
    /// Saves and loads arbitrary JSON-serializable data objects.
    /// Singleton accessed via CloudSaveManager.Instance.
    /// </summary>
    public class CloudSaveManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Singleton
        // ------------------------------------------------------------------ //

        private static CloudSaveManager _instance;

        public static CloudSaveManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[CloudSaveManager]");
                    _instance = go.AddComponent<CloudSaveManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Save a JSON-serializable object to cloud storage under the given key.
        /// Uses bucket "plaga44_save".
        /// </summary>
        /// <typeparam name="T">Must be JSON-serializable by JsonUtility.</typeparam>
        /// <param name="key">Storage key (max 64 chars, alphanumeric + underscores).</param>
        /// <param name="data">Data object to serialize and save.</param>
        /// <returns>True if saved successfully.</returns>
        public async Task<bool> Save<T>(string key, T data)
        {
            if (!PlatformManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[CloudSaveManager] PlatformManager not initialized. Falling back to PlayerPrefs.");
                return SaveToPlayerPrefs(key, data);
            }

            string json = JsonUtility.ToJson(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

#if HAS_META_XR_PLATFORM
            try
            {
                var tcs = new TaskCompletionSource<bool>();

                CloudStorage2.Put(CloudSaveBuckets.MainSave, key, bytes).OnComplete(msg =>
                {
                    if (msg.IsError)
                    {
                        Debug.LogError($"[CloudSaveManager] Save error key='{key}': {msg.GetError().Message}");
                        tcs.SetResult(false);
                    }
                    else
                    {
                        Debug.Log($"[CloudSaveManager] Saved key='{key}' ({bytes.Length} bytes).");
                        tcs.SetResult(true);
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSaveManager] Save exception key='{key}': {ex}");
                return false;
            }
#else
            await Task.CompletedTask;
            return SaveToPlayerPrefs(key, data);
#endif
        }

        /// <summary>
        /// Load a JSON-serializable object from cloud storage by key.
        /// Returns default(T) if not found or on error.
        /// </summary>
        /// <typeparam name="T">Must be JSON-serializable by JsonUtility.</typeparam>
        /// <param name="key">Storage key used during Save.</param>
        /// <returns>Deserialized object or default(T).</returns>
        public async Task<T> Load<T>(string key)
        {
            if (!PlatformManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[CloudSaveManager] PlatformManager not initialized. Falling back to PlayerPrefs.");
                return LoadFromPlayerPrefs<T>(key);
            }

#if HAS_META_XR_PLATFORM
            try
            {
                var tcs = new TaskCompletionSource<T>();

                CloudStorage2.Get(CloudSaveBuckets.MainSave, key).OnComplete(msg =>
                {
                    if (msg.IsError)
                    {
                        Debug.LogError($"[CloudSaveManager] Load error key='{key}': {msg.GetError().Message}");
                        tcs.SetResult(default(T));
                        return;
                    }

                    var cloudData = msg.GetCloudStorageData();
                    if (cloudData == null || cloudData.Data == null || cloudData.Data.Length == 0)
                    {
                        Debug.Log($"[CloudSaveManager] No data found for key='{key}'.");
                        tcs.SetResult(default(T));
                        return;
                    }

                    try
                    {
                        string json = Encoding.UTF8.GetString(cloudData.Data);
                        T result = JsonUtility.FromJson<T>(json);
                        Debug.Log($"[CloudSaveManager] Loaded key='{key}' ({cloudData.Data.Length} bytes).");
                        tcs.SetResult(result);
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogError($"[CloudSaveManager] JSON parse error key='{key}': {parseEx}");
                        tcs.SetResult(default(T));
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSaveManager] Load exception key='{key}': {ex}");
                return default(T);
            }
#else
            await Task.CompletedTask;
            return LoadFromPlayerPrefs<T>(key);
#endif
        }

        /// <summary>
        /// Delete a cloud save entry by key.
        /// </summary>
        public async Task<bool> Delete(string key)
        {
            if (!PlatformManager.Instance.IsInitialized)
            {
                PlayerPrefs.DeleteKey(GetPlayerPrefsKey(key));
                return true;
            }

#if HAS_META_XR_PLATFORM
            try
            {
                var tcs = new TaskCompletionSource<bool>();

                CloudStorage2.Delete(CloudSaveBuckets.MainSave, key).OnComplete(msg =>
                {
                    if (msg.IsError)
                    {
                        Debug.LogError($"[CloudSaveManager] Delete error key='{key}': {msg.GetError().Message}");
                        tcs.SetResult(false);
                    }
                    else
                    {
                        Debug.Log($"[CloudSaveManager] Deleted key='{key}'.");
                        tcs.SetResult(true);
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSaveManager] Delete exception key='{key}': {ex}");
                return false;
            }
#else
            PlayerPrefs.DeleteKey(GetPlayerPrefsKey(key));
            await Task.CompletedTask;
            return true;
#endif
        }

        // ------------------------------------------------------------------ //
        // PlayerPrefs fallback (Editor / non-Meta builds)
        // ------------------------------------------------------------------ //

        private string GetPlayerPrefsKey(string key) => $"plaga44_cloudsave_{key}";

        private bool SaveToPlayerPrefs<T>(string key, T data)
        {
            try
            {
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(GetPlayerPrefsKey(key), json);
                PlayerPrefs.Save();
                Debug.Log($"[CloudSaveManager] PlayerPrefs fallback save key='{key}'.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSaveManager] PlayerPrefs save error key='{key}': {ex}");
                return false;
            }
        }

        private T LoadFromPlayerPrefs<T>(string key)
        {
            string ppKey = GetPlayerPrefsKey(key);
            if (!PlayerPrefs.HasKey(ppKey))
            {
                Debug.Log($"[CloudSaveManager] PlayerPrefs fallback -- no data for key='{key}'.");
                return default(T);
            }

            try
            {
                string json = PlayerPrefs.GetString(ppKey);
                T result = JsonUtility.FromJson<T>(json);
                Debug.Log($"[CloudSaveManager] PlayerPrefs fallback load key='{key}'.");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSaveManager] PlayerPrefs load error key='{key}': {ex}");
                return default(T);
            }
        }
    }
}
