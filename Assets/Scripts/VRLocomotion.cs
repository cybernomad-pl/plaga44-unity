using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class VRLocomotion : MonoBehaviour
{
    public float speed = 2.0f;
    public float snapTurnAngle = 45f;
    public float snapTurnCooldown = 0.3f;

    private CharacterController _cc;
    private Transform _head;
    private float _fallSpeed;
    private float _snapTimer;
    private OVRInput.Controller _moveHand = OVRInput.Controller.LTouch;
    private OVRInput.Controller _turnHand = OVRInput.Controller.RTouch;

    void Start()
    {
        _cc = GetComponent<CharacterController>();
        var centerEye = transform.Find("TrackingSpace/CenterEyeAnchor");
        _head = centerEye != null ? centerEye : Camera.main?.transform;
    }

    void Update()
    {
        if (_cc == null || _head == null) return;

        Vector2 moveInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _moveHand);
        Vector2 turnInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _turnHand);

        // Move in head direction
        Vector3 forward = _head.forward;
        Vector3 right = _head.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * moveInput.y + right * moveInput.x) * speed;

        // Gravity
        if (_cc.isGrounded)
            _fallSpeed = -0.1f;
        else
            _fallSpeed += Physics.gravity.y * Time.deltaTime;

        move.y = _fallSpeed;

        _cc.Move(move * Time.deltaTime);

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
