// =============================================================================
// NaNTransformFinder.cs
// CYBERNOMAD / PLAGA44 -- diagnostyka: znajdz GameObjecty z NaN/Infinity
// w transform.position, .rotation, .scale albo w Renderer.bounds.
//
// Menu: PLAGA44/Diag/Find NaN Transforms
// Dziala w Edit Mode i w Play Mode. Loguje kazdy znaleziony winny obiekt
// z pelna sciezka hierarchii.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class NaNTransformFinder
    {
        private const string LOG = "[PLAGA44][NaNFinder]";

        [MenuItem("PLAGA44/Diag/Find NaN Transforms")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            int totalTransforms = 0;
            int badTransforms = 0;
            int badRenderers  = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    totalTransforms++;
                    string problems = InspectTransform(t);
                    if (!string.IsNullOrEmpty(problems))
                    {
                        badTransforms++;
                        Debug.LogWarning($"{LOG} [TRANSFORM] '{GetPath(t)}' -- {problems}", t);
                    }
                }
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!IsFiniteVector3(r.bounds.center) || !IsFiniteVector3(r.bounds.extents))
                    {
                        badRenderers++;
                        Debug.LogWarning($"{LOG} [RENDERER] '{GetPath(r.transform)}' -- bounds center={r.bounds.center} extents={r.bounds.extents}", r);
                    }
                }
            }

            Debug.Log($"{LOG} SCAN DONE: {totalTransforms} transforms, {badTransforms} z NaN/Inf pozycji, {badRenderers} rendererow z NaN bounds");
            if (badTransforms == 0 && badRenderers == 0)
                Debug.Log($"{LOG} [OK] zadnych NaN/Inf -- problem NIE w scenie teraz. Moze runtime-only (Play Mode component update)");
        }

        private static string InspectTransform(Transform t)
        {
            var pos   = t.position;
            var lpos  = t.localPosition;
            var rot   = t.rotation;
            var lrot  = t.localRotation;
            var lscl  = t.localScale;

            var problems = new System.Text.StringBuilder();
            if (!IsFiniteVector3(pos))   problems.Append($" pos={pos};");
            if (!IsFiniteVector3(lpos))  problems.Append($" lpos={lpos};");
            if (!IsFiniteQuat(rot))      problems.Append($" rot={rot};");
            if (!IsFiniteQuat(lrot))     problems.Append($" lrot={lrot};");
            if (!IsFiniteVector3(lscl))  problems.Append($" scl={lscl};");
            return problems.ToString();
        }

        private static bool IsFiniteVector3(Vector3 v)
            => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

        private static bool IsFiniteQuat(Quaternion q)
            => IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);

        private static bool IsFinite(float f)
            => !float.IsNaN(f) && !float.IsInfinity(f);

        private static string GetPath(Transform t)
        {
            if (t.parent == null) return t.name;
            return GetPath(t.parent) + "/" + t.name;
        }
    }
}
#endif
