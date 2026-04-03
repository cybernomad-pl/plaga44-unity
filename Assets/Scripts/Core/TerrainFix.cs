// TerrainFix.cs
// CYBERNOMAD -- Fix terrain settings that are off in scene file.
// Enables shadows, GPU instancing, tree rendering.

using UnityEngine;

public class TerrainFix : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Fix()
    {
        var terrain = FindAnyObjectByType<Terrain>();
        if (terrain == null) return;

        // Enable shadows
        terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        // Enable GPU instancing
        terrain.drawInstanced = true;

        // Enable trees and foliage
        terrain.drawTreesAndFoliage = true;
        terrain.treeDistance = 300f;
        terrain.treeBillboardDistance = 100f;
        terrain.treeCrossFadeLength = 20f;
        terrain.treeMaximumFullLODCount = 50;

        // Details (grass etc)
        terrain.detailObjectDistance = 80f;
        terrain.detailObjectDensity = 0.5f;

        // Tree colliders
        terrain.terrainData.treeInstances = terrain.terrainData.treeInstances; // force refresh

        Debug.Log("[PLAGA44] TerrainFix: shadows ON, GPU instancing ON, trees ON");
    }
}
