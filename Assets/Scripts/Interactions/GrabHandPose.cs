// GrabHandPose.cs
// CYBERNOMAD -- Custom hand pose when grabbing items.
// Index: extended (pointing forward)
// Middle, Ring, Pinky: curled (gripping)
// Thumb: clenched inward (Force Choke / Vader grip)
//
// Works with OVRHand / OVRCustomSkeleton or falls back to
// hiding controller model and showing pose via finger bone transforms.

using UnityEngine;

public class GrabHandPose : MonoBehaviour
{
    // Finger curl values: 0 = extended, 1 = fully curled
    public static float indexCurl = 0.0f;    // extended
    public static float middleCurl = 0.9f;   // curled
    public static float ringCurl = 0.95f;    // curled tight
    public static float pinkyCurl = 1.0f;    // curled tightest
    public static float thumbCurl = 0.7f;    // clenched inward

    private OVRCameraRig _rig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_GrabHandPose");
        go.AddComponent<GrabHandPose>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        _rig = FindAnyObjectByType<OVRCameraRig>();
    }

    void LateUpdate()
    {
        if (_rig == null) return;

        // Check if either hand is grabbing
        bool leftGrab = IsGrabbing(OVRInput.Controller.LTouch);
        bool rightGrab = IsGrabbing(OVRInput.Controller.RTouch);

        if (leftGrab)
            ApplyPose(_rig.leftHandAnchor, true);
        if (rightGrab)
            ApplyPose(_rig.rightHandAnchor, false);

        // Set OVRInput overrides for hand animation layers
        // OVRHand uses Anim Layer Blend to control finger curls
        SetFingerOverrides(leftGrab, rightGrab);
    }

    bool IsGrabbing(OVRInput.Controller ctrl)
    {
        // Grabbing = grip trigger held
        return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl) > 0.7f ||
               OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, ctrl) > 0.7f;
    }

    void ApplyPose(Transform handAnchor, bool isLeft)
    {
        if (handAnchor == null) return;

        // Try to find OVRSkeleton on hand
        var skeleton = handAnchor.GetComponentInChildren<OVRSkeleton>();
        if (skeleton == null || skeleton.Bones == null || skeleton.Bones.Count == 0) return;

        foreach (var bone in skeleton.Bones)
        {
            if (bone == null || bone.Transform == null) continue;
            string boneName = bone.Id.ToString().ToLower();

            float targetCurl = 0f;
            bool apply = false;

            // Index finger -- EXTENDED
            if (boneName.Contains("index"))
            {
                targetCurl = indexCurl;
                apply = true;
            }
            // Middle -- CURLED
            else if (boneName.Contains("middle"))
            {
                targetCurl = middleCurl;
                apply = true;
            }
            // Ring -- CURLED
            else if (boneName.Contains("ring"))
            {
                targetCurl = ringCurl;
                apply = true;
            }
            // Pinky -- CURLED TIGHT
            else if (boneName.Contains("pinky") || boneName.Contains("little"))
            {
                targetCurl = pinkyCurl;
                apply = true;
            }
            // Thumb -- CLENCHED INWARD
            else if (boneName.Contains("thumb"))
            {
                targetCurl = thumbCurl;
                apply = true;
            }

            if (apply && (boneName.Contains("1") || boneName.Contains("2") || boneName.Contains("3")))
            {
                // Apply curl as rotation on X axis (typical for finger bones)
                float angle = targetCurl * 90f;
                Quaternion curled = Quaternion.Euler(angle, 0, 0);
                bone.Transform.localRotation = Quaternion.Slerp(
                    bone.Transform.localRotation, curled, Time.deltaTime * 15f);
            }
        }
    }

    void SetFingerOverrides(bool leftGrab, bool rightGrab)
    {
        // OVRInput custom capacitive touch overrides
        // This affects the default hand animation in OVRControllerHelper
        // When grabbing, we override the animator parameters

        // Find OVRControllerHelper on each hand
        if (_rig == null) return;

        if (leftGrab)
            ApplyAnimatorOverride(_rig.leftControllerAnchor);
        if (rightGrab)
            ApplyAnimatorOverride(_rig.rightControllerAnchor);
    }

    void ApplyAnimatorOverride(Transform anchor)
    {
        if (anchor == null) return;
        var animator = anchor.GetComponentInChildren<Animator>();
        if (animator == null) return;

        // OVR hand animator uses these parameters:
        // "Flex" (0-1) for overall grip
        // "Pinch" (0-1) for index-thumb pinch
        // "Point" (0-1) for index pointing

        // Force Choke pose: high flex (grip), no pinch, full point (index out)
        animator.SetFloat("Flex", 0.9f);
        animator.SetFloat("Point", 1.0f);   // index extended
        animator.SetFloat("Pinch", 0.0f);   // no pinch
    }
}
