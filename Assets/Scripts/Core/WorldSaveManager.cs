// =============================================================================
// WorldSaveManager.cs
// CYBERNOMAD -- Event-driven world-save (#196).
// Zapis: wejscie do menu, wyjscie z menu, spawn itemu, quit/pause.
// Plik: Application.persistentDataPath/plaga44_save.json (dziala na Quescie).
// Load na boot: settings + pozycja gracza + avatar + respawn wszystkich
// SaveableObject. Brak pliku -> czysty start.
//
// Settings sa MIRROREM (PlayerPrefs zostaje zywym mechanizmem SettingsRegistry).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Plaga44.UI;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class WorldSaveManager : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][WorldSave]";
        private const string FileName = "plaga44_save.json";
        private const string OvrRigName = "OVRCameraRig";
        private const string AutoBootGoName = "_WorldSaveManager";

        public static WorldSaveManager Instance { get; private set; }

        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        public static bool HasSave => File.Exists(SavePath);

        // ---- DTO (JsonUtility-serializable) --------------------------------
        [Serializable]
        public struct Vec
        {
            public float x, y, z;
            public static Vec Of(Vector3 v) => new Vec { x = v.x, y = v.y, z = v.z };
            public Vector3 V => new Vector3(x, y, z);
        }

        [Serializable]
        public class SavedObject
        {
            public string resourcePath;
            public Vec pos, euler, vel, angVel;
            public bool held;
        }

        [Serializable]
        public class SavedSetting { public string key; public float value; }

        [Serializable]
        public class WorldSave
        {
            public Vec playerPos;
            public float playerYaw;
            public int avatarMode;
            public List<SavedSetting> settings = new List<SavedSetting>();
            public List<SavedObject> objects = new List<SavedObject>();
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<WorldSaveManager>() != null) return;
            new GameObject(AutoBootGoName).AddComponent<WorldSaveManager>();
        }

        private bool _loaded;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start() => Load(); // other singletons ready by now

        private void OnApplicationQuit() => Save("quit");
        private void OnApplicationPause(bool paused) { if (paused) Save("pause"); }

        // =====================================================================
        // SAVE
        // =====================================================================

        public void Save(string reason)
        {
            try
            {
                var data = Capture();
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                Debug.Log($"{LOG} SAVED ({reason}): {data.objects.Count} obj, {data.settings.Count} settings -> {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG} Save FAILED ({reason}): {e.Message}");
            }
        }

        private WorldSave Capture()
        {
            var save = new WorldSave();

            var rig = GameObject.Find(OvrRigName);
            if (rig != null)
            {
                save.playerPos = Vec.Of(rig.transform.position);
                save.playerYaw = rig.transform.eulerAngles.y;
            }

            var avatar = PlayerAvatar.FindCurrent();
            save.avatarMode = avatar != null ? avatar.CurrentMode : 0;

            foreach (var section in SettingsRegistry.GetSectionNames())
            {
                foreach (var s in SettingsRegistry.GetSettings(section))
                {
                    // Ten sam kontrakt co SettingsRegistry -- pomija read-only/akcje,
                    // GAME STATE (runtime, nie ustawienie) i baseline-forced (Eye Tex).
                    if (!SettingsRegistry.IsPersistable(section, s)) continue;
                    save.settings.Add(new SavedSetting { key = section + "/" + s.name, value = s.get() });
                }
            }

            foreach (var so in FindObjectsByType<SaveableObject>(FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(so.resourcePath)) continue;
                if (so.name.Contains("Preview")) continue; // transient menu preview
                var rb = so.GetComponent<Rigidbody>();
                var grab = so.GetComponent<Plaga44.Inventory.PlagaGrabbable>();
                bool moving = rb != null && !rb.isKinematic;
                save.objects.Add(new SavedObject
                {
                    resourcePath = so.resourcePath,
                    pos = Vec.Of(so.transform.position),
                    euler = Vec.Of(so.transform.eulerAngles),
                    vel = Vec.Of(moving ? rb.linearVelocity : Vector3.zero),
                    angVel = Vec.Of(moving ? rb.angularVelocity : Vector3.zero),
                    held = grab != null && grab.isGrabbed
                });
            }

            return save;
        }

        // =====================================================================
        // LOAD
        // =====================================================================

        public void Load()
        {
            if (_loaded) return;
            _loaded = true;

            if (!HasSave) { Debug.Log($"{LOG} No save file -- fresh start."); return; }
            try
            {
                var data = JsonUtility.FromJson<WorldSave>(File.ReadAllText(SavePath));
                if (data == null) { Debug.LogWarning($"{LOG} Save empty/corrupt -- fresh start."); return; }
                ApplySettings(data);
                ApplyPlayer(data);
                RespawnObjects(data);
                Debug.Log($"{LOG} LOADED: {data.objects.Count} obj, {data.settings.Count} settings.");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG} Load FAILED: {e.Message}");
            }
        }

        private static void ApplySettings(WorldSave data)
        {
            if (data.settings == null) return;
            var map = new Dictionary<string, float>();
            foreach (var ss in data.settings) map[ss.key] = ss.value;

            foreach (var section in SettingsRegistry.GetSectionNames())
            {
                foreach (var s in SettingsRegistry.GetSettings(section))
                {
                    // Ten sam kontrakt co Capture/SettingsRegistry -- NIE przywracaj
                    // GAME STATE/Phase (blokowalo ruch na boot) ani Eye Tex baseline.
                    if (!SettingsRegistry.IsPersistable(section, s)) continue;
                    if (map.TryGetValue(section + "/" + s.name, out float v))
                        s.set(Mathf.Clamp(v, s.min, s.max));
                }
            }
        }

        private static void ApplyPlayer(WorldSave data)
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig != null)
            {
                rig.transform.position = data.playerPos.V;
                var e = rig.transform.eulerAngles; e.y = data.playerYaw;
                rig.transform.eulerAngles = e;
            }
            var avatar = PlayerAvatar.FindCurrent();
            if (avatar != null) avatar.SetAvatarMode(data.avatarMode);
        }

        private void RespawnObjects(WorldSave data)
        {
            if (data.objects == null) return;
            foreach (var o in data.objects)
            {
                if (string.IsNullOrEmpty(o.resourcePath)) continue;
                var prefab = Resources.Load<GameObject>(o.resourcePath);
                if (prefab == null)
                {
                    Debug.LogWarning($"{LOG} Respawn skip -- Resources/{o.resourcePath} not found");
                    continue;
                }
                var go = Instantiate(prefab, o.pos.V, Quaternion.Euler(o.euler.V));
                go.name = prefab.name;
                SaveableObject.Tag(go, o.resourcePath);

                var rb = go.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.linearVelocity = o.vel.V;
                    rb.angularVelocity = o.angVel.V;
                }
                // held-on-save -> respawn at saved transform as free object.
                // Auto re-grip on boot is a refinement (hands not tracked yet). See #196.
            }
        }
    }
}
