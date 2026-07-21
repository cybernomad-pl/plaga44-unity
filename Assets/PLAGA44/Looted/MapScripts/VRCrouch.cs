using UnityEngine;
using UnityEngine.InputSystem;

public class VRCrouch : MonoBehaviour
{
    public float crouchOffset = 0.35f;
    public float speed = 12f;

    private bool _crouching;
    private Transform _trackingSpace;
    private InputAction _crouchAction;

    // DISABLED: Button-based crouch removed from A button.
    // Crouch is now physical only (headset Y position via CrouchDetector.cs).
    // To re-enable, uncomment AutoAttach and rebuild.
    //
    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    // static void AutoAttach()
    // {
    //     var pc = Object.FindAnyObjectByType<OVRPlayerController>();
    //     if (pc == null) { Debug.LogWarning("[CROUCH] No OVRPlayerController found."); return; }
    //     if (pc.GetComponent<VRCrouch>() == null) pc.gameObject.AddComponent<VRCrouch>();
    // }

    void OnEnable()
    {
        _crouchAction = new InputAction("Crouch", InputActionType.Button);
        // Only A button (right controller) -- X is used by Item Spawner
        _crouchAction.AddBinding("<XRController>{RightHand}/primaryButton");
        _crouchAction.AddBinding("<Keyboard>/c");
        _crouchAction.performed += _ => {
            _crouching = !_crouching;
            Debug.Log($"[CROUCH] {(_crouching ? "DOWN" : "UP")}");
        };
        _crouchAction.Enable();

        // Find TrackingSpace -- child of OVRCameraRig
        var rig = GetComponentInChildren<OVRCameraRig>();
        if (rig != null)
        {
            // TrackingSpace is the first child of OVRCameraRig
            _trackingSpace = rig.transform.Find("TrackingSpace");
            if (_trackingSpace == null && rig.transform.childCount > 0)
                _trackingSpace = rig.transform.GetChild(0);
        }

        if (_trackingSpace != null)
            Debug.Log($"[CROUCH] Ready. TrackingSpace = {_trackingSpace.name}");
        else
            Debug.LogError("[CROUCH] TrackingSpace NOT FOUND -- crouch won't work.");
    }

    void OnDisable()
    {
        if (_crouchAction != null)
        {
            _crouchAction.Disable();
            _crouchAction.Dispose();
            _crouchAction = null;
        }
    }

    // LateUpdate -- AFTER OVRCameraRig.Update() sets head tracking positions
    void LateUpdate()
    {
        if (_trackingSpace == null) return;

        float target = _crouching ? -crouchOffset : 0f;
        var pos = _trackingSpace.localPosition;
        pos.y = Mathf.Lerp(pos.y, target, Time.deltaTime * speed);
        _trackingSpace.localPosition = pos;
    }
}
