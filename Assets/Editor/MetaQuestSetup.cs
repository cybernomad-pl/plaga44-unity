#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
            new[] { "com.meta.xr.sdk.interaction",  META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.audio",        META_SDK_VERSION },
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

        [MenuItem("CYBERNOMAD/Meta SDK Setup/3. Setup VR Scene", false, 3)]
        public static void SetupVRScene()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError($"{LOG} Build target is not Android. Run Step 2 first.");
                return;
            }

            foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "OVRPlayerController" || t.name == "OVRCameraRig")
                {
                    Debug.LogWarning($"{LOG} {t.name} already in scene. Skipping.");
                    return;
                }
            }

            foreach (var cam in GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.gameObject.name == "Main Camera")
                {
                    Undo.DestroyObjectImmediate(cam.gameObject);
                    break;
                }
            }

            AddScriptingDefine("HAS_META_XR");

            // --- OVRCameraRig ---
            var rigPrefab = FindPrefab("OVRCameraRig");
            if (rigPrefab == null) { Debug.LogError($"{LOG} OVRCameraRig not found."); return; }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(rig, "Add OVRCameraRig");

            // Enable hand tracking in project config
            var projectConfig = OVRProjectConfig.CachedProjectConfig;
            if (projectConfig != null)
            {
                projectConfig.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
                OVRProjectConfig.CommitProjectConfig(projectConfig);
                Debug.Log($"{LOG} Hand tracking: Controllers and Hands.");
            }

            // --- Hands (OVRHandPrefab on each anchor) ---
            AddHandPrefab(rig, true);
            AddHandPrefab(rig, false);

            // --- Controllers (fallback) ---
            AddControllerPrefabs(rig);

            // --- VRInputDebug ---
            Undo.AddComponent<VRInputDebug>(rig);

            // --- OVRPlayerController wrapper (locomotion) ---
            GameObject playerCtrl = new GameObject("OVRPlayerController");
            playerCtrl.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, 0f), Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(playerCtrl, "Add OVRPlayerController");

            var cc = Undo.AddComponent<CharacterController>(playerCtrl);
            cc.height = 2.0f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0f, 1.0f, 0f);
            cc.skinWidth = 0.02f;

            Undo.SetTransformParent(rig.transform, playerCtrl.transform, "Reparent rig");
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            Undo.AddComponent(playerCtrl, typeof(OVRPlayerController));

            // --- LocomotionEnvironment (Meta test room) ---
            var envPrefab = FindPrefab("LocomotionEnvironment");
            if (envPrefab != null)
            {
                var env = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
                env.transform.position = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(env, "Add LocomotionEnvironment");
            }

            Selection.activeGameObject = playerCtrl;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} VR Scene ready: locomotion + hands + controllers + debug + Meta test room.");
        }

        static void AddHandPrefab(GameObject rig, bool isLeft)
        {
            string anchorName = isLeft ? "LeftHandAnchor" : "RightHandAnchor";
            Transform anchor = FindChildRecursive(rig.transform, anchorName);
            if (anchor == null) return;

            var handPrefab = FindPrefab("OVRHandPrefab");
            if (handPrefab == null) return;

            var hand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, anchor);
            hand.name = isLeft ? "LeftHand" : "RightHand";
            hand.transform.localPosition = Vector3.zero;
            hand.transform.localRotation = Quaternion.identity;

            var ovrHand = hand.GetComponent<OVRHand>();
            if (ovrHand != null)
            {
                var field = typeof(OVRHand).GetField("HandType",
                    BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                    field.SetValue(ovrHand, isLeft ? OVRHand.Hand.HandLeft : OVRHand.Hand.HandRight);
            }

            var ovrSkeleton = hand.GetComponent<OVRSkeleton>();
            if (ovrSkeleton != null)
            {
                var field = typeof(OVRSkeleton).GetField("_skeletonType",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    field.SetValue(ovrSkeleton, isLeft
                        ? OVRSkeleton.SkeletonType.HandLeft
                        : OVRSkeleton.SkeletonType.HandRight);
            }

            Undo.RegisterCreatedObjectUndo(hand, $"Add {hand.name}");
            Debug.Log($"{LOG} {hand.name} added.");
        }

        static GameObject FindPrefab(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:prefab");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static void AddScriptingDefine(string define)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            if (defines.Contains(define)) return;
            defines = string.IsNullOrEmpty(defines) ? define : defines + ";" + define;
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, defines);
            Debug.Log($"{LOG} Added scripting define: {define}");
        }

        static void AddControllerPrefabs(GameObject rig)
        {
            string[] ctrlGuids = AssetDatabase.FindAssets("OVRControllerPrefab t:prefab");
            if (ctrlGuids.Length == 0)
            {
                Debug.LogWarning($"{LOG} OVRControllerPrefab not found.");
                return;
            }

            string ctrlPath = AssetDatabase.GUIDToAssetPath(ctrlGuids[0]);
            var ctrlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ctrlPath);
            if (ctrlPrefab == null)
            {
                Debug.LogWarning($"{LOG} Could not load OVRControllerPrefab.");
                return;
            }

            Transform leftAnchor = FindChildRecursive(rig.transform, "LeftControllerAnchor")
                                ?? FindChildRecursive(rig.transform, "LeftHandAnchor");
            Transform rightAnchor = FindChildRecursive(rig.transform, "RightControllerAnchor")
                                 ?? FindChildRecursive(rig.transform, "RightHandAnchor");

            if (leftAnchor != null)
            {
                var left = (GameObject)PrefabUtility.InstantiatePrefab(ctrlPrefab, leftAnchor);
                left.name = "LeftControllerModel";
                left.transform.localPosition = Vector3.zero;
                left.transform.localRotation = Quaternion.identity;
                SetControllerType(left, "LTouch");
                Undo.RegisterCreatedObjectUndo(left, "Add Left Controller");
                Debug.Log($"{LOG} Left controller added.");
            }

            if (rightAnchor != null)
            {
                var right = (GameObject)PrefabUtility.InstantiatePrefab(ctrlPrefab, rightAnchor);
                right.name = "RightControllerModel";
                right.transform.localPosition = Vector3.zero;
                right.transform.localRotation = Quaternion.identity;
                SetControllerType(right, "RTouch");
                Undo.RegisterCreatedObjectUndo(right, "Add Right Controller");
                Debug.Log($"{LOG} Right controller added.");
            }
        }

        static void SetControllerType(GameObject ctrlObj, string controllerName)
        {
            var components = ctrlObj.GetComponents<MonoBehaviour>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var compType = comp.GetType();
                if (!compType.Name.Contains("Controller")) continue;

                var field = compType.GetField("m_controller",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                    field = compType.GetField("m_controllerType",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null && field.FieldType.IsEnum)
                {
                    try
                    {
                        var enumVal = System.Enum.Parse(field.FieldType, controllerName);
                        field.SetValue(comp, enumVal);
                        Debug.Log($"{LOG} Set controller type: {controllerName}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"{LOG} Could not set controller type: {e.Message}");
                    }
                    return;
                }

                try
                {
                    var so = new SerializedObject(comp);
                    var prop = so.FindProperty("m_controller") ?? so.FindProperty("m_controllerType");
                    if (prop != null && prop.propertyType == SerializedPropertyType.Enum)
                    {
                        prop.enumValueIndex = controllerName == "LTouch" ? 1 : 2;
                        so.ApplyModifiedProperties();
                        Debug.Log($"{LOG} Set controller type: {controllerName} (serialized)");
                        return;
                    }
                }
                catch { }
            }
        }

        static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
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
