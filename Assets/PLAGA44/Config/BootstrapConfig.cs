// =============================================================================
// BootstrapConfig.cs
// ScriptableObject -- konfiguracja bootstrapu sceny PLAGA '44.
// Czytany przez Bootstrap i klasy Setup. Nie trafia do builda (Editor only).
// Tworz przez: Assets > Create > PLAGA44 > Bootstrap Config
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44
{
    [CreateAssetMenu(menuName = "PLAGA44/Bootstrap Config", fileName = "BootstrapConfig")]
    public class BootstrapConfig : ScriptableObject
    {
        [Header("Scene")]
        public string scenePath = "Assets/PLAGA44/TESTBED.unity";

        [Header("Terrain")]
        public string terrainAssetPath = "Assets/PLAGA44/Terrain/Scene_A_Terrain.asset";
        public string terrainMaterialPath = "Assets/PLAGA44/Materials/TerrainLit.mat";
        public string terrainLayersFolder = "Assets/PLAGA44/TerrainLayers";
        [Tooltip("Horizontal scale multiplier (X,Z) applied to terrain size. 1.0 = default. 2.0 = 2x wider in both directions.")]
        public float terrainHorizontalScale = 2.0f;

        [Header("Skybox")]
        public string skyboxMatPath = "Assets/PLAGA44/Skybox/BGR_Sky1.mat";

        [Header("Fog")]
        public bool     fogEnabled       = true;
        public FogMode  fogMode          = FogMode.ExponentialSquared;
        public Color    fogColor         = new Color(0.7f, 0.8f, 0.9f, 1f);
        public float    fogDensity       = 0.01f;
        public float    fogStartDistance = 0f;
        public float    fogEndDistance   = 300f;

        [Header("Ambient")]
        public AmbientMode ambientMode         = AmbientMode.Skybox;
        [Tooltip("Tryb Skybox: ambientIntensity mnozy skybox IBL. Inne pola kolorow ignorowane.")]
        public float       ambientIntensity    = 1f;
        [Tooltip("Tryb Flat: jednolity kolor ambient.")]
        public Color       ambientLight        = new Color(0.3f, 0.3f, 0.3f, 1f);
        [Tooltip("Tryb Trilight: gradient sky/equator/ground.")]
        public Color       ambientSkyColor     = new Color(0.5f, 0.7f, 1f, 1f);
        public Color       ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        public Color       ambientGroundColor  = new Color(0.2f, 0.2f, 0.15f, 1f);

        [Header("Directional Light")]
        public Color sunColor = new Color(1f, 0.95f, 0.84f);
        public float sunIntensity = 1f;
        public Vector3 sunRotation = new Vector3(50f, -30f, 0f);
        public LightShadows sunShadows = LightShadows.Soft;

        [Header("Character Controller")]
        public float ccHeight = 1.8f;
        public float ccRadius = 0.3f;
        public Vector3 ccCenter = new Vector3(0f, 0.9f, 0f);
        public float ccSkinWidth = 0.08f;
        public float ccStepOffset = 0.5f;

        [Header("Locomotion")]
        public float moveSpeed = 2.5f;
        public float strafeFactor = 0.8f;
        public float turnSpeed = 120f;
        public float turnDeadZone = 0.15f;

        [Header("Sky Rotator")]
        public float skyRotationSpeed = 0.5f;

        [Header("Player Spawn")]
        [Tooltip("StratoJump height (meters above ground). 0 = snap to ground. >0 = spawn at this altitude (fun drop). Overrides savePlayerPosition.")]
        public float stratoJumpHeight = 1000f;
        [Tooltip("If true AND stratoJumpHeight=0 -- restore last session position from PlayerPrefs.")]
        public bool savePlayerPosition = false;

        [Header("Grab Volume")]
        public float grabVolumeRadius = 0.08f;

        [Header("Bounce Light")]
        [Tooltip("Fill light pointing straight up (ground bounce simulation).")]
        public Color bounceLightColor = new Color(0.6f, 0.65f, 0.75f);
        public float bounceLightIntensity = 0.35f;
        [Tooltip("Rotation X=-90 = straight up. Adjust for angle.")]
        public Vector3 bounceLightRotation = new Vector3(-90f, 0f, 0f);
        public LightShadows bounceLightShadows = LightShadows.None;

        [Header("Object Spawner")]
        [Tooltip("Spawn offset relative to HEAD (eye level). x=right, y=up from eyes (negative=table level), z=forward.")]
        public Vector3 spawnerOffset = new Vector3(0f, -0.5f, 1.2f);
        [Tooltip("Default item Resources path for spawner.")]
        public string defaultSpawnItem = "Items/Revolver";

        [Header("Avatar Registry")]
        public string avatarRegistryPath = "Assets/PLAGA44/Resources/AvatarRegistry.asset";
    }
}
