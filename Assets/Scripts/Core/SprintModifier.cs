// SprintModifier.cs
// CYBERNOMAD -- Left thumbstick press = sprint (3x movement speed).

using UnityEngine;

public class SprintModifier : MonoBehaviour
{
    public float sprintMultiplier = 3f;
    private OVRPlayerController _pc;
    private float _baseSpeed;
    private bool _sprinting;

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
            _baseSpeed = _pc.Acceleration;
    }

    void Update()
    {
        if (_pc == null) return;

        // Don't sprint when menus are open
        if (VRQualityMenu.MenuOpen || VRItemSpawner.MenuOpen) return;

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
}
