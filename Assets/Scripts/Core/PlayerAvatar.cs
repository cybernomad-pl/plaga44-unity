// =============================================================================
// PlayerAvatar.cs
// CYBERNOMAD -- Avatar gracza podpiety pod OVRCameraRig.
//
// Spawnuje PLAYER_rigged z Resources lub Assets/Characters/Player/,
// ustawia jako dziecko riga, mapuje glowe na CenterEyeAnchor,
// rece na HandAnchors. W edytorze bez headsetu -- statyczny model.
//
// Sub-mesh visibility:
//   Body, Eyes, Eyelashes -- UKRYTE (first person)
//   Eyewear              -- UKRYTE domyslnie (w inventory)
//   Masks                -- widoczna, szybka z przezroczystym materialem
//   Shoes, Tops, Bottoms, Hats, Gloves -- widoczne normalnie
//
// Public API:
//   PlayerAvatar.Instance.SetSubmeshVisible("Hats", false); -- zdejmij czapke
//   PlayerAvatar.Instance.IsSubmeshVisible("Hats");         -- sprawdz
//   PlayerAvatar.Instance.GetAllSubmeshNames();              -- lista sub-meshes
//   PlayerAvatar.Instance.AvatarRoot                        -- root GO modelu
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class PlayerAvatar : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Avatar]";
        private const string AVATAR_PATH = "Characters/Player/PLAYER_rigged";

        // =====================================================================
        // Singleton
        // =====================================================================

        public static PlayerAvatar Instance { get; private set; }

        // =====================================================================
        // Config
        // =====================================================================

        [Header("Config")]
        [Tooltip("Skala modelu (Fuse OBJ = centymetry, potrzebuje 0.01)")]
        public float modelScale = 0.01f;

        [Tooltip("Offset Y modelu wzgledem riga (stopy na podlodze)")]
        public float yOffset = -1.65f;

        [Tooltip("Ukryj glowe/szyje w first person")]
        public bool hideHeadInFirstPerson = true;

        // =====================================================================
        // State
        // =====================================================================

        private GameObject _avatarInstance;
        private Transform _headBone;
        private Transform _neckBone;
        private Transform _hipsBone;
        private Transform _headAnchor;
        private Transform _leftHandAnchor;
        private Transform _rightHandAnchor;
        private Animator _animator;

        // Sub-mesh renderers indexed by OBJ group name
        private readonly Dictionary<string, Renderer> _submeshRenderers = new Dictionary<string, Renderer>();

        /// <summary>Root GameObject of the spawned avatar model.</summary>
        public GameObject AvatarRoot => _avatarInstance;

        // Sub-meshes that are ALWAYS hidden in first person (body under suit)
        private static readonly string[] ALWAYS_HIDDEN = { "Body", "Eyes", "Eyelashes" };

        // Sub-meshes hidden by default (inventory items -- player can toggle)
        private static readonly string[] DEFAULT_HIDDEN = { "Eyewear" };

        // Sub-mesh name for gas mask lens that gets transparent material
        private const string MASK_SUBMESH = "Masks";

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{LOG} Duplikat -- niszcze.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Debug.Log($"{LOG} Start: spawning avatar...");

            SpawnAvatar();
            IndexSubmeshes();
            ApplyDefaultVisibility();
            ApplyMaskLensMaterial();
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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // Spawn
        // =====================================================================

        private void SpawnAvatar()
        {
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

            _animator = _avatarInstance.GetComponent<Animator>();
            if (_animator != null && _animator.avatar != null)
                Debug.Log($"{LOG} Animator: isHuman={_animator.avatar.isHuman}");
            else
                Debug.Log($"{LOG} Brak Animator lub Avatar na modelu");

            Debug.Log($"{LOG} Spawned: scale={modelScale}, yOffset={yOffset}");
        }

        // =====================================================================
        // Sub-mesh indexing & visibility
        // =====================================================================

        /// <summary>
        /// Index all child renderers by their GameObject name (OBJ group name).
        /// </summary>
        private void IndexSubmeshes()
        {
            if (_avatarInstance == null) return;

            _submeshRenderers.Clear();
            foreach (var renderer in _avatarInstance.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.gameObject.name;
                _submeshRenderers[name] = renderer;
                Debug.Log($"{LOG} Indexed sub-mesh: {name}");
            }
        }

        /// <summary>
        /// Apply default visibility: hide body parts, hide default-off items.
        /// </summary>
        private void ApplyDefaultVisibility()
        {
            // Always hidden (body under suit)
            foreach (var hideName in ALWAYS_HIDDEN)
            {
                foreach (var kvp in _submeshRenderers)
                {
                    if (kvp.Key == hideName || kvp.Key.Contains(hideName))
                    {
                        kvp.Value.enabled = false;
                        Debug.Log($"{LOG} Ukryto (always): {kvp.Key}");
                    }
                }
            }

            // Default hidden (inventory toggleable)
            foreach (var hideName in DEFAULT_HIDDEN)
            {
                foreach (var kvp in _submeshRenderers)
                {
                    if (kvp.Key == hideName || kvp.Key.Contains(hideName))
                    {
                        kvp.Value.enabled = false;
                        Debug.Log($"{LOG} Ukryto (default off): {kvp.Key}");
                    }
                }
            }
        }

        /// <summary>
        /// Apply a transparent green-tinted material to the mask lens sub-mesh.
        /// URP/Lit, Surface Type: Transparent, alpha 0.03, slight green tint.
        /// </summary>
        private void ApplyMaskLensMaterial()
        {
            if (_avatarInstance == null) return;

            Renderer maskRenderer = null;
            foreach (var kvp in _submeshRenderers)
            {
                if (kvp.Key == MASK_SUBMESH || kvp.Key.Contains(MASK_SUBMESH))
                {
                    maskRenderer = kvp.Value;
                    break;
                }
            }

            if (maskRenderer == null)
            {
                Debug.Log($"{LOG} Sub-mesh '{MASK_SUBMESH}' nie znaleziony -- pomijam lens material.");
                return;
            }

            // Szukamy lens material wsrod istniejacych materialow na renderze.
            // Jesli mesh ma wiele materialow, szybka/lens to zwykle osobny material slot.
            // Tworzymy transparentny material i podmieniamy odpowiedni slot.
            var mats = maskRenderer.sharedMaterials;
            bool foundLens = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.ToLower().Contains("lens"))
                {
                    mats[i] = CreateTransparentLensMaterial();
                    foundLens = true;
                    Debug.Log($"{LOG} Podmieniono lens material na slot {i}");
                }
            }

            // Jesli nie znaleziono dedykowanego lens materialu, dodaj na ostatnim slocie
            // Ale tylko jesli jest wiecej niz 1 material (multi-material mesh)
            if (!foundLens && mats.Length > 1)
            {
                // Podmien ostatni material (czesto lens jest ostatni)
                mats[mats.Length - 1] = CreateTransparentLensMaterial();
                Debug.Log($"{LOG} Podmieniono ostatni material slot na Masks jako lens");
            }
            else if (!foundLens)
            {
                // Mesh ma jeden material -- nie podmieniamy calego, tylko logujemy
                Debug.Log($"{LOG} Masks ma 1 material, nie podmieniam (caly mesh bylby przezroczysty)");
            }

            maskRenderer.sharedMaterials = mats;
        }

        /// <summary>
        /// Create a URP/Lit transparent material for the gas mask lens.
        /// </summary>
        private static Material CreateTransparentLensMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning($"{LOG} URP/Lit shader nie znaleziony -- fallback Standard");
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader);
            mat.name = "MaskLens_Transparent";

            // URP/Lit transparent setup
            mat.SetFloat("_Surface", 1f);         // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 0f);            // 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply
            mat.SetFloat("_AlphaClip", 0f);        // no alpha clip
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0.9f);     // szklo jest gladkie
            mat.SetFloat("_Metallic", 0f);

            // Zielonkawy tint, prawie niewidoczny (alpha = 0.03)
            mat.SetColor("_BaseColor", new Color(0.4f, 0.7f, 0.3f, 0.03f));

            // Render queue for transparent
            mat.renderQueue = (int)RenderQueue.Transparent;

            // Enable transparent keywords
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");

            return mat;
        }

        // =====================================================================
        // Public API -- sub-mesh visibility (used by InventoryScreen)
        // =====================================================================

        /// <summary>
        /// Set visibility of a sub-mesh by name. Does NOT affect always-hidden
        /// sub-meshes (Body, Eyes, Eyelashes) -- those stay hidden in gameplay.
        /// </summary>
        public void SetSubmeshVisible(string submeshName, bool visible)
        {
            // Block toggling always-hidden submeshes
            foreach (var h in ALWAYS_HIDDEN)
            {
                if (submeshName == h || submeshName.Contains(h))
                {
                    Debug.LogWarning($"{LOG} Cannot toggle always-hidden sub-mesh: {submeshName}");
                    return;
                }
            }

            foreach (var kvp in _submeshRenderers)
            {
                if (kvp.Key == submeshName || kvp.Key.Contains(submeshName))
                {
                    kvp.Value.enabled = visible;
                    Debug.Log($"{LOG} Sub-mesh '{kvp.Key}' visible={visible}");
                    return;
                }
            }

            Debug.LogWarning($"{LOG} Sub-mesh '{submeshName}' nie znaleziony.");
        }

        /// <summary>Check if a sub-mesh is currently visible.</summary>
        public bool IsSubmeshVisible(string submeshName)
        {
            foreach (var kvp in _submeshRenderers)
            {
                if (kvp.Key == submeshName || kvp.Key.Contains(submeshName))
                    return kvp.Value.enabled;
            }
            return false;
        }

        /// <summary>Get all indexed sub-mesh names.</summary>
        public List<string> GetAllSubmeshNames()
        {
            return new List<string>(_submeshRenderers.Keys);
        }

        /// <summary>Get renderer for a sub-mesh by name.</summary>
        public Renderer GetSubmeshRenderer(string submeshName)
        {
            foreach (var kvp in _submeshRenderers)
            {
                if (kvp.Key == submeshName || kvp.Key.Contains(submeshName))
                    return kvp.Value;
            }
            return null;
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

            _headBone.localScale = Vector3.zero;
            if (_neckBone != null)
                _neckBone.localScale = new Vector3(1, 1, 0.01f);

            Debug.Log($"{LOG} Head/neck hidden (first person)");
        }
    }
}
