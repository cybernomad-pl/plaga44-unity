// =============================================================================
// PlayerRigSetup.cs
// Konfiguruje OVRCameraRig: CharacterController, LocomotionController,
// SmoothTurnController, PlayerAvatar. Wywolywany przez Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Plaga44.Locomotion;

namespace Plaga44.Editor.Setup
{
    public static class PlayerRigSetup
    {
        private const string LOG = "[PLAGA44][PlayerRigSetup]";
        private const string OvrRigName = "OVRCameraRig";
        private const string DefaultRigName = "StylizedCharacterLocomotion";
        private const string DefaultRigPartial = "StylizedCharacter";

        public static bool Run(BootstrapConfig cfg)
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] {OvrRigName} not found in scene");
                return false;
            }

            bool changed = false;
            changed |= SetupCharacterController(rig, cfg);
            changed |= SetupLocomotion(rig, cfg);
            changed |= SetupSmoothTurn(rig, cfg);
            changed |= SetupPlayerAvatar(rig);
            // StratoJump removed -- no spawn repositioning.
            return changed;
        }

        private static bool SetupCharacterController(GameObject rig, BootstrapConfig cfg)
        {
            var cc = rig.GetComponent<CharacterController>();
            if (cc != null)
            {
                Debug.Log($"{LOG} [OK] CharacterController");
                return false;
            }
            cc = Undo.AddComponent<CharacterController>(rig);
            cc.height = cfg.ccHeight;
            cc.radius = cfg.ccRadius;
            cc.center = cfg.ccCenter;
            cc.skinWidth = cfg.ccSkinWidth;
            cc.stepOffset = cfg.ccStepOffset;
            Debug.Log($"{LOG} [ADDED] CharacterController (h={cfg.ccHeight} r={cfg.ccRadius})");
            return true;
        }

        private static bool SetupLocomotion(GameObject rig, BootstrapConfig cfg)
        {
            var loco = rig.GetComponent<LocomotionController>();
            if (loco != null)
            {
                Debug.Log($"{LOG} [OK] LocomotionController");
                return false;
            }
            loco = Undo.AddComponent<LocomotionController>(rig);
            loco.moveSpeed = cfg.moveSpeed;
            loco.strafeFactor = cfg.strafeFactor;
            Debug.Log($"{LOG} [ADDED] LocomotionController (speed={cfg.moveSpeed})");
            return true;
        }

        private static bool SetupSmoothTurn(GameObject rig, BootstrapConfig cfg)
        {
            var turn = rig.GetComponent<SmoothTurnController>();
            if (turn != null)
            {
                Debug.Log($"{LOG} [OK] SmoothTurnController");
                return false;
            }
            turn = Undo.AddComponent<SmoothTurnController>(rig);
            turn.turnSpeed = cfg.turnSpeed;
            turn.deadZone = cfg.turnDeadZone;
            Debug.Log($"{LOG} [ADDED] SmoothTurnController (turn={cfg.turnSpeed} deg/s)");
            return true;
        }

        private static bool SetupPlayerAvatar(GameObject rig)
        {
            bool changed = false;
            var avatar = rig.GetComponent<PlayerAvatar>();
            if (avatar == null)
            {
                avatar = Undo.AddComponent<PlayerAvatar>(rig);
                changed = true;
                Debug.Log($"{LOG} [ADDED] PlayerAvatar");
            }

            if (avatar.avatarMode != 0)
            {
                avatar.avatarMode = 0;
                changed = true;
                Debug.Log($"{LOG} [FIX] avatarMode -> 0");
            }

            // Clear persisted PlayerPrefs avatarMode -- otherwise PlayerAvatar.Start()
            // will restore old broken mode (e.g. PINEA when its rig is broken).
            // Bootstrap = fresh start, runtime persistence kicks in after first menu change.
            const string AvatarPrefsKey = "Plaga44_Current_AVATAR_Mode";
            if (PlayerPrefs.HasKey(AvatarPrefsKey))
            {
                PlayerPrefs.DeleteKey(AvatarPrefsKey);
                PlayerPrefs.Save();
                Debug.Log($"{LOG} [FIX] Cleared persisted AVATAR_Mode PlayerPrefs (fresh Bootstrap)");
            }
            if (avatar.avatarPrefab != null)
            {
                avatar.avatarPrefab = null;
                changed = true;
                Debug.Log($"{LOG} [FIX] cleared legacy avatarPrefab");
            }
            // ALWAYS re-resolve defaultRig from scene -- old reference may be stale after scene reload
            var foundRig = GameObject.Find(DefaultRigName)
                ?? FindChildContaining(rig.transform, DefaultRigPartial);
            if (foundRig != null)
            {
                if (avatar.defaultRig != foundRig)
                {
                    avatar.defaultRig = foundRig;
                    changed = true;
                    Debug.Log($"{LOG} [FIX] defaultRig -> {foundRig.name} (path={GetGameObjectPath(foundRig)})");
                }
                else
                {
                    Debug.Log($"{LOG} [OK] defaultRig already wired to {foundRig.name}");
                }
                if (!foundRig.activeSelf)
                {
                    foundRig.SetActive(true);
                    changed = true;
                    Debug.Log($"{LOG} [FIX] defaultRig activated (was inactive)");
                }
            }
            else
            {
                Debug.LogError($"{LOG} [MISSING] defaultRig '{DefaultRigName}' not found in scene -- "
                    + "SDK char prefab missing or scene corrupted");
            }

            if (!changed) Debug.Log($"{LOG} [OK] PlayerAvatar");
            return changed;
        }

        // Walk parents to print full hierarchy path -- useful for debugging defaultRig resolution
        private static string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "<null>";
            string path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        // StratoJump removed -- player spawns at scene position (saved or default).

        private static GameObject FindChildContaining(Transform root, string partial)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.Contains(partial)) return t.gameObject;
            return null;
        }
    }
}
#endif
