// =============================================================================
// RemoveDuplicateGrabbers.cs
// CYBERNOMAD -- Deletes dead "missing script" MonoBehaviour stubs that sit
// alongside the REAL PlagaGrabber on the hand objects.
//
// Recon (Assets/PLAGA44/TESTBED_V6.unity) found stubs referencing script guid
// 24b867055d828014684aa3cfcd180e91 -- a guid that no longer maps to any asset
// (the script it pointed to was deleted). Each hand carries one such stub plus
// the live PlagaGrabber (guid 59857497601f2cb48aefbb01cc0e22d1).
//
// Targeting is precise: only GameObjects that ALSO hold a live PlagaGrabber get
// their missing-script components stripped, so unrelated missing scripts
// elsewhere in the scene are left untouched.
//
// Does NOT save -- touched scenes are marked dirty; save manually to persist.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Plaga44.Inventory;

namespace Plaga44.EditorTools
{
    public static class RemoveDuplicateGrabbers
    {
        private const string LOG = "[PLAGA44][RemoveDuplicateGrabbers]";

        [MenuItem("PLAGA44/Cleanup/Remove Duplicate Grabbers")]
        public static void Run()
        {
            int scannedHands = 0;
            int removedStubs = 0;
            var touchedScenes = new HashSet<Scene>();
            var report = new StringBuilder();

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var grabber in root.GetComponentsInChildren<PlagaGrabber>(true))
                    {
                        var go = grabber.gameObject;
                        scannedHands++;

                        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                        if (missing <= 0) continue;

                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                        removedStubs += missing;
                        touchedScenes.Add(scene);
                        report.AppendLine($"  - '{GetHierarchyPath(go)}' (scene '{scene.name}'): removed {missing} missing-script stub(s)");
                    }
                }
            }

            foreach (var scene in touchedScenes)
                EditorSceneManager.MarkSceneDirty(scene);

            if (removedStubs == 0)
            {
                Debug.Log($"{LOG} No duplicate grabber stubs found ({scannedHands} PlagaGrabber object(s) scanned). Nothing to do.");
                return;
            }

            Debug.Log($"{LOG} Removed {removedStubs} duplicate grabber stub(s) from {scannedHands} PlagaGrabber object(s):\n{report}"
                + "Scene(s) marked dirty -- save manually (Ctrl+S) to persist.");
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null)
            {
                sb.Insert(0, t.name + "/");
                t = t.parent;
            }
            return sb.ToString();
        }
    }
}
