// =============================================================================
// PredatorHandsSetup.cs
// Przypina material PredatorHands do wszystkich Skinned/MeshRenderer zawierajacych
// "hand" / "Hand" w nazwie pod StylizedCharacterLocomotion (SDK char).
// Odpala sie z menu "PLAGA44/Setup/Apply Predator Hands" -- na razie nie wpiete w Pipeline.
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class PredatorHandsSetup
    {
        private const string LOG          = "[PLAGA44][PredatorHands]";
        private const string ShaderName   = "PLAGA44/PredatorHands";
        private const string MaterialPath = "Assets/PLAGA44/Materials/PredatorHands.mat";
        private const string RigPartial   = "StylizedCharacter";
        private const string HandPartial  = "hand";

        [MenuItem("PLAGA44/Setup/Apply Predator Hands")]
        public static void ApplyFromMenu()
        {
            int n = Apply();
            EditorUtility.DisplayDialog(
                "Predator Hands",
                $"Podpiety material do {n} rendererow.",
                "OK");
        }

        [MenuItem("PLAGA44/Setup/Revert Predator Hands (restore original materials)")]
        public static void RevertFromMenu()
        {
            int n = Revert();
            EditorUtility.DisplayDialog(
                "Predator Hands",
                $"Przywrocono oryginalne materialy na {n} rendererach.",
                "OK");
        }

        // --- public API (could be wired into Pipeline.cs later) ----------------

        public static int Apply()
        {
            var mat = ResolveMaterial();
            if (mat == null) { Debug.LogError($"{LOG} [MISSING] material at {MaterialPath}"); return 0; }

            var renderers = FindHandRenderers();
            if (renderers.Count == 0)
            {
                Debug.LogWarning($"{LOG} [SKIP] no hand renderers found under '{RigPartial}'");
                return 0;
            }

            int changed = 0;
            foreach (var r in renderers)
            {
                // Backup original once
                var backupKey = BackupKey(r);
                if (!EditorPrefs.HasKey(backupKey))
                {
                    var orig = r.sharedMaterial;
                    var origPath = orig != null ? AssetDatabase.GetAssetPath(orig) : "";
                    EditorPrefs.SetString(backupKey, origPath);
                }

                Undo.RecordObject(r, "PredatorHands apply");
                r.sharedMaterial = mat;
                EditorUtility.SetDirty(r);
                changed++;
                Debug.Log($"{LOG} [APPLY] {r.name} ({r.GetType().Name})");
            }
            return changed;
        }

        public static int Revert()
        {
            int restored = 0;
            foreach (var r in FindHandRenderers())
            {
                var backupKey = BackupKey(r);
                if (!EditorPrefs.HasKey(backupKey)) continue;

                var origPath = EditorPrefs.GetString(backupKey);
                if (string.IsNullOrEmpty(origPath)) continue;

                var orig = AssetDatabase.LoadAssetAtPath<Material>(origPath);
                if (orig == null) continue;

                Undo.RecordObject(r, "PredatorHands revert");
                r.sharedMaterial = orig;
                EditorUtility.SetDirty(r);
                EditorPrefs.DeleteKey(backupKey);
                restored++;
                Debug.Log($"{LOG} [REVERT] {r.name} -> {orig.name}");
            }
            return restored;
        }

        // --- helpers ------------------------------------------------------------

        private static Material ResolveMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null) return null;

            // If .mat was created without a resolved shader GUID, patch it here.
            var shader = Shader.Find(ShaderName);
            if (shader != null && (mat.shader == null || mat.shader.name != ShaderName))
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssetIfDirty(mat);
                Debug.Log($"{LOG} [FIX] shader wired -> {ShaderName}");
            }
            else if (shader == null)
            {
                Debug.LogError($"{LOG} [MISSING] shader '{ShaderName}' not compiled yet");
            }
            return mat;
        }

        private static List<Renderer> FindHandRenderers()
        {
            var list = new List<Renderer>();
            foreach (var root in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (root == null) continue;
                if (!root.name.Contains(RigPartial)) continue;

                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (r.name.ToLower().Contains(HandPartial))
                        list.Add(r);
                }
            }
            return list;
        }

        private static string BackupKey(Renderer r)
        {
            return $"PLAGA44_PredatorHands_Backup_{r.GetInstanceID()}";
        }
    }
}
#endif
