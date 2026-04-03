// SaveManager.cs
// CYBERNOMAD -- Save/Load system. Singleton with auto-save.
// Collects player position, terrain params, NPC states, spawned objects.
// Serializes to JSON at Application.persistentDataPath/save.json.

using System;
using System.Collections;
using System.IO;
using UnityEngine;

#if HAS_META_XR
using Plaga44.AI;
#endif

public class SaveManager : MonoBehaviour
{
    private const string LOG = "[PLAGA44] SaveManager";
    private const string SAVE_FILE = "save.json";
    private const float AUTO_SAVE_INTERVAL = 300f; // 5 minutes

    public static SaveManager Instance { get; private set; }

    private float _playTime;
    private string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FILE);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("_SaveManager");
        go.AddComponent<SaveManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(AutoSaveLoop());
        Debug.Log($"{LOG}: initialized. Save path: {SavePath}");
    }

    private void Update()
    {
        _playTime += Time.unscaledDeltaTime;
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- Public API ----

    public void Save()
    {
        try
        {
            var data = CollectData();
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"{LOG}: saved ({json.Length} chars)");
        }
        catch (Exception e)
        {
            Debug.LogError($"{LOG}: save failed -- {e.Message}");
        }
    }

    public void Load()
    {
        if (!HasSave())
        {
            Debug.LogWarning($"{LOG}: no save file found");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<SaveData>(json);
            ApplyData(data);
            Debug.Log($"{LOG}: loaded (version {data.version}, playtime {data.playTime:F0}s)");
        }
        catch (Exception e)
        {
            Debug.LogError($"{LOG}: load failed -- {e.Message}");
        }
    }

    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public void DeleteSave()
    {
        if (HasSave())
        {
            File.Delete(SavePath);
            Debug.Log($"{LOG}: save deleted");
        }
    }

    // ---- Data collection ----

    private SaveData CollectData()
    {
        var data = new SaveData
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            playTime = _playTime,
            presetSlot = SceneDefaults.SafeMode ? 3 : 1
        };

        // Player position/rotation
        CollectPlayer(data);

        // Terrain
        data.terrainSeed = TerrainDeformer.NoiseSeed;
        data.terrainScale = TerrainDeformer.NoiseScale;
        data.terrainStrength = TerrainDeformer.NoiseStrength;

        // NPCs
        CollectNPCs(data);

        // Spawned objects
        CollectObjects(data);

        return data;
    }

    private void CollectPlayer(SaveData data)
    {
#if HAS_META_XR
        var player = FindAnyObjectByType<OVRPlayerController>();
        if (player != null)
        {
            var t = player.transform;
            var pos = t.position;
            var rot = t.rotation;
            data.playerPosition = new float[] { pos.x, pos.y, pos.z };
            data.playerRotation = new float[] { rot.x, rot.y, rot.z, rot.w };
        }
        else
#endif
        {
            // Fallback: main camera
            var cam = Camera.main;
            if (cam != null)
            {
                var t = cam.transform;
                var pos = t.position;
                var rot = t.rotation;
                data.playerPosition = new float[] { pos.x, pos.y, pos.z };
                data.playerRotation = new float[] { rot.x, rot.y, rot.z, rot.w };
            }
        }
    }

    private void CollectNPCs(SaveData data)
    {
#if HAS_META_XR
        var enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        if (enemies.Length == 0)
        {
            data.npcs = Array.Empty<NPCSaveData>();
            return;
        }

        data.npcs = new NPCSaveData[enemies.Length];
        for (int i = 0; i < enemies.Length; i++)
        {
            var ai = enemies[i];
            var health = ai.GetComponent<EnemyHealth>();
            var pos = ai.transform.position;

            data.npcs[i] = new NPCSaveData
            {
                name = ai.gameObject.name,
                position = new float[] { pos.x, pos.y, pos.z },
                health = health != null ? health.CurrentHP : 0f,
                alive = health != null && !health.IsDead
            };
        }
#else
        data.npcs = Array.Empty<NPCSaveData>();
#endif
    }

    private void CollectObjects(SaveData data)
    {
#if HAS_META_XR
        var grabbables = FindObjectsByType<OVRGrabbable>(FindObjectsSortMode.None);
        if (grabbables.Length == 0)
        {
            data.objects = Array.Empty<ObjectSaveData>();
            return;
        }

        data.objects = new ObjectSaveData[grabbables.Length];
        for (int i = 0; i < grabbables.Length; i++)
        {
            var g = grabbables[i];
            var t = g.transform;
            var pos = t.position;
            var rot = t.rotation;
            var scl = t.localScale;

            data.objects[i] = new ObjectSaveData
            {
                prefabName = g.gameObject.name.Replace("(Clone)", "").Trim(),
                position = new float[] { pos.x, pos.y, pos.z },
                rotation = new float[] { rot.x, rot.y, rot.z, rot.w },
                scale = new float[] { scl.x, scl.y, scl.z }
            };
        }
#else
        data.objects = Array.Empty<ObjectSaveData>();
#endif
    }

    // ---- Data application ----

    private void ApplyData(SaveData data)
    {
        // Restore play time
        _playTime = data.playTime;

        // Restore player position
        ApplyPlayer(data);

        // Restore terrain
        if (data.terrainScale > 0f || data.terrainStrength > 0f)
        {
            TerrainDeformer.NoiseSeed = data.terrainSeed;
            TerrainDeformer.NoiseScale = data.terrainScale;
            TerrainDeformer.NoiseStrength = data.terrainStrength;
            TerrainDeformer.ApplyDeformation();
            Debug.Log($"{LOG}: terrain restored (seed={data.terrainSeed}, scale={data.terrainScale}, strength={data.terrainStrength})");
        }

        // NPC restore -- placeholder, skip for now
        if (data.npcs != null && data.npcs.Length > 0)
        {
            Debug.Log($"{LOG}: NPC restore skipped (placeholder) -- {data.npcs.Length} NPCs in save");
        }

        // Object restore -- placeholder, skip for now
        if (data.objects != null && data.objects.Length > 0)
        {
            Debug.Log($"{LOG}: Object restore skipped (placeholder) -- {data.objects.Length} objects in save");
        }
    }

    private void ApplyPlayer(SaveData data)
    {
        if (data.playerPosition == null || data.playerPosition.Length < 3) return;

        var pos = new Vector3(data.playerPosition[0], data.playerPosition[1], data.playerPosition[2]);

        Quaternion rot = Quaternion.identity;
        if (data.playerRotation != null && data.playerRotation.Length >= 4)
        {
            rot = new Quaternion(
                data.playerRotation[0], data.playerRotation[1],
                data.playerRotation[2], data.playerRotation[3]);
        }

#if HAS_META_XR
        var player = FindAnyObjectByType<OVRPlayerController>();
        if (player != null)
        {
            // Disable CharacterController momentarily to allow teleport
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = pos;
            player.transform.rotation = rot;

            if (cc != null) cc.enabled = true;

            Debug.Log($"{LOG}: player restored to {pos}");
            return;
        }
#endif
        // Fallback: move main camera
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = pos;
            cam.transform.rotation = rot;
        }
    }

    // ---- Auto-save coroutine ----

    private IEnumerator AutoSaveLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(AUTO_SAVE_INTERVAL);
            Debug.Log($"{LOG}: auto-save triggered");
            Save();
        }
    }
}
