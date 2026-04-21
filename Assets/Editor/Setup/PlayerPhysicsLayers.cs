// =============================================================================
// PlayerPhysicsLayers.cs
// CYBERNOMAD -- EDITOR-side: tworzy warstwy "PlayerBody" (6) i "Item" (7)
// w ProjectSettings/TagManager.asset (persistent). Nazwy warstw przetrwaja
// zerowanie testbedu bo TagManager jest w repo.
//
// COLLISION MATRIX jest ustawiana przez runtime PlayerPhysicsLayersRuntime
// (Physics.IgnoreLayerCollision nie persystuje do DynamicsManager.asset
// mimo nazwy "Physics" -- to runtime-only API).
//
// Logika matrix (definicja jednorodna z runtime):
//   PlayerBody x Default/TransparentFX/IgnoreRaycast/Water/UI = OFF
//   PlayerBody x PlayerBody = OFF
//   PlayerBody x Item       = ON    (cel body physics -- blokuje itemy)
//
// Idempotentne: jesli warstwy juz istnieja, nic nie zmienia.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class PlayerPhysicsLayers
    {
        private const string LOG = "[PLAGA44][PhysicsLayers]";

        // Aliasy do PlayerPhysicsLayersRuntime (jedyne zrodlo prawdy dla ID warstw)
        public static int PlayerBodyLayer => Plaga44.PlayerPhysicsLayersRuntime.PlayerBodyLayer;
        public static int ItemLayer       => Plaga44.PlayerPhysicsLayersRuntime.ItemLayer;

        public const string PlayerBodyName = "PlayerBody";
        public const string ItemName       = "Item";

        public static void Run()
        {
            EnsureLayerNames();
            // Collision matrix -- robi runtime PlayerPhysicsLayersRuntime
            // (BeforeSceneLoad). Nie powielamy tutaj bo editor-time call
            // Physics.IgnoreLayerCollision nie persystuje miedzy sesjami.
        }

        // -----------------------------------------------------------------
        // Layer names in TagManager.asset
        // -----------------------------------------------------------------
        private static void EnsureLayerNames()
        {
            var tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAsset == null || tagManagerAsset.Length == 0)
            {
                Debug.LogError($"{LOG} Cannot load ProjectSettings/TagManager.asset");
                return;
            }
            var so = new SerializedObject(tagManagerAsset[0]);
            var layersProp = so.FindProperty("layers");
            if (layersProp == null)
            {
                Debug.LogError($"{LOG} 'layers' property not found in TagManager (Unity format change?)");
                return;
            }

            bool changed = false;
            changed |= SetLayerIfEmpty(layersProp, PlayerBodyLayer, PlayerBodyName);
            changed |= SetLayerIfEmpty(layersProp, ItemLayer,       ItemName);

            if (changed)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log($"{LOG} Layers registered: {PlayerBodyName}={PlayerBodyLayer}, {ItemName}={ItemLayer}");
            }
            else
            {
                Debug.Log($"{LOG} [OK] Layers already registered.");
            }
        }

        private static bool SetLayerIfEmpty(SerializedProperty layersArray, int index, string name)
        {
            var slot = layersArray.GetArrayElementAtIndex(index);
            if (slot == null) return false;
            if (slot.stringValue == name) return false;
            if (!string.IsNullOrEmpty(slot.stringValue))
            {
                // Slot zajety przez co innego -- log + nie nadpisuj, inaczej
                // zepsujemy layer setup innego developera/projektu.
                Debug.LogWarning($"{LOG} Layer {index} already named '{slot.stringValue}', expected '{name}'. Skipping.");
                return false;
            }
            slot.stringValue = name;
            return true;
        }

    }
}
#endif
