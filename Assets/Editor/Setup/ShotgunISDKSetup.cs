// =============================================================================
// ShotgunISDKSetup.cs
// CYBERNOMAD -- Migruje Shotgun.prefab z PlagaGrabbable (OVR Core legacy)
// na ISDK HandGrabInteractable. Meta Movement SDK samples ISDKIntegration/Mug
// to wzorzec -- kopiujemy pattern.
//
// OPERATIONS na Shotgun.prefab:
//   1. Wylacz PlagaGrabbable + HapticOnGrab (redundantne z ISDK)
//   2. Dodaj HandGrabInteractable (+ jego wymagane Grabbable base)
//   3. Dodaj child GO "HandGrabPose" z ISDK HandGrabPose component
//      (user skalibruje grip w Inspectorze -- poki co default pose)
//
// Idempotent: jesli HandGrabInteractable juz jest, skip.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class ShotgunISDKSetup
    {
        private const string LOG = "[PLAGA44][ShotgunISDKSetup]";
        private const string PrefabPath = "Assets/Resources/Items/Shotgun.prefab";

        public static void Run()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"{LOG} Shotgun.prefab not loaded: {PrefabPath}");
                return;
            }

            try
            {
                bool changed = false;
                changed |= DisableLegacyGrabbable(prefabRoot);
                changed |= AddISDKGrabbable(prefabRoot);
                changed |= AddISDKHandGrabInteractable(prefabRoot);

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                    Debug.Log($"{LOG} [SAVED] {PrefabPath}");
                }
                else
                {
                    Debug.Log($"{LOG} [OK] No changes to {PrefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool DisableLegacyGrabbable(GameObject root)
        {
            bool changed = false;
            var plagaGrab = root.GetComponent<Plaga44.Inventory.PlagaGrabbable>();
            if (plagaGrab != null && plagaGrab.enabled)
            {
                plagaGrab.enabled = false;
                changed = true;
                Debug.Log($"{LOG} PlagaGrabbable disabled (ISDK Grabbable replaces)");
            }
            var haptic = root.GetComponent<Plaga44.Feedback.HapticOnGrab>();
            if (haptic != null && haptic.enabled)
            {
                haptic.enabled = false;
                changed = true;
                Debug.Log($"{LOG} HapticOnGrab disabled (ISDK ma wlasne haptics)");
            }
            return changed;
        }

        private static bool AddISDKGrabbable(GameObject root)
        {
            var grabbableType = System.Type.GetType(
                "Oculus.Interaction.Grabbable, Oculus.Interaction");
            if (grabbableType == null)
            {
                Debug.LogWarning($"{LOG} Oculus.Interaction.Grabbable type not found");
                return false;
            }
            if (root.GetComponent(grabbableType) != null)
            {
                Debug.Log($"{LOG} [OK] Grabbable already on Shotgun root");
                return false;
            }
            root.AddComponent(grabbableType);
            Debug.Log($"{LOG} [ADDED] Oculus.Interaction.Grabbable");
            return true;
        }

        private static bool AddISDKHandGrabInteractable(GameObject root)
        {
            var interactableType = System.Type.GetType(
                "Oculus.Interaction.HandGrab.HandGrabInteractable, Oculus.Interaction.HandGrab");
            if (interactableType == null)
            {
                Debug.LogWarning($"{LOG} Oculus.Interaction.HandGrab.HandGrabInteractable type not found");
                return false;
            }
            if (root.GetComponent(interactableType) != null)
            {
                Debug.Log($"{LOG} [OK] HandGrabInteractable already on Shotgun root");
                return false;
            }
            root.AddComponent(interactableType);
            Debug.Log($"{LOG} [ADDED] HandGrabInteractable (user skalibruj grip pose w Inspectorze)");
            return true;
        }
    }
}
#endif
