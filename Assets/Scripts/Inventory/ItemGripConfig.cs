// =============================================================================
// ItemGripConfig.cs
// CYBERNOMAD -- Per-item grip offset/scale calibration stored in PlayerPrefs.
// User tunes via HamburgerMenu > ITEM GRIP sliders, saves per-item name.
// Applied by PlagaGrabbable on GrabBegin (set localPos/Rot/Scale of item
// relative to hand grip transform).
// =============================================================================

using UnityEngine;

namespace Plaga44.Inventory
{
    /// <summary>
    /// Serializable grip configuration per-item. Loaded from PlayerPrefs
    /// by item name. Each item remembers its ideal grip offset.
    /// </summary>
    public struct ItemGripConfig
    {
        public Vector3 offsetPos;    // local position offset from hand grip (m)
        public Vector3 offsetRotEuler; // local rotation (degrees)
        public float   scale;         // uniform scale multiplier (1.0 = original)

        public static ItemGripConfig Default => new ItemGripConfig
        {
            offsetPos = Vector3.zero,
            offsetRotEuler = Vector3.zero,
            scale = 1f
        };

        // --- PlayerPrefs keys (one per item name) ---------------------------
        private const string KeyPrefix = "Plaga44_ItemGrip_";

        private static string K(string itemName, string field) => $"{KeyPrefix}{itemName}_{field}";

        public static ItemGripConfig Load(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return Default;
            if (!PlayerPrefs.HasKey(K(itemName, "posX"))) return Default;

            var cfg = new ItemGripConfig
            {
                offsetPos = new Vector3(
                    PlayerPrefs.GetFloat(K(itemName, "posX"), 0f),
                    PlayerPrefs.GetFloat(K(itemName, "posY"), 0f),
                    PlayerPrefs.GetFloat(K(itemName, "posZ"), 0f)),
                offsetRotEuler = new Vector3(
                    PlayerPrefs.GetFloat(K(itemName, "rotX"), 0f),
                    PlayerPrefs.GetFloat(K(itemName, "rotY"), 0f),
                    PlayerPrefs.GetFloat(K(itemName, "rotZ"), 0f)),
                scale = PlayerPrefs.GetFloat(K(itemName, "scale"), 1f),
            };
            Debug.Log($"[PLAGA44][ItemGrip] Loaded '{itemName}': pos={cfg.offsetPos:F3} rot={cfg.offsetRotEuler:F1} scale={cfg.scale:F2}");
            return cfg;
        }

        public static void Save(string itemName, ItemGripConfig cfg)
        {
            if (string.IsNullOrEmpty(itemName)) return;
            PlayerPrefs.SetFloat(K(itemName, "posX"), cfg.offsetPos.x);
            PlayerPrefs.SetFloat(K(itemName, "posY"), cfg.offsetPos.y);
            PlayerPrefs.SetFloat(K(itemName, "posZ"), cfg.offsetPos.z);
            PlayerPrefs.SetFloat(K(itemName, "rotX"), cfg.offsetRotEuler.x);
            PlayerPrefs.SetFloat(K(itemName, "rotY"), cfg.offsetRotEuler.y);
            PlayerPrefs.SetFloat(K(itemName, "rotZ"), cfg.offsetRotEuler.z);
            PlayerPrefs.SetFloat(K(itemName, "scale"), cfg.scale);
            PlayerPrefs.Save();
            Debug.Log($"[PLAGA44][ItemGrip] SAVED '{itemName}': pos={cfg.offsetPos:F3} rot={cfg.offsetRotEuler:F1} scale={cfg.scale:F2}");
        }

        public static void Clear(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return;
            PlayerPrefs.DeleteKey(K(itemName, "posX"));
            PlayerPrefs.DeleteKey(K(itemName, "posY"));
            PlayerPrefs.DeleteKey(K(itemName, "posZ"));
            PlayerPrefs.DeleteKey(K(itemName, "rotX"));
            PlayerPrefs.DeleteKey(K(itemName, "rotY"));
            PlayerPrefs.DeleteKey(K(itemName, "rotZ"));
            PlayerPrefs.DeleteKey(K(itemName, "scale"));
            PlayerPrefs.Save();
            Debug.Log($"[PLAGA44][ItemGrip] RESET '{itemName}'");
        }
    }
}
