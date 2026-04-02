// GazeThrow.cs
// CYBERNOMAD -- Gaze-corrected throwing for VR.
// Problem: VR throwing using raw controller velocity is inaccurate and frustrating.
// Solution: Blend hand throw direction with head gaze direction based on how close
// the controller is to the player's line of sight at release.
//
// Three concentric zones (angle from gaze center):
//   INNER  (0-15°)  = strong gaze influence (70%) -- throw goes where you look
//   MIDDLE (15-30°)  = moderate influence (40%)
//   OUTER  (30°+)    = minimal influence (10%) -- mostly raw hand velocity
//
// Also applies velocity boost on release (replaces ThrowBoost).

using UnityEngine;

public class GazeThrow : MonoBehaviour
{
    [Header("Throw Boost")]
    [Tooltip("Velocity multiplier on release. 5 = throw 5x harder.")]
    public float boostMultiplier = 5.0f;

    [Header("Gaze Zones (degrees from gaze center)")]
    [Tooltip("Inner zone boundary angle.")]
    public float innerZoneDeg = 15f;
    [Tooltip("Middle zone boundary angle.")]
    public float middleZoneDeg = 30f;

    [Header("Gaze Influence per Zone (0=hand only, 1=gaze only)")]
    public float innerGazeWeight = 0.7f;
    public float middleGazeWeight = 0.4f;
    public float outerGazeWeight = 0.1f;

    private OVRGrabbable _grabbable;
    private Rigidbody _rb;
    private bool _wasGrabbed;
    private Transform _lastGrabberHand;

    void Start()
    {
        _grabbable = GetComponent<OVRGrabbable>();
        _rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (_grabbable == null || _rb == null) return;

        bool grabbed = _grabbable.isGrabbed;

        // Track which hand is grabbing
        if (grabbed && _grabbable.grabbedBy != null)
            _lastGrabberHand = _grabbable.grabbedBy.transform;

        // Just released -- apply gaze-corrected throw
        if (_wasGrabbed && !grabbed)
        {
            ApplyGazeThrow();
        }

        _wasGrabbed = grabbed;
    }

    private void ApplyGazeThrow()
    {
        // 1. Boost velocity first
        float rawSpeed = _rb.linearVelocity.magnitude;
        _rb.linearVelocity *= boostMultiplier;
        _rb.angularVelocity *= boostMultiplier;

        float speed = _rb.linearVelocity.magnitude;
        if (speed < 0.5f) return; // barely moving, skip gaze correction

        // 2. Find head
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig == null || rig.centerEyeAnchor == null) return;

        Transform head = rig.centerEyeAnchor;
        Vector3 gazeDir = head.forward;

        // 3. Compute angle between controller position and gaze axis
        float angle = 90f; // default: outer zone
        if (_lastGrabberHand != null)
        {
            Vector3 toController = (_lastGrabberHand.position - head.position).normalized;
            angle = Vector3.Angle(gazeDir, toController);
        }

        // 4. Zone -> gaze weight
        float gazeWeight;
        string zone;
        if (angle <= innerZoneDeg)
        {
            gazeWeight = innerGazeWeight;
            zone = "INNER";
        }
        else if (angle <= middleZoneDeg)
        {
            gazeWeight = middleGazeWeight;
            zone = "MIDDLE";
        }
        else
        {
            gazeWeight = outerGazeWeight;
            zone = "OUTER";
        }

        // 5. Blend throw direction with gaze direction
        Vector3 throwDir = _rb.linearVelocity.normalized;
        Vector3 blendedDir = Vector3.Slerp(throwDir, gazeDir, gazeWeight).normalized;
        _rb.linearVelocity = blendedDir * speed;

        Debug.Log($"[PLAGA44] GazeThrow: zone={zone} angle={angle:F1}deg " +
                  $"gaze={gazeWeight * 100f:F0}% speed={rawSpeed:F1}->{speed:F1} m/s");
    }

    /// <summary>
    /// Returns current gaze zone info for the given controller.
    /// Used by VRInputDebug to show active zone on HUD.
    /// </summary>
    public static GazeZoneInfo GetControllerZone(OVRCameraRig rig, OVRInput.Controller ctrl)
    {
        var info = new GazeZoneInfo();
        if (rig == null || rig.centerEyeAnchor == null)
            return info;

        Transform head = rig.centerEyeAnchor;
        Vector3 localPos = OVRInput.GetLocalControllerPosition(ctrl);
        Vector3 worldPos = rig.transform.TransformPoint(localPos);
        Vector3 toCtrl = (worldPos - head.position).normalized;
        info.angle = Vector3.Angle(head.forward, toCtrl);

        // Project controller onto head-local space for HUD dot position
        Vector3 headLocal = head.InverseTransformPoint(worldPos);
        info.hudX = Mathf.Atan2(headLocal.x, headLocal.z) * Mathf.Rad2Deg;
        info.hudY = Mathf.Atan2(headLocal.y, headLocal.z) * Mathf.Rad2Deg;

        if (info.angle <= 15f)
            info.zone = 0; // inner
        else if (info.angle <= 30f)
            info.zone = 1; // middle
        else
            info.zone = 2; // outer

        return info;
    }

    public struct GazeZoneInfo
    {
        public float angle;  // degrees from gaze center
        public float hudX;   // horizontal angle (degrees) for HUD projection
        public float hudY;   // vertical angle (degrees) for HUD projection
        public int zone;     // 0=inner, 1=middle, 2=outer
    }
}
