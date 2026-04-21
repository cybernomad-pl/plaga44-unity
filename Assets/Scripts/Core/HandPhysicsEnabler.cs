// =============================================================================
// HandPhysicsEnabler.cs
// CYBERNOMAD -- Runtime wlacza OVRSkeleton._enablePhysicsCapsules na kazdym
// OVRHand w scenie. Meta SDK generuje wtedy automatycznie CapsuleCollider +
// Rigidbody (kinematic) na kosciach palcow -> reka blokuje item fizycznie.
//
// Dlaczego runtime a nie editor-time:
//   OVRHandPrefab/OVRSkeleton jest czesto dodawany do sceny w runtime albo
//   aktywowany po ladowaniu (w Meta SDK 83 OVRBuildingBlock etc.). Editor
//   Bootstrap widzi scene PRZED aktywacja -> "No OVRSkeleton in scene".
//
// _enablePhysicsCapsules to private SerializeField. Ustawiamy przez
// reflection PRZED OVRSkeleton zainicjalizuje capsules (InitializeCapsules
// jest wywolywane w Update gdy skeleton valid -- my ustawiamy w Awake
// [RuntimeInitializeOnLoadMethod] zeby zdazyc).
// =============================================================================

using System.Reflection;
using UnityEngine;

namespace Plaga44
{
    public static class HandPhysicsEnabler
    {
        private const string LOG      = "[PLAGA44][HandPhysicsEnabler]";
        private const string PropName = "_enablePhysicsCapsules";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            // Opoznione -- OVRManager/OVRCameraRig moga jeszcze wczytywac
            // prefab hands po AfterSceneLoad. Odpalamy też monitoring w tle.
            EnableAll();
            // Na wypadek pozniejszego dodawania hands przez SDK BuildingBlock,
            // podpinamy Update check.
            var go = new GameObject("_HandPhysicsWatcher");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watcher>();
        }

        public static int EnableAll()
        {
            var skeletons = Object.FindObjectsByType<OVRSkeleton>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (skeletons == null || skeletons.Length == 0) return 0;

            var field = typeof(OVRSkeleton).GetField(PropName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogWarning($"{LOG} Field '{PropName}' not found in OVRSkeleton (SDK version mismatch)");
                return 0;
            }

            int enabled = 0;
            foreach (var sk in skeletons)
            {
                if (sk == null) continue;
                var type = sk.GetSkeletonType();
                // Tylko hands -- body skeleton tez ma OVRSkeleton, ale capsules
                // konfliktowalyby z CharacterRetargeter kosci.
                if (type != OVRSkeleton.SkeletonType.HandLeft
                    && type != OVRSkeleton.SkeletonType.HandRight) continue;

                bool current = (bool)field.GetValue(sk);
                if (current) continue;

                field.SetValue(sk, true);
                enabled++;
                Debug.Log($"{LOG} [ENABLE] {GetPath(sk.transform)} physics capsules");
            }
            if (enabled > 0)
                Debug.Log($"{LOG} runtime-enabled {enabled} hand skeletons");
            return enabled;
        }

        private static string GetPath(Transform t)
        {
            if (t == null) return "<null>";
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        /// <summary>MonoBehaviour ktory co 1s sprawdza czy nowe hand skeletons
        /// nie pojawily sie po lazy init SDK buildingblock. Idempotent.</summary>
        private class Watcher : MonoBehaviour
        {
            private float _nextCheck;
            private void Update()
            {
                if (Time.unscaledTime < _nextCheck) return;
                _nextCheck = Time.unscaledTime + 1f;
                EnableAll();
            }
        }
    }
}
