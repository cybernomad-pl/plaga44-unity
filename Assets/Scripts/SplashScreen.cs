// SplashScreen.cs
// CYBERNOMAD -- Black plane in front of face with PLAGA '44 title.
// Stays until both index triggers are pressed simultaneously.
// Hides controller/hand models while active (input still works).
// Uses world-space canvas following CenterEyeAnchor (same as VRInputDebug).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SplashScreen : MonoBehaviour
{
    public float fadeDuration = 1.0f;

    private Canvas _canvas;
    private Image _bg;
    private Text _title;
    private Transform _centerEye;
    private bool _fading;
    private float _fadeTimer;
    private CanvasGroup _group;
    private List<Renderer> _hiddenRenderers = new List<Renderer>();

    // World-space params
    private float displayDistance = 0.55f;
    private float displayScale = 0.001f;

    void Start()
    {
        CreateWorldCanvas();
    }

    void Update()
    {
        // Find head
        if (_centerEye == null)
        {
            FindCenterEye();
            if (_centerEye == null) return;
        }

        // Follow head -- locked to face
        _canvas.transform.position = _centerEye.position + _centerEye.forward * displayDistance;
        _canvas.transform.rotation = _centerEye.rotation;

        // Hide controllers while splash is showing
        if (!_fading)
        {
            HideControllers();
        }

        if (_fading)
        {
            _fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
            _group.alpha = 1f - t;

            if (t >= 1f)
            {
                ShowControllers();
                Destroy(gameObject);
            }
            return;
        }

        // Wait for BOTH index triggers pressed at the same time
        if (BothTriggersPressed())
        {
            _fading = true;
            _fadeTimer = 0f;
        }
    }

    void OnDestroy()
    {
        // Safety -- always restore controllers
        ShowControllers();
    }

    private bool BothTriggersPressed()
    {
#if HAS_META_XR
        float left = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        float right = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        return left > 0.5f && right > 0.5f;
#else
        return false;
#endif
    }

    private void HideControllers()
    {
#if HAS_META_XR
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig == null) return;

        Transform[] anchors = new Transform[]
        {
            rig.leftControllerAnchor,
            rig.rightControllerAnchor,
            rig.leftHandAnchor,
            rig.rightHandAnchor
        };

        foreach (var anchor in anchors)
        {
            if (anchor == null) continue;
            foreach (var r in anchor.GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled)
                {
                    r.enabled = false;
                    if (!_hiddenRenderers.Contains(r))
                        _hiddenRenderers.Add(r);
                }
            }
        }
#endif
    }

    private void ShowControllers()
    {
        foreach (var r in _hiddenRenderers)
        {
            if (r != null) r.enabled = true;
        }
        _hiddenRenderers.Clear();
    }

    private void FindCenterEye()
    {
#if HAS_META_XR
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            _centerEye = rig.centerEyeAnchor;
            return;
        }
#endif
        var cam = Camera.main;
        if (cam != null) _centerEye = cam.transform;
    }

    private void CreateWorldCanvas()
    {
        // World-space canvas -- big black plane covering full FOV
        var canvasGO = new GameObject("SplashCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 9999;

        var rect = _canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(4000, 4000);
        rect.localScale = Vector3.one * displayScale;

        // CanvasGroup for clean fade
        _group = canvasGO.AddComponent<CanvasGroup>();

        // Black background -- fills the entire canvas
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        _bg = bgGO.AddComponent<Image>();
        _bg.color = Color.black;
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // "TESTBED:" label -- small, above project name
        var labelGO = new GameObject("TestbedLabel");
        labelGO.transform.SetParent(canvasGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = "TESTBED:";
        label.font = Font.CreateDynamicFontFromOSFont("Consolas", 28);
        label.fontSize = 28;
        label.color = new Color(0.6f, 0.6f, 0.6f);
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchoredPosition = new Vector2(0, 80);
        labelRect.sizeDelta = new Vector2(2000, 100);

        // Project name -- e.g. "PLAGA '44" with '44 in red
        // Uses Application.productName from PlayerSettings
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        _title = titleGO.AddComponent<Text>();
        string projectName = Application.productName;
        // Color the numeric suffix red (e.g. "'44" in "PLAGA '44")
        int apoIdx = projectName.IndexOf('\'');
        if (apoIdx >= 0)
        {
            string before = projectName.Substring(0, apoIdx);
            string after = projectName.Substring(apoIdx);
            _title.text = $"{before}<color=#CC3333>{after}</color>";
        }
        else
        {
            _title.text = projectName;
        }
        _title.font = Font.CreateDynamicFontFromOSFont("Consolas", 52);
        _title.fontSize = 52;
        _title.color = Color.white;
        _title.alignment = TextAnchor.MiddleCenter;
        _title.supportRichText = true;
        _title.horizontalOverflow = HorizontalWrapMode.Overflow;
        _title.verticalOverflow = VerticalWrapMode.Overflow;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, -10);
        titleRect.sizeDelta = new Vector2(2000, 200);
    }
}
