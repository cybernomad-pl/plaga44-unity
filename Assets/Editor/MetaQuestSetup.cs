#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44.Editor
{
    public static class MetaQuestSetup
    {
        private const string LOG = "[PLAGA44]";
        private const string META_SDK_VERSION = "81.0.0";

        private static readonly string[][] PackagesToInstall = new[]
        {
            new[] { "com.unity.xr.openxr",       "1.14.0" },
            new[] { "com.unity.xr.meta-openxr",   "2.4.0"  },
            new[] { "com.meta.xr.sdk.core",        META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.interaction",      META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.interaction.ovr", META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.audio",            META_SDK_VERSION },
        };

        [MenuItem("CYBERNOMAD/Meta SDK Setup/1. Setup Meta SDK", false, 1)]
        public static void SetupMetaSDK()
        {
            Debug.Log($"{LOG} === Setup Meta SDK ===");

            AddScopedRegistry();
            AddPackagesToManifest();
            SetPlayerSettings();

            Debug.Log($"{LOG} === DONE -- Unity will now resolve packages ===");
        }

        static void AddScopedRegistry()
        {
            string path = GetManifestPath();
            if (path == null) return;

            string manifest = File.ReadAllText(path);

            if (manifest.Contains("npm.developer.oculus.com"))
            {
                Debug.Log($"{LOG} Registry already present.");
                return;
            }

            int depsIdx = manifest.IndexOf("\"dependencies\"");
            if (depsIdx < 0)
            {
                Debug.LogError($"{LOG} Cannot find 'dependencies' in manifest.json");
                return;
            }

            int lineStart = manifest.LastIndexOf('\n', depsIdx);
            if (lineStart < 0) lineStart = 0;
            else lineStart += 1;

            string registry = @"  ""scopedRegistries"": [
    {
      ""name"": ""Meta XR"",
      ""url"": ""https://npm.developer.oculus.com"",
      ""scopes"": [
        ""com.meta.xr""
      ]
    }
  ],
";
            manifest = manifest.Substring(0, lineStart)
                     + registry
                     + manifest.Substring(lineStart);

            File.WriteAllText(path, manifest);
            Debug.Log($"{LOG} Added Meta XR scoped registry.");
        }

        static void AddPackagesToManifest()
        {
            string path = GetManifestPath();
            if (path == null) return;

            string manifest = File.ReadAllText(path);
            bool changed = false;

            foreach (var pkg in PackagesToInstall)
            {
                if (manifest.Contains(pkg[0]))
                {
                    Debug.Log($"{LOG} {pkg[0]} already in manifest.");
                    continue;
                }

                int depsIdx = manifest.IndexOf("\"dependencies\"");
                int braceIdx = manifest.IndexOf('{', depsIdx);
                if (braceIdx < 0) continue;

                string entry = $"\n    \"{pkg[0]}\": \"{pkg[1]}\",";
                manifest = manifest.Substring(0, braceIdx + 1)
                         + entry
                         + manifest.Substring(braceIdx + 1);
                changed = true;
                Debug.Log($"{LOG} Added {pkg[0]}@{pkg[1]}");
            }

            if (changed)
            {
                File.WriteAllText(path, manifest);
                Debug.Log($"{LOG} Packages added to manifest. Resolving...");
                UnityEditor.PackageManager.Client.Resolve();
            }
            else
            {
                Debug.Log($"{LOG} All packages already in manifest.");
            }
        }

        static void SetPlayerSettings()
        {
            PlayerSettings.companyName = "Cybernomad";
            PlayerSettings.productName = "PLAGA 44";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.cybernomad.plaga44");

            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 20f;
            QualitySettings.lodBias = 1.0f;
            QualitySettings.pixelLightCount = 2;

            Debug.Log($"{LOG} Player/Quality settings configured.");
        }

        [MenuItem("CYBERNOMAD/Meta SDK Setup/2. Switch to Android", false, 2)]
        public static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log($"{LOG} Already on Android.");
                return;
            }

            Debug.Log($"{LOG} Switching to Android...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Setup VR Rig (Controllers)", false, 51)]
        public static void SetupVRSceneControllers()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError($"{LOG} Build target is not Android. Run Step 2 first.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("OVRCameraRig t:prefab");
            if (guids.Length == 0)
            {
                Debug.LogError($"{LOG} OVRCameraRig prefab not found. Run Step 1 first.");
                return;
            }

            string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Could not load OVRCameraRig at: {prefabPath}");
                return;
            }

            var existing = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in existing)
            {
                if (t.name == "OVRCameraRig" || t.name == "XROrigin")
                {
                    Debug.LogWarning($"{LOG} {t.name} already in scene. Skipping.");
                    return;
                }
            }

            var cameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.gameObject.name == "Main Camera")
                {
                    Undo.DestroyObjectImmediate(cam.gameObject);
                    Debug.Log($"{LOG} Deleted Main Camera.");
                    break;
                }
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.position = Vector3.zero;
            rig.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(rig, "Add OVRCameraRig");

            var mgr = rig.GetComponent<OVRManager>();
            if (mgr != null)
            {
                var so = new SerializedObject(mgr);
                var pTrackingOrigin = so.FindProperty("_trackingOriginType");
                if (pTrackingOrigin != null) pTrackingOrigin.intValue = 1; // FloorLevel
                so.ApplyModifiedProperties();
                Debug.Log($"{LOG} OVRManager configured (FloorLevel, controllers only).");
            }

            Selection.activeGameObject = rig;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} VR Scene ready: OVRCameraRig (controllers).");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Setup VR Rig (Hands)", false, 52)]
        public static void SetupVRSceneHands()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError($"{LOG} Build target is not Android. Run Step 2 first.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("OVRCameraRig t:prefab");
            if (guids.Length == 0)
            {
                Debug.LogError($"{LOG} OVRCameraRig prefab not found. Run Step 1 first.");
                return;
            }

            string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Could not load OVRCameraRig at: {prefabPath}");
                return;
            }

            var existing = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in existing)
            {
                if (t.name == "OVRCameraRig" || t.name == "XROrigin")
                {
                    Debug.LogWarning($"{LOG} {t.name} already in scene. Skipping.");
                    return;
                }
            }

            var cameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.gameObject.name == "Main Camera")
                {
                    Undo.DestroyObjectImmediate(cam.gameObject);
                    Debug.Log($"{LOG} Deleted Main Camera.");
                    break;
                }
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.position = Vector3.zero;
            rig.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(rig, "Add OVRCameraRig");

            var mgr = rig.GetComponent<OVRManager>();
            if (mgr != null)
            {
                var so = new SerializedObject(mgr);

                // Tracking origin -- FloorLevel
                SetProperty(so, "_trackingOriginType", 1, "FloorLevel");

                // Controller-driven hand poses type -- ConformingToController
                SetProperty(so, "controllerDrivenHandPosesType", 1, "ConformingToController");

                // Enable simultaneous hands+controllers at startup
                SetProperty(so, "launchSimultaneousHandsControllersOnStartup", true, "SimultaneousHandsControllers");

                // Runtime flag for simultaneous hands+controllers
                SetProperty(so, "SimultaneousHandsAndControllersEnabled", true, "SimultaneousEnabled");

                so.ApplyModifiedProperties();
                Debug.Log($"{LOG} OVRManager configured (FloorLevel + controller-driven hand poses + simultaneous hands&controllers).");
            }

            AddOVRHandPrefabs(rig);

            Selection.activeGameObject = rig;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} VR Scene ready: OVRCameraRig + OVRHandPrefab (controller-driven hands).");
        }

        public static void AddOVRHandPrefabs(GameObject rig)
        {
            string[] guids = AssetDatabase.FindAssets("OVRHandPrefab t:prefab");
            GameObject handPrefab = null;
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("Prefabs/OVRHandPrefab.prefab"))
                {
                    handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (handPrefab != null)
                    {
                        Debug.Log($"{LOG} Found OVRHandPrefab: {p}");
                        break;
                    }
                }
            }

            if (handPrefab == null)
            {
                Debug.LogError($"{LOG} OVRHandPrefab.prefab not found. Is com.meta.xr.sdk.core installed?");
                return;
            }

            Transform leftAnchor = FindChildRecursive(rig.transform, "LeftHandAnchor");
            Transform rightAnchor = FindChildRecursive(rig.transform, "RightHandAnchor");

            if (leftAnchor == null || rightAnchor == null)
            {
                Debug.LogError($"{LOG} LeftHandAnchor or RightHandAnchor not found in OVRCameraRig!");
                return;
            }

            var leftHand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, leftAnchor);
            leftHand.name = "OVRHandPrefab";
            leftHand.transform.localPosition = Vector3.zero;
            leftHand.transform.localRotation = Quaternion.identity;
            ConfigureOVRHand(leftHand, 0); // 0 = HandLeft
            Undo.RegisterCreatedObjectUndo(leftHand, "Add Left OVRHandPrefab");

            var rightHand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, rightAnchor);
            rightHand.name = "OVRHandPrefab";
            rightHand.transform.localPosition = Vector3.zero;
            rightHand.transform.localRotation = Quaternion.identity;
            ConfigureOVRHand(rightHand, 1); // 1 = HandRight
            Undo.RegisterCreatedObjectUndo(rightHand, "Add Right OVRHandPrefab");

            Debug.Log($"{LOG} OVRHandPrefab added under LeftHandAnchor + RightHandAnchor.");
        }

        public static void ConfigureOVRHand(GameObject handGO, int handIndex)
        {
            var hand = handGO.GetComponent<OVRHand>();
            if (hand != null)
            {
                var so = new SerializedObject(hand);
                SetProperty(so, "HandType", handIndex, $"OVRHand.HandType ({handGO.name})");
                // m_showState: Always=0 so hands are always visible
                SetProperty(so, "m_showState", 0, $"OVRHand.m_showState=Always ({handGO.name})");
                so.ApplyModifiedProperties();
            }
            else Debug.LogError($"{LOG} OVRHand component NOT FOUND on {handGO.name}!");

            var skeleton = handGO.GetComponent<OVRSkeleton>();
            if (skeleton != null)
            {
                var so = new SerializedObject(skeleton);
                var prop = so.FindProperty("_skeletonType");
                if (prop != null)
                {
                    prop.intValue = handIndex;
                    so.ApplyModifiedProperties();
                }
                else Debug.LogError($"{LOG} OVRSkeleton._skeletonType property NOT FOUND!");
            }
            else Debug.LogError($"{LOG} OVRSkeleton NOT FOUND on {handGO.name}!");

            var mesh = handGO.GetComponent<OVRMesh>();
            if (mesh != null)
            {
                var so = new SerializedObject(mesh);
                var prop = so.FindProperty("_meshType");
                if (prop != null)
                {
                    prop.intValue = handIndex;
                    so.ApplyModifiedProperties();
                }
                else Debug.LogError($"{LOG} OVRMesh._meshType property NOT FOUND!");
            }
            else Debug.LogError($"{LOG} OVRMesh NOT FOUND on {handGO.name}!");
        }

        public static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static void SetProperty(SerializedObject so, string name, int value, string label)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
            {
                prop.intValue = value;
                Debug.Log($"{LOG} {label}: {name} = {value}");
            }
            else
                Debug.LogError($"{LOG} Property NOT FOUND: {name} -- SDK field name may have changed!");
        }

        static void SetProperty(SerializedObject so, string name, bool value, string label)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
            {
                prop.boolValue = value;
                Debug.Log($"{LOG} {label}: {name} = {value}");
            }
            else
                Debug.LogError($"{LOG} Property NOT FOUND: {name} -- SDK field name may have changed!");
        }

        static string GetManifestPath()
        {
            string p = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(p))
            {
                Debug.LogError($"{LOG} manifest.json not found!");
                return null;
            }
            return p;
        }
    }
}
#endif
