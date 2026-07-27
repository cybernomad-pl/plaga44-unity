#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    // Klonuje tafle wody (3D_Water z Environment), zaklada MeshCollider na kazdy
    // kafel i opuszcza klon o 1.2m -- gracz stoi na tym colliderze i brodzi
    // w wodzie glownej (tafla wyzej). Idempotent.
    public static class WadeWaterSetup
    {
        private const string LOG = "[PLAGA44][WadeWaterSetup]";
        private const string Name = "Water_Wade";
        private const string EnvName = "Environment";
        private const float DropMeters = 1.2f;

        public static bool Run(BootstrapConfig cfg)
        {
            if (GameObject.Find(Name) != null) return false;

            var env = GameObject.Find(EnvName);
            if (env == null)
            {
                Debug.LogError($"{LOG} brak {EnvName} w scenie -- najpierw faza Water.");
                return false;
            }

            var clone = Object.Instantiate(env);
            clone.name = Name;
            SceneManager.MoveGameObjectToScene(clone, SceneManager.GetActiveScene());

            // Zostaw tylko tafle wody (3D_Water), reszta (SUN itp) precz.
            foreach (var child in new List<Transform>(GetDirectChildren(clone.transform)))
                if (!child.name.StartsWith("3D_Water", System.StringComparison.Ordinal))
                    Object.DestroyImmediate(child.gameObject);

            // MeshCollider na kazdym kaflu wody.
            int colliders = 0;
            foreach (var mf in clone.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                colliders++;
            }

            // Opusc o 1.2m -- gracz brodzi.
            clone.transform.position += Vector3.down * DropMeters;

            Debug.Log($"{LOG} [OK] {Name} -1.2m, {colliders} MeshCollider(ow).");
            return true;
        }

        private static IEnumerable<Transform> GetDirectChildren(Transform t)
        {
            for (int i = 0; i < t.childCount; i++) yield return t.GetChild(i);
        }
    }
}
#endif
