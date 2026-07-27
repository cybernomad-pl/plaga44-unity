#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Plaga44.Npc;

namespace Plaga44.Editor.Setup
{
    public static class NpcSpawnerSetup
    {
        private const string LOG = "[PLAGA44][NpcSpawnerSetup]";
        private const string GoName = "_NpcSpawner";

        public static bool Run(BootstrapConfig cfg)
        {
            if (Object.FindAnyObjectByType<NpcSpawner>() != null)
            {
                Debug.Log($"{LOG} [OK] NpcSpawner");
                return false;
            }

            var go = new GameObject(GoName);
            Undo.RegisterCreatedObjectUndo(go, "Bootstrap: Add NpcSpawner");
            go.AddComponent<NpcSpawner>();
            Debug.Log($"{LOG} [ADDED] NpcSpawner");
            return true;
        }
    }
}
#endif
