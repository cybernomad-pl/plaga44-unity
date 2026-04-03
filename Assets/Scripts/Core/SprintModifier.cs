// SprintModifier.cs
// CYBERNOMAD -- Left thumbstick press = sprint (3x movement speed).
// B button = JUMP (only when hands are empty).

using UnityEngine;

public class SprintModifier : MonoBehaviour
{
    [Header("Sprint")]
    public float sprintMultiplier = 3f;

    [Header("Jump")]
    public float jumpForce = 5f;
    public float jumpCooldown = 0.5f;

    private OVRPlayerController _pc;
    private CharacterController _cc;
    private float _baseSpeed;
    private bool _sprinting;

    // Jump state
    private float _verticalVelocity;
    private Vector3 _jumpHorizontalVelocity;
    private float _jumpTimer;
    private const float Gravity = 9.81f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_SprintModifier");
        go.AddComponent<SprintModifier>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        _pc = FindAnyObjectByType<OVRPlayerController>();
        if (_pc != null)
        {
            _baseSpeed = _pc.Acceleration;
            _cc = _pc.GetComponent<CharacterController>();
        }
    }

    void Update()
    {
        if (_pc == null) return;

        // Don't sprint/jump when menus are open
        if (VRQualityMenu.MenuOpen || VRItemSpawner.MenuOpen) return;

        HandleSprint();
        HandleJump();
    }

    void HandleSprint()
    {
        bool pressed = OVRInput.Get(OVRInput.Button.PrimaryThumbstick); // L3

        if (pressed && !_sprinting)
        {
            _pc.Acceleration = _baseSpeed * sprintMultiplier;
            _sprinting = true;
        }
        else if (!pressed && _sprinting)
        {
            _pc.Acceleration = _baseSpeed;
            _sprinting = false;
        }
    }

    void HandleJump()
    {
        _jumpTimer -= Time.deltaTime;

        // B button (right controller) = jump, only when hands empty
        bool bPressed = OVRInput.GetDown(OVRInput.Button.Two); // B button

        if (bPressed && _jumpTimer <= 0f && HandsAreEmpty())
        {
            _verticalVelocity = jumpForce;
            // Capture forward momentum at jump start
            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
            Transform head = _pc.transform;
            Vector3 forward = head.forward;
            Vector3 right = head.right;
            forward.y = 0f; forward.Normalize();
            right.y = 0f; right.Normalize();
            float speed = _sprinting ? _baseSpeed * sprintMultiplier : _baseSpeed;
            _jumpHorizontalVelocity = (forward * stick.y + right * stick.x) * speed * 0.4f;
            _jumpTimer = jumpCooldown;
            Debug.Log("[PLAGA44] JUMP");
        }

        // Apply gravity + vertical movement
        bool grounded = _cc != null && _cc.isGrounded;

        if (grounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -0.5f; // small downward to keep grounded
            _jumpHorizontalVelocity = Vector3.zero; // stop air movement on land
        }
        else
        {
            // Use scene gravity if non-zero, otherwise default
            float g = Physics.gravity.magnitude > 0.01f ? Physics.gravity.magnitude : Gravity;
            _verticalVelocity -= g * Time.deltaTime;
        }

        Vector3 move = Vector3.up * _verticalVelocity * Time.deltaTime + _jumpHorizontalVelocity * Time.deltaTime;

        if (_cc != null)
        {
            _cc.Move(move);
        }
        else
        {
            _pc.transform.position += move;
        }
    }

    bool HandsAreEmpty()
    {
        // Sprawdz czy ktorykolwiek OVRGrabber trzyma cos
        var grabbers = FindObjectsByType<OVRGrabber>(FindObjectsSortMode.None);
        foreach (var g in grabbers)
        {
            if (g.grabbedObject != null) return false;
        }
        return true;
    }
}
