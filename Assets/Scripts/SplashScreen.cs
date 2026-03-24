// SplashScreen.cs
// CYBERNOMAD -- Black cube room around player with PLAGA '44 title on front wall.
// Player can look around but NOT move (locomotion disabled).
// Both triggers -> fade to black -> load game scene.
// Cube is static in world space (not attached to head).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashScreen : MonoBehaviour
{
    [Header("Display")]
    [Tooltip("Use <color=#CC3333> for red parts.")]
    public string displayName = "PLAGA <color=#CC3333>'44</color>";

    [Header("Transition")]
    public float fadeDuration = 1.5f;
    public string gameSceneName = ""; // If empty, stays in current scene

    [Header("Room")]
    public float roomSize = 4f; // Cube side length in meters

    private Transform _centerEye;
    private bool _triggered;
    private float _fadeTimer;
    private Material _wallMaterial;
    private Material _fadeMaterial;
    private GameObject _fadeQuad;
    private OVRPlayerController _playerController;
    private List<Renderer> _hiddenRenderers = new List<Renderer>();

    void Start()
    {
        DisableLocomotion();
        HideControllers();
        CreateRoom();
    }

    void Update()
    {
        if (_centerEye == null)
        {
            FindCenterEye();
            if (_centerEye == null) return;
        }

        // Keep hiding controllers
        if (!_triggered) HideControllers();

        if (_triggered)
        {
            _fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeTimer / fadeDuration);

            // Fade quad from transparent to black
            if (_fadeMaterial != null)
            {
                Color c = _fadeMaterial.color;
                c.a = t;
                _fadeMaterial.color = c;
            }

            if (t >= 1f)
            {
                EnableLocomotion();
                ShowControllers();

                if (!string.IsNullOrEmpty(gameSceneName))
                {
                    SceneManager.LoadScene(gameSceneName);
                }
                else
                {
                    // Same scene mode -- just remove splash
                    Destroy(gameObject);
                }
            }
            return;
        }

        // Wait for BOTH index triggers
        if (BothTriggersPressed())
        {
            _triggered = true;
            _fadeTimer = 0f;
            CreateFadeQuad();
        }
    }

    void OnDestroy()
    {
        EnableLocomotion();
        ShowControllers();
    }

    // ---- ROOM (black cube around player) ----

    private void CreateRoom()
    {
        FindCenterEye();
        Vector3 center = _centerEye != null
            ? new Vector3(_centerEye.position.x, _centerEye.position.y, _centerEye.position.z)
            : transform.position;
        // Slightly lower so floor is at feet
        center.y -= 0.5f;

        float half = roomSize / 2f;
        _wallMaterial = new Material(Shader.Find("Unlit/Color"));
        _wallMaterial.color = Color.black;

        // 6 walls: front (+Z), back (-Z), left (-X), right (+X), top (+Y), bottom (-Y)
        CreateWall("Floor",   center + Vector3.down * half,   new Vector3(roomSize, roomSize, 1), Quaternion.Euler(90, 0, 0));
        CreateWall("Ceiling", center + Vector3.up * half,     new Vector3(roomSize, roomSize, 1), Quaternion.Euler(-90, 0, 0));
        CreateWall("Left",    center + Vector3.left * half,   new Vector3(roomSize, roomSize, 1), Quaternion.Euler(0, 90, 0));
        CreateWall("Right",   center + Vector3.right * half,  new Vector3(roomSize, roomSize, 1), Quaternion.Euler(0, -90, 0));
        CreateWall("Back",    center + Vector3.back * half,   new Vector3(roomSize, roomSize, 1), Quaternion.identity);

        // Front wall -- has the title
        var frontWall = CreateWall("Front", center + Vector3.forward * half, new Vector3(roomSize, roomSize, 1), Quaternion.Euler(0, 180, 0));
        CreateTitleOnWall(frontWall.transform, center + Vector3.forward * (half - 0.01f));
    }

    private GameObject CreateWall(string name, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wall.name = $"SplashWall_{name}";
        wall.transform.SetParent(transform);
        wall.transform.position = pos;
        wall.transform.rotation = rot;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = _wallMaterial;

        // Remove collider -- don't block player
        var col = wall.GetComponent<Collider>();
        if (col != null) Destroy(col);

        return wall;
    }

    private void CreateTitleOnWall(Transform wall, Vector3 canvasPos)
    {
        // World-space canvas on front wall
        var canvasGO = new GameObject("SplashCanvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.position = canvasPos;
        // Face the player (player looks at +Z, canvas faces -Z)
        canvasGO.transform.rotation = Quaternion.Euler(0, 180, 0);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 9999;

        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2000, 2000);
        rect.localScale = Vector3.one * 0.001f; // 1 unit = 1000 canvas pixels = 1 meter

        // "TESTBED:" label
        var labelGO = new GameObject("TestbedLabel");
        labelGO.transform.SetParent(canvasGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = "TESTBED:";
        label.font = Font.CreateDynamicFontFromOSFont("Consolas", 14);
        label.fontSize = 14;
        label.color = new Color(0.5f, 0.5f, 0.5f);
        label.alignment = TextAnchor.LowerLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-124, 40);
        labelRect.sizeDelta = new Vector2(400, 30);

        // Project name
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var title = titleGO.AddComponent<Text>();
        title.text = string.IsNullOrEmpty(displayName) ? Application.productName : displayName;
        title.font = Font.CreateDynamicFontFromOSFont("Consolas", 52);
        title.fontSize = 52;
        title.color = Color.white;
        title.alignment = TextAnchor.MiddleCenter;
        title.supportRichText = true;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(2000, 200);

        // Subtitle -- "Press both triggers"
        var subGO = new GameObject("Subtitle");
        subGO.transform.SetParent(canvasGO.transform, false);
        var sub = subGO.AddComponent<Text>();
        sub.text = "press both triggers";
        sub.font = Font.CreateDynamicFontFromOSFont("Consolas", 16);
        sub.fontSize = 16;
        sub.color = new Color(0.4f, 0.4f, 0.4f);
        sub.alignment = TextAnchor.MiddleCenter;
        sub.horizontalOverflow = HorizontalWrapMode.Overflow;
        sub.verticalOverflow = VerticalWrapMode.Overflow;
        var subRect = subGO.GetComponent<RectTransform>();
        subRect.anchoredPosition = new Vector2(0, -60);
        subRect.sizeDelta = new Vector2(2000, 50);
    }

    // ---- FADE ----

    private void CreateFadeQuad()
    {
        // Full-screen fade quad attached to center eye
        _fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fadeQuad.name = "FadeQuad";
        _fadeQuad.transform.SetParent(_centerEye);
        _fadeQuad.transform.localPosition = new Vector3(0, 0, 0.3f);
        _fadeQuad.transform.localRotation = Quaternion.identity;
        _fadeQuad.transform.localScale = new Vector3(2f, 2f, 1f);

        var col = _fadeQuad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        _fadeMaterial = new Material(Shader.Find("Unlit/Color"));
        _fadeMaterial.color = new Color(0, 0, 0, 0);
        // Need transparent shader
        _fadeMaterial = new Material(Shader.Find("UI/Default"));
        _fadeMaterial.color = new Color(0, 0, 0, 0);
        _fadeQuad.GetComponent<Renderer>().material = _fadeMaterial;
    }

    // ---- LOCOMOTION CONTROL ----

    private void DisableLocomotion()
    {
        _playerController = FindFirstObjectByType<OVRPlayerController>();
        if (_playerController != null)
        {
            _playerController.EnableLinearMovement = false;
            _playerController.EnableRotation = false;
            Debug.Log("[PLAGA44] SplashScreen: locomotion DISABLED");
        }
    }

    private void EnableLocomotion()
    {
        if (_playerController != null)
        {
            _playerController.EnableLinearMovement = true;
            _playerController.EnableRotation = true;
            Debug.Log("[PLAGA44] SplashScreen: locomotion ENABLED");
        }
    }

    // ---- INPUT ----

    private bool BothTriggersPressed()
    {
        float left = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        float right = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        return left > 0.5f && right > 0.5f;
    }

    // ---- CONTROLLER VISIBILITY ----

    private void HideControllers()
    {
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig == null) return;

        Transform[] anchors = new Transform[]
        {
            rig.leftControllerAnchor, rig.rightControllerAnchor,
            rig.leftHandAnchor, rig.rightHandAnchor
        };

        foreach (var anchor in anchors)
        {
            if (anchor == null) continue;
            foreach (var r in anchor.GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled)
                {
                    r.enabled = false;
                    if (!_hiddenRenderers.Contains(r)) _hiddenRenderers.Add(r);
                }
            }
        }
    }

    private void ShowControllers()
    {
        foreach (var r in _hiddenRenderers)
            if (r != null) r.enabled = true;
        _hiddenRenderers.Clear();

        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig == null) return;
        Transform[] anchors = new Transform[]
        {
            rig.leftControllerAnchor, rig.rightControllerAnchor,
            rig.leftHandAnchor, rig.rightHandAnchor
        };
        foreach (var anchor in anchors)
        {
            if (anchor == null) continue;
            foreach (var r in anchor.GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
        }
    }

    private void FindCenterEye()
    {
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) _centerEye = rig.centerEyeAnchor;
    }
}
