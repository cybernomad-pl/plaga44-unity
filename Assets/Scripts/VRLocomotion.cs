using UnityEngine;

/// <summary>
/// Thumbstick locomotion for OVRCameraRig. No gravity -- flat movement only.
/// Left stick = move in head direction, right stick = snap turn.
/// </summary>
public class VRLocomotion : MonoBehaviour
{
    public float speed = 2.0f;
    public float snapTurnAngle = 45f;
    public float snapTurnCooldown = 0.3f;

    private Transform _head;
    private float _snapTimer;
    private OVRInput.Controller _moveHand = OVRInput.Controller.LTouch;
    private OVRInput.Controller _turnHand = OVRInput.Controller.RTouch;

    void Start()
    {
        var centerEye = transform.Find("TrackingSpace/CenterEyeAnchor");
        _head = centerEye;
    }

    void Update()
    {
        if (_head == null) return;
        if (Plaga44.UI.VRMenuManager.MenuOpen || VRQualityMenu.MenuOpen) return;

        Vector2 moveInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _moveHand);
        Vector2 turnInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _turnHand);

        // Move in head direction, flat (no Y)
        Vector3 forward = _head.forward;
        Vector3 right = _head.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * moveInput.y + right * moveInput.x) * speed * Time.deltaTime;
        move.y = 0f;

        transform.position += move;

        // Snap turn
        _snapTimer -= Time.deltaTime;
        if (Mathf.Abs(turnInput.x) > 0.6f && _snapTimer <= 0f)
        {
            float angle = Mathf.Sign(turnInput.x) * snapTurnAngle;
            transform.Rotate(0f, angle, 0f);
            _snapTimer = snapTurnCooldown;
        }
    }
}
