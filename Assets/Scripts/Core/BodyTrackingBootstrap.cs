// BodyTrackingBootstrap.cs
// CYBERNOMAD -- Wires up body tracking + IK on the OVR rig at runtime.
// Auto-creates BodyTrackingManager, PlayerBody, and SimpleIKController.
// Designed for Quest 3 full body tracking.
//
// Requirements:
//   - OVRCameraRig in scene
//   - HAS_META_XR define (set by Meta XR SDK)
//   - com.meta.xr.sdk.core v74+
//   - Optional: com.meta.xr.sdk.movement for CharacterRetargeter

using UnityEngine;

public class BodyTrackingBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_BodyTrackingBootstrap");
        go.AddComponent<BodyTrackingBootstrap>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        // Guard: skip body tracking setup entirely when running in Editor without
        // a connected headset. OVRSkeleton/OVRBody spam "Global joint set is invalid"
        // hundreds of times per frame when there is no active body tracking provider.
        if (!IsBodyTrackingAvailable())
        {
            Debug.Log("[PLAGA44] BodyTrackingBootstrap: body tracking not available " +
                      "(editor without headset or unsupported platform) -- skipping setup.");
            return;
        }

        SetupBodyTracking();
    }

    /// <summary>
    /// Returns true only when body tracking hardware is actually reachable.
    /// In Editor Play Mode without a Quest connected, returns false to prevent
    /// OVRSkeleton/OVRBody spam.
    /// </summary>
    static bool IsBodyTrackingAvailable()
    {
#if HAS_META_XR
        // First check: is the OVR runtime even present?
        try
        {
            if (!OVRPlugin.initialized)
            {
                Debug.Log("[PLAGA44] BodyTrackingBootstrap: OVRPlugin not initialized.");
                return false;
            }

            // Check if a headset is actually connected and body tracking is supported.
            if (!OVRPlugin.bodyTrackingSupported)
            {
                Debug.Log("[PLAGA44] BodyTrackingBootstrap: bodyTrackingSupported = false.");
                return false;
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.Log($"[PLAGA44] BodyTrackingBootstrap: OVRPlugin check failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    void SetupBodyTracking()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogWarning("[PLAGA44] BodyTrackingBootstrap: no OVRCameraRig found");
            return;
        }

        // 1. BodyTrackingManager -- manages OVRBody
        var btm = FindAnyObjectByType<Plaga44.BodyTracking.BodyTrackingManager>();
        if (btm == null)
        {
            var btmGo = new GameObject("BodyTrackingManager");
            btmGo.transform.SetParent(rig.transform, false);
            btm = btmGo.AddComponent<Plaga44.BodyTracking.BodyTrackingManager>();
            btm.showDebugSkeleton = true; // visible for testing
            Debug.Log("[PLAGA44] BodyTrackingBootstrap: created BodyTrackingManager");
        }

        // 2. PlayerBody -- joint data bridge
        var pb = FindAnyObjectByType<Plaga44.BodyTracking.PlayerBody>();
        if (pb == null)
        {
            pb = btm.gameObject.AddComponent<Plaga44.BodyTracking.PlayerBody>();
            pb.bodyTrackingManager = btm;
            Debug.Log("[PLAGA44] BodyTrackingBootstrap: created PlayerBody");
        }

        // 3. BodyCalibration -- height estimation
        var bc = FindAnyObjectByType<Plaga44.BodyTracking.BodyCalibration>();
        if (bc == null)
        {
            bc = btm.gameObject.AddComponent<Plaga44.BodyTracking.BodyCalibration>();
            Debug.Log("[PLAGA44] BodyTrackingBootstrap: created BodyCalibration");
        }

        // 4. IK Controller -- foot grounding
        // SimpleIKController needs bone references which come from the tracked skeleton.
        // We create it but bones get assigned once tracking starts.
        var ik = FindAnyObjectByType<Plaga44.IK.SimpleIKController>();
        if (ik == null)
        {
            ik = btm.gameObject.AddComponent<Plaga44.IK.SimpleIKController>();
            ik.ikWeight = 0.8f;
            ik.footTrackingSpeed = 10f;
            Debug.Log("[PLAGA44] BodyTrackingBootstrap: created SimpleIKController (bones assigned when tracking starts)");
        }

        // 5. Auto-wire IK bones when tracking becomes active
        btm.gameObject.AddComponent<IKBoneWirer>();

        Debug.Log("[PLAGA44] BodyTrackingBootstrap: full body tracking pipeline ready. " +
                  "Waiting for Quest body tracking to activate...");
    }
}

/// <summary>
/// Helper that wires IK bone references once OVRSkeleton provides them.
/// Polls until skeleton bones are available, then assigns to SimpleIKController.
/// </summary>
public class IKBoneWirer : MonoBehaviour
{
    private float _pollInterval = 0.5f;
    private float _timer;
    private bool _wired;

    void Update()
    {
        if (_wired) { Destroy(this); return; }

        _timer -= Time.deltaTime;
        if (_timer > 0) return;
        _timer = _pollInterval;

#if HAS_META_XR
        var skeleton = GetComponent<OVRSkeleton>();
        if (skeleton == null || skeleton.Bones == null || skeleton.Bones.Count == 0) return;

        var ik = GetComponent<Plaga44.IK.SimpleIKController>();
        if (ik == null) return;

        // Map OVRSkeleton bones to IK controller by name (SDK-version agnostic)
        foreach (var bone in skeleton.Bones)
        {
            if (bone == null || bone.Transform == null) continue;
            string name = bone.Id.ToString();

            if (name.Contains("LeftUpperLeg"))       ik.leftUpperLeg  = bone.Transform;
            else if (name.Contains("LeftLowerLeg"))  ik.leftLowerLeg  = bone.Transform;
            else if (name.Contains("LeftFoot"))      ik.leftFoot      = bone.Transform;
            else if (name.Contains("RightUpperLeg")) ik.rightUpperLeg = bone.Transform;
            else if (name.Contains("RightLowerLeg")) ik.rightLowerLeg = bone.Transform;
            else if (name.Contains("RightFoot"))     ik.rightFoot     = bone.Transform;
        }

        if (ik.leftUpperLeg != null && ik.rightUpperLeg != null)
        {
            _wired = true;
            Debug.Log($"[PLAGA44] IKBoneWirer: bones assigned! " +
                      $"L.Leg={ik.leftUpperLeg.name}, R.Leg={ik.rightUpperLeg.name}");
        }
#endif
    }
}
