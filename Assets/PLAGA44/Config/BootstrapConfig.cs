// =============================================================================
// BootstrapConfig.cs
// ScriptableObject -- konfiguracja bootstrapu sceny PLAGA '44.
// Czytany przez Bootstrap i klasy Setup. Nie trafia do builda (Editor only).
// Tworz przez: Assets > Create > PLAGA44 > Bootstrap Config
// =============================================================================
using UnityEngine;

namespace Plaga44
{
    [CreateAssetMenu(menuName = "PLAGA44/Bootstrap Config", fileName = "BootstrapConfig")]
    public class BootstrapConfig : ScriptableObject
    {
        [Header("Scene")]
        public string scenePath = "Assets/PLAGA44/TESTBED_V6.unity";

        [Header("Terrain")]
        public string terrainAssetPath = "Assets/Potok/Terrain/Scene_A_Terrain.asset";
        public string terrainMaterialPath = "Assets/PLAGA44/Materials/TerrainLit.mat";
        public string terrainLayersFolder = "Assets/PLAGA44/TerrainLayers";

        [Header("Skybox")]
        public string skyboxMatPath = "Assets/Potok/Skybox/BGR_Sky1.mat";

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
        public float spawnAboveTerrain = 1000f;
        [Tooltip("Gracz spawnuje 1km nad terenem i opada w Play Mode. Wylacz jesli chcesz testowac bez skoku.")]
        public bool stratoJump = true;

        [Header("Grab Volume")]
        public float grabVolumeRadius = 0.08f;

        [Header("Avatar Registry")]
        public string avatarRegistryPath = "Assets/PLAGA44/Resources/AvatarRegistry.asset";
    }
}
