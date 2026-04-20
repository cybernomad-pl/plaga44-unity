// =============================================================================
// PlayerAnimatorSetup.cs
// CYBERNOMAD -- Buduje programowo AnimatorController dla gracza PLAGA44.
// States: Idle, Locomotion (blend), Fly, Freefall, Landing.
// Parameters: Speed (f), StrafeX (f), ForwardZ (f), IsFlying (b), IsFreefall (b), Land (trigger).
//
// Zapisywany w Assets/PLAGA44/Resources/PLAGA44_PlayerAnimator.controller.
// Wywolywany przez Bootstrap jesli asset brak lub flaga force.
// =============================================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class PlayerAnimatorSetup
    {
        private const string LOG          = "[PLAGA44][AnimatorSetup]";
        private const string ControllerPath = "Assets/PLAGA44/Resources/PLAGA44_PlayerAnimator.controller";

        // Animation clip paths
        private const string IdlePath          = "Assets/Samples/Meta XR Movement SDK/83.0.0/Advanced Samples/ISDKLocomotion/Animations/Idle.fbx";
        private const string RunPath           = "Assets/Samples/Meta XR Movement SDK/83.0.0/Advanced Samples/ISDKLocomotion/Animations/Run.fbx";
        private const string RunBackwardPath   = "Assets/Samples/Meta XR Movement SDK/83.0.0/Advanced Samples/ISDKLocomotion/Animations/RunBackward.fbx";
        private const string RunLeftStrafe     = "Assets/Samples/Meta XR Movement SDK/83.0.0/Advanced Samples/ISDKLocomotion/Animations/RunLeftStrafe.fbx";
        private const string RunRightStrafe    = "Assets/Samples/Meta XR Movement SDK/83.0.0/Advanced Samples/ISDKLocomotion/Animations/RunRightStrafe.fbx";
        private const string FallingIdlePath   = "Assets/PLAGA44/Animations/Falling_Idle.fbx";
        private const string FallingToRollPath = "Assets/PLAGA44/Animations/Falling_ToRoll.fbx";

        public static bool EnsureController()
        {
            if (File.Exists(ControllerPath)) return true;
            return BuildController();
        }

        public static bool BuildController()
        {
            EnsureFolder("Assets/PLAGA44/Resources");

            var idle         = LoadClip(IdlePath,          "Idle");
            var run          = LoadClip(RunPath,           "Run");
            var runBack      = LoadClip(RunBackwardPath,   "RunBackward");
            var runLeft      = LoadClip(RunLeftStrafe,     "RunLeftStrafe");
            var runRight     = LoadClip(RunRightStrafe,    "RunRightStrafe");
            var fallingIdle  = LoadClip(FallingIdlePath,   "Falling_Idle");
            var fallingRoll  = LoadClip(FallingToRollPath, "Falling_ToRoll");

            if (idle == null || run == null || fallingIdle == null)
            {
                Debug.LogError($"{LOG} Missing required clips -- abort");
                return false;
            }

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Parameters
            ctrl.AddParameter("Speed",      AnimatorControllerParameterType.Float);
            ctrl.AddParameter("StrafeX",    AnimatorControllerParameterType.Float);
            ctrl.AddParameter("ForwardZ",   AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsFlying",   AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsFreefall", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Land",       AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            sm.entryPosition = new Vector3(50, 100);
            sm.anyStatePosition = new Vector3(50, 300);
            sm.exitPosition = new Vector3(800, 100);

            // State: Idle
            var idleState = sm.AddState("Idle", new Vector3(300, 100));
            idleState.motion = idle;

            // State: Locomotion (2D blend tree: ForwardZ + StrafeX)
            var locomotionBlend = new BlendTree { name = "LocomotionBlend", blendType = BlendTreeType.SimpleDirectional2D, blendParameter = "StrafeX", blendParameterY = "ForwardZ" };
            locomotionBlend.AddChild(idle,      new Vector2( 0f,  0f));
            locomotionBlend.AddChild(run,       new Vector2( 0f,  1f));
            locomotionBlend.AddChild(runBack,   new Vector2( 0f, -1f));
            locomotionBlend.AddChild(runLeft,   new Vector2(-1f,  0f));
            locomotionBlend.AddChild(runRight,  new Vector2( 1f,  0f));
            AssetDatabase.AddObjectToAsset(locomotionBlend, ctrl);
            var locomotionState = sm.AddState("Locomotion", new Vector3(300, 200));
            locomotionState.motion = locomotionBlend;

            // State: Fly (falling idle pose w locie)
            var flyState = sm.AddState("Fly", new Vector3(300, 300));
            flyState.motion = fallingIdle;

            // State: Freefall (pure fall pose, full body override via retargeter weights)
            var freefallState = sm.AddState("Freefall", new Vector3(300, 400));
            freefallState.motion = fallingIdle;

            // State: Landing (one-shot)
            var landingState = sm.AddState("Landing", new Vector3(600, 400));
            landingState.motion = fallingRoll != null ? fallingRoll : (Motion)idle;

            // Default = Freefall (bo start gry = StratoJump). Animator.Play("Idle") za Start().
            sm.defaultState = freefallState;

            // Transitions
            AddTransition(idleState,       locomotionState, "Speed",      AnimatorConditionMode.Greater, 0.1f);
            AddTransition(locomotionState, idleState,       "Speed",      AnimatorConditionMode.Less,    0.05f);

            // Any -> Fly (priority)
            var anyToFly = sm.AddAnyStateTransition(flyState);
            anyToFly.AddCondition(AnimatorConditionMode.If, 0, "IsFlying");
            anyToFly.duration = 0.15f;
            anyToFly.canTransitionToSelf = false;

            AddTransition(flyState, idleState, "IsFlying", AnimatorConditionMode.IfNot, 0);

            // Any -> Freefall (priority)
            var anyToFreefall = sm.AddAnyStateTransition(freefallState);
            anyToFreefall.AddCondition(AnimatorConditionMode.If, 0, "IsFreefall");
            anyToFreefall.duration = 0.1f;
            anyToFreefall.canTransitionToSelf = false;

            // Freefall -> Landing (Land trigger)
            var freefallToLanding = freefallState.AddTransition(landingState);
            freefallToLanding.AddCondition(AnimatorConditionMode.If, 0, "Land");
            freefallToLanding.hasExitTime = false;
            freefallToLanding.duration = 0.15f;

            // Landing -> Idle (exit time)
            var landingToIdle = landingState.AddTransition(idleState);
            landingToIdle.hasExitTime = true;
            landingToIdle.exitTime = 0.95f;
            landingToIdle.duration = 0.1f;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LOG} Built controller at {ControllerPath}");
            return true;
        }

        private static AnimationClip LoadClip(string path, string logName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                // FBX importer may have clip as sub-asset with different name
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var a in allAssets)
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { clip = c; break; }
            }
            if (clip == null) Debug.LogWarning($"{LOG} Clip not found: {path} ({logName})");
            return clip;
        }

        private static void AddTransition(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode, float threshold)
        {
            var t = from.AddTransition(to);
            t.AddCondition(mode, threshold, param);
            t.hasExitTime = false;
            t.duration = 0.1f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
