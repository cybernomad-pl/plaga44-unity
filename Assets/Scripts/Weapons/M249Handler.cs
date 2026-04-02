// M249Handler.cs
// CYBERNOMAD -- M249 SAW two-handed grip + bipod deployment.
//
// Right hand: pistol grip (primary grab)
// Left hand: carry handle or bottom rail (secondary grab point)
// Crouch: auto-deploy bipod, weapon rests on ground
//
// Attaches to M249 prefab at spawn time.

using UnityEngine;

public class M249Handler : MonoBehaviour
{
    [Header("Grip Points (local offsets from mesh center)")]
    [Tooltip("Right hand pistol grip position (local space)")]
    public Vector3 pistolGripOffset = new Vector3(0f, -0.05f, -0.15f);

    [Tooltip("Left hand carry handle / rail position (local space)")]
    public Vector3 leftGripOffset = new Vector3(0f, 0.08f, 0.25f);

    [Header("Bipod")]
    [Tooltip("Bipod deploy height above ground")]
    public float bipodHeight = 0.3f;

    [Tooltip("Bipod deploy forward offset from grip")]
    public float bipodForward = 0.4f;

    [Header("State")]
    public bool isBipodDeployed = false;
    public bool isTwoHanded = false;

    private OVRGrabbable _grabbable;
    private Rigidbody _rb;
    private Transform _leftGripTarget;
    private Transform _rightGripTarget;
    private OVRCameraRig _rig;
    private float _standingHeadHeight;
    private bool _calibrated;

    void Start()
    {
        _grabbable = GetComponent<OVRGrabbable>();
        _rb = GetComponent<Rigidbody>();
        _rig = FindAnyObjectByType<OVRCameraRig>();

        CreateGripPoints();

        // Calibrate standing height after 2 seconds
        Invoke(nameof(CalibrateHeight), 2f);
    }

    void CreateGripPoints()
    {
        // Right hand grip (pistol grip)
        var rightGo = new GameObject("_RightGrip");
        rightGo.transform.SetParent(transform, false);
        rightGo.transform.localPosition = pistolGripOffset;
        rightGo.transform.localRotation = Quaternion.Euler(90, 0, 0); // barrel forward
        _rightGripTarget = rightGo.transform;

        // Left hand grip (carry handle / foregrip)
        var leftGo = new GameObject("_LeftGrip");
        leftGo.transform.SetParent(transform, false);
        leftGo.transform.localPosition = leftGripOffset;
        _leftGripTarget = leftGo.transform;

        // Set snap offset to right grip
        SetSnapOffset(_rightGripTarget);
    }

    void SetSnapOffset(Transform offset)
    {
        if (_grabbable == null) return;
        var type = typeof(OVRGrabbable);

        var snapOrient = type.GetField("m_snapOrientation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (snapOrient != null) snapOrient.SetValue(_grabbable, true);

        var snapPos = type.GetField("m_snapPosition",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (snapPos != null) snapPos.SetValue(_grabbable, true);

        var snapOffsetField = type.GetField("m_snapOffset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (snapOffsetField != null) snapOffsetField.SetValue(_grabbable, offset);
    }

    void CalibrateHeight()
    {
        if (_rig != null && _rig.centerEyeAnchor != null)
        {
            _standingHeadHeight = _rig.centerEyeAnchor.position.y;
            _calibrated = true;
            Debug.Log($"[M249] Calibrated standing height: {_standingHeadHeight:F2}m");
        }
    }

    void Update()
    {
        if (_grabbable == null || !_grabbable.isGrabbed) return;

        UpdateTwoHandedGrip();
        UpdateBipod();
    }

    void UpdateTwoHandedGrip()
    {
        if (_rig == null) return;

        // Check if left hand is near the left grip point
        var leftHand = _rig.leftHandAnchor;
        if (leftHand == null) return;

        float distToLeftGrip = Vector3.Distance(leftHand.position, _leftGripTarget.position);
        bool leftGrabbing = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) > 0.5f;

        if (leftGrabbing && distToLeftGrip < 0.2f)
        {
            if (!isTwoHanded)
            {
                isTwoHanded = true;
                Debug.Log("[M249] Two-handed grip engaged");
            }

            // Stabilize weapon -- barrel points from right hand toward left hand
            Vector3 rightPos = _grabbable.grabbedBy.transform.position;
            Vector3 leftPos = leftHand.position;
            Vector3 barrelDir = (leftPos - rightPos).normalized;

            // Smoothly rotate weapon to align barrel with two-hand direction
            Quaternion targetRot = Quaternion.LookRotation(barrelDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);
        }
        else
        {
            if (isTwoHanded)
            {
                isTwoHanded = false;
                Debug.Log("[M249] Two-handed grip released");
            }
        }
    }

    void UpdateBipod()
    {
        if (!_calibrated || _rig == null) return;

        float headY = _rig.centerEyeAnchor.position.y;
        float crouchRatio = headY / _standingHeadHeight;
        bool isCrouching = crouchRatio < 0.7f; // head dropped 30%+

        if (isCrouching && !isBipodDeployed)
        {
            DeployBipod();
        }
        else if (!isCrouching && isBipodDeployed)
        {
            RetractBipod();
        }

        if (isBipodDeployed)
        {
            // Lock weapon position -- rests on ground via bipod
            Vector3 groundPos = transform.position;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f))
            {
                groundPos.y = hit.point.y + bipodHeight;
            }

            // Smoothly settle onto bipod
            Vector3 targetPos = new Vector3(transform.position.x, groundPos.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);

            // Keep barrel level when on bipod
            Vector3 fwd = transform.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude > 0.01f)
            {
                Quaternion levelRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, levelRot, Time.deltaTime * 3f);
            }

            // Reduce recoil / sway when bipod deployed
            if (_rb != null)
            {
                _rb.linearDamping = 5f;
                _rb.angularDamping = 5f;
            }
        }
    }

    void DeployBipod()
    {
        isBipodDeployed = true;
        Debug.Log("[M249] Bipod DEPLOYED (crouching)");
    }

    void RetractBipod()
    {
        isBipodDeployed = false;
        if (_rb != null)
        {
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0.05f;
        }
        Debug.Log("[M249] Bipod RETRACTED (standing)");
    }
}
