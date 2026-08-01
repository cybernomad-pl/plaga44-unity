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
            changed |= SetupPositionPersistence(rig, cfg);
            changed |= PositionRig(rig, cfg);
            changed |= SetupFingerFreezer(rig);
            return changed;
        }

        // HandFingerFreezer -- added to SDK char (same GO as Animator).
        // Locks finger bones while PlagaGrabbable held -- stops "flapping fingers".
        private static bool SetupFingerFreezer(GameObject rig)
        {
            var avatar = rig.GetComponent<PlayerAvatar>();
            if (avatar == null || avatar.defaultRig == null)
            {
                Debug.LogWarning($"{LOG} [SKIP] HandFingerFreezer: no defaultRig");
                return false;
            }
            var sdkChar = avatar.defaultRig;
            if (sdkChar.GetComponent<Plaga44.Inventory.HandFingerFreezer>() != null)
            {
                Debug.Log($"{LOG} [OK] HandFingerFreezer (on {sdkChar.name})");
                return false;
            }
            Undo.AddComponent<Plaga44.Inventory.HandFingerFreezer>(sdkChar);
            Debug.Log($"{LOG} [ADDED] HandFingerFreezer on {sdkChar.name}");
            return true;
        }

        // PlayerPositionPersistence: restore last session position.
        // Disabled if stratoJumpHeight > 0 (we want StratoJump each session).
        private static bool SetupPositionPersistence(GameObject rig, BootstrapConfig cfg)
        {
            bool shouldHave = cfg.savePlayerPosition && cfg.stratoJumpHeight <= 0f;
            var existing = rig.GetComponent<PlayerPositionPersistence>();

            if (shouldHave)
            {
                if (existing != null)
                {
                    Debug.Log($"{LOG} [OK] PlayerPositionPersistence (already present)");
                    return false;
                }
                Undo.AddComponent<PlayerPositionPersistence>(rig);
                Debug.Log($"{LOG} [ADDED] PlayerPositionPersistence (savePlayerPosition=true)");
                return true;
            }
            else if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log($"{LOG} [REMOVED] PlayerPositionPersistence (StratoJump mode OR savePlayerPosition=false)");
                return true;
            }
            return false;
        }

        // Position rig: srodek WhiteRoom, na podlodze (TOP podlogi = Y0 pokoju).
        // cfg.stratoJumpHeight > 0 -> spawn tyle metrow nad podloga (free-fall).
        // ZERO FALLBACK: brak WhiteRoom/Floor -> LogError + nie pozycjonuje
        // (caller widzi blad, nie zgadujemy gruntu).
        private static bool PositionRig(GameObject rig, BootstrapConfig cfg)
        {
            var room = GameObject.Find("WhiteRoom");
            var floor = room != null ? room.transform.Find("Floor") : null;
            if (floor == null)
            {
                Debug.LogError($"{LOG} [FAIL] brak WhiteRoom/Floor w scenie -- nie pozycjonuje rigu (faza 1-WhiteRoom przed 6-PlayerRig?)");
                return false;
            }

            // TOP podlogi = srodek + polowa grubosci (scale.y).
            float groundY = floor.position.y + floor.lossyScale.y / 2f;
            float targetY = groundY + cfg.stratoJumpHeight;
            Vector3 target = new Vector3(floor.position.x, targetY, floor.position.z);
            string mode = cfg.stratoJumpHeight > 0f ? $"StratoJump +{cfg.stratoJumpHeight:F0}m" : "Ground snap";

            var cur = rig.transform.position;
            if ((cur - target).sqrMagnitude < 0.0001f)
            {
                Debug.Log($"{LOG} [OK] Rig already at target ({mode})");
                return false;
            }

            Undo.RecordObject(rig.transform, "PlayerRigSetup position rig");
            rig.transform.position = target;
            Debug.Log($"{LOG} [FIX] Rig -> ({target.x:F0},{target.y:F2},{target.z:F0}) ground={groundY:F2} (WhiteRoom) {mode}");
            return true;
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
