using UnityEngine;
using UnityEngine.XR;

public class SimpleLocomotion : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float turnSpeed = 45f;

    private InputDevice _leftController;
    private InputDevice _rightController;
    private Transform _head;

    void Start()
    {
        var head = transform.Find("TrackingSpace/CenterEyeAnchor");
        if (head == null) head = Camera.main?.transform;
        _head = head;
    }

    void Update()
    {
        if (!_leftController.isValid)
            _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!_rightController.isValid)
            _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        // Left thumbstick: move
        if (_leftController.isValid &&
            _leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 moveAxis))
        {
            if (moveAxis.sqrMagnitude > 0.01f && _head != null)
            {
                Vector3 forward = _head.forward;
                Vector3 right = _head.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                Vector3 move = (forward * moveAxis.y + right * moveAxis.x) * moveSpeed * Time.deltaTime;
                transform.position += move;
            }
        }

        // Right thumbstick X: snap turn
        if (_rightController.isValid &&
            _rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 turnAxis))
        {
            if (Mathf.Abs(turnAxis.x) > 0.5f)
            {
                float turn = Mathf.Sign(turnAxis.x) * turnSpeed * Time.deltaTime;
                transform.Rotate(0f, turn, 0f);
            }
        }
    }
}
