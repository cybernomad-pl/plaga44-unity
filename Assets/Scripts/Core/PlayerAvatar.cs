// =============================================================================
// PlayerAvatar.cs
// CYBERNOMAD -- Avatar gracza podpiety pod OVRCameraRig.
//
// Spawnuje PLAYER_rigged z Resources lub Assets/Characters/Player/,
// ustawia jako dziecko riga, mapuje glowe na CenterEyeAnchor,
// rece na HandAnchors. W edytorze bez headsetu -- statyczny model.
//
// Na Questcie z body tracking -- uzywa OVRBody/OVRSkeleton jesli dostepne.
// =============================================================================

using UnityEngine;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class PlayerAvatar : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Avatar]";
        private const string AVATAR_PATH = "Characters/Player/PLAYER_rigged";

        [Header("Config")]
        [Tooltip("Skala modelu (Fuse OBJ = centymetry, potrzebuje 0.01)")]
        public float modelScale = 0.01f;

        [Tooltip("Offset Y modelu wzgledem riga (stopy na podlodze)")]
        public float yOffset = -1.65f;

        [Tooltip("Ukryj glowe/szyje w first person")]
        public bool hideHeadInFirstPerson = true;

        private GameObject _avatarInstance;
        private Transform _headBone;
        private Transform _neckBone;
        private Transform _hipsBone;
        private Transform _headAnchor;
        private Transform _leftHandAnchor;
        private Transform _rightHandAnchor;
        private Animator _animator;

        // Renderer glowy/szyi do ukrycia w FP
        private Renderer[] _headRenderers;

        private void Start()
        {
            Debug.Log($"{LOG} Start: spawning avatar...");

            SpawnAvatar();
            FindAnchors();
            FindBones();

            if (hideHeadInFirstPerson)
                HideHeadBones();

            Debug.Log($"{LOG} Avatar ready: head={_headBone?.name ?? "NULL"}, hips={_hipsBone?.name ?? "NULL"}");
        }

        private void LateUpdate()
        {
            if (_avatarInstance == null) return;

            // Pozycja avatara -- stopy na poziomie riga
            _avatarInstance.transform.position = transform.position + Vector3.up * yOffset;

            // Rotacja avatara -- yaw z riga (nie pitch, nie roll)
            float yaw = transform.eulerAngles.y;
            _avatarInstance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Mapuj glowe na head anchor (kamera VR)
            if (_headBone != null && _headAnchor != null)
            {
                _headBone.rotation = _headAnchor.rotation;
            }

            // Mapuj rece na hand anchors
            if (_hipsBone != null)
            {
                MapHand(_leftHandAnchor, "mixamorig:LeftArm", "mixamorig:LeftForeArm");
                MapHand(_rightHandAnchor, "mixamorig:RightArm", "mixamorig:RightForeArm");
            }
        }

        // =====================================================================
        // Spawn
        // =====================================================================

        private void SpawnAvatar()
        {
            // Laduj z Resources (FBX musi byc w Assets/Resources/ lub podlinkowany)
            var prefab = Resources.Load<GameObject>("PLAYER_rigged");

#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Characters/Player/PLAYER_rigged.fbx");
            }
#endif

            if (prefab == null)
            {
                Debug.LogError($"{LOG} BRAK PLAYER_rigged! Wrzuc do Assets/Resources/ lub Assets/Characters/Player/");
                return;
            }

            _avatarInstance = Instantiate(prefab, transform.position, Quaternion.identity);
            _avatarInstance.name = "PlayerAvatar";
            _avatarInstance.transform.localScale = Vector3.one * modelScale;

            // Animator -- sprawdz czy humanoid
            _animator = _avatarInstance.GetComponent<Animator>();
            if (_animator != null && _animator.avatar != null)
                Debug.Log($"{LOG} Animator: isHuman={_animator.avatar.isHuman}");
            else
                Debug.Log($"{LOG} Brak Animator lub Avatar na modelu");

            Debug.Log($"{LOG} Spawned: scale={modelScale}, yOffset={yOffset}");
        }

        // =====================================================================
        // Anchors (OVRCameraRig)
        // =====================================================================

        private void FindAnchors()
        {
            var tracking = transform.Find("TrackingSpace");
            if (tracking != null)
            {
                _headAnchor = tracking.Find("CenterEyeAnchor");
                _leftHandAnchor = tracking.Find("LeftHandAnchor");
                _rightHandAnchor = tracking.Find("RightHandAnchor");
            }

            if (_headAnchor == null && Camera.main != null)
                _headAnchor = Camera.main.transform;

            Debug.Log($"{LOG} Anchors: head={_headAnchor?.name ?? "NULL"}, " +
                      $"LH={_leftHandAnchor?.name ?? "NULL"}, RH={_rightHandAnchor?.name ?? "NULL"}");
        }

        // =====================================================================
        // Bones
        // =====================================================================

        private void FindBones()
        {
            if (_avatarInstance == null) return;

            _headBone = FindBoneRecursive(_avatarInstance.transform, "mixamorig:Head");
            _neckBone = FindBoneRecursive(_avatarInstance.transform, "mixamorig:Neck");
            _hipsBone = FindBoneRecursive(_avatarInstance.transform, "mixamorig:Hips");

            Debug.Log($"{LOG} Bones found: head={_headBone != null}, neck={_neckBone != null}, hips={_hipsBone != null}");
        }

        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name == boneName) return parent;
            foreach (Transform child in parent)
            {
                var found = FindBoneRecursive(child, boneName);
                if (found != null) return found;
            }
            return null;
        }

        // =====================================================================
        // Hand mapping (basic IK-like)
        // =====================================================================

        private void MapHand(Transform anchor, string upperBoneName, string lowerBoneName)
        {
            if (anchor == null) return;

            var upper = FindBoneRecursive(_avatarInstance.transform, upperBoneName);
            var lower = FindBoneRecursive(_avatarInstance.transform, lowerBoneName);
            if (upper == null || lower == null) return;

            // Prosty look-at: upper bone patrzy w kierunku anchor
            Vector3 dir = anchor.position - upper.position;
            if (dir.sqrMagnitude > 0.001f)
                upper.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, -90, 0);
        }

        // =====================================================================
        // First person -- ukryj glowe
        // =====================================================================

        private void HideHeadBones()
        {
            if (_headBone == null) return;

            // Skaluj glowe i szyje do zera
            _headBone.localScale = Vector3.zero;
            if (_neckBone != null)
                _neckBone.localScale = new Vector3(1, 1, 0.01f); // prawie zero na Z

            Debug.Log($"{LOG} Head/neck hidden (first person)");
        }
    }
}
