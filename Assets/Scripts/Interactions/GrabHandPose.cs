// GrabHandPose.cs
// CYBERNOMAD -- Custom hand pose when grabbing items.
// Index: extended (pointing forward)
// Middle, Ring, Pinky: curled (gripping)
// Thumb: clenched inward (Force Choke / Vader grip)
//
// Drives the OVR hand Animator parameters (Flex, Point, Pinch) which is the
// correct way to control finger poses on Meta Quest controllers.
// Does NOT directly manipulate bone transforms -- that fights the Animator
// and causes flickering/glitchy finger animation.
//
// Guards against hand tracking mode (no controllers connected) to prevent
// unnecessary Animator writes and log spam.

using UnityEngine;
using Plaga44.Core;

public class GrabHandPose : MonoBehaviour
{
    // Animator parameter targets for the Force Choke / Vader grip:
    //   Flex = overall grip curl (0 = open, 1 = fist)
    //   Point = index finger extension (0 = curled with fist, 1 = pointing)
    //   Pinch = index+thumb pinch (0 = no pinch, 1 = full pinch)
    [Header("Grab Pose Parameters")]
    public float grabFlex  = 0.9f;   // grip fingers curled
    public float grabPoint = 1.0f;   // index extended
    public float grabPinch = 0.0f;   // no pinch

    [Header("Thresholds")]
    [Tooltip("Grip trigger value to START the grab pose")]
    public float gripOnThreshold  = 0.7f;
    [Tooltip("Grip trigger value to END the grab pose (hysteresis)")]
    public float gripOffThreshold = 0.4f;
    [Tooltip("How fast the Animator parameters blend (higher = snappier)")]
    public float blendSpeed = 12f;

    private OVRCameraRig _rig;
    private bool _leftGrabbing;
    private bool _rightGrabbing;

    // Current blended Animator values (for smooth transitions)
    private float _leftFlex, _leftPoint, _leftPinch;
    private float _rightFlex, _rightPoint, _rightPinch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
#if LOCOMOTION_ONLY
        return;
#endif
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

        // Skip entirely if no controllers connected (hand tracking mode)
        if (!ControllerModeHelper.AnyControllerActive())
            return;

        // Update grab state with hysteresis to prevent flickering
        UpdateGrabState(OVRInput.Controller.LTouch, ref _leftGrabbing);
        UpdateGrabState(OVRInput.Controller.RTouch, ref _rightGrabbing);

        // Drive Animator parameters on each hand
        BlendAndApply(_rig.leftControllerAnchor, _leftGrabbing,
                      ref _leftFlex, ref _leftPoint, ref _leftPinch);
        BlendAndApply(_rig.rightControllerAnchor, _rightGrabbing,
                      ref _rightFlex, ref _rightPoint, ref _rightPinch);
    }

    void UpdateGrabState(OVRInput.Controller ctrl, ref bool grabbing)
    {
        float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl);

        // Hysteresis: require a higher threshold to START grab,
        // and a lower threshold to STOP. This prevents flickering
        // when the trigger hovers near the boundary.
        if (grabbing)
        {
            if (grip < gripOffThreshold)
                grabbing = false;
        }
        else
        {
            if (grip > gripOnThreshold)
                grabbing = true;
        }
    }

    void BlendAndApply(Transform anchor, bool grabbing,
                       ref float curFlex, ref float curPoint, ref float curPinch)
    {
        if (anchor == null) return;

        var animator = anchor.GetComponentInChildren<Animator>();
        if (animator == null) return;

        // Target values: grab pose or default (let controller capacitive sensing drive)
        float targetFlex  = grabbing ? grabFlex  : 0f;
        float targetPoint = grabbing ? grabPoint : 0f;
        float targetPinch = grabbing ? grabPinch : 0f;

        float dt = Time.deltaTime * blendSpeed;
        curFlex  = Mathf.Lerp(curFlex,  targetFlex,  dt);
        curPoint = Mathf.Lerp(curPoint, targetPoint, dt);
        curPinch = Mathf.Lerp(curPinch, targetPinch, dt);

        // Only override Animator when we have a meaningful override.
        // When not grabbing, values blend back to 0 and the controller's
        // capacitive touch sensing takes over naturally.
        animator.SetFloat("Flex",  curFlex);
        animator.SetFloat("Point", curPoint);
        animator.SetFloat("Pinch", curPinch);
    }
}
