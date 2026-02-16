// SplashScreen.cs
// CYBERNOMAD -- Black plane in front of face with PLAGA '44 title.
// Stays until both index triggers are pressed simultaneously.
// Uses world-space canvas following CenterEyeAnchor (same as VRInputDebug).

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

    // World-space params (similar to VRInputDebug)
    private float displayDistance = 0.35f;
    private float displayScale = 0.0005f;

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

        if (_fading)
        {
            _fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
            _group.alpha = 1f - t;

            if (t >= 1f)
            {
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
        // World-space canvas -- big black plane close to face
        var canvasGO = new GameObject("SplashCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 9999;

        var rect = _canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(3000, 3000);
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

        // Title text -- centered
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        _title = titleGO.AddComponent<Text>();
        _title.text = "PLAGA '44\n<size=36>(testbed)</size>";
        _title.font = Font.CreateDynamicFontFromOSFont("Consolas", 72);
        _title.fontSize = 72;
        _title.color = Color.white;
        _title.alignment = TextAnchor.MiddleCenter;
        _title.supportRichText = true;
        _title.horizontalOverflow = HorizontalWrapMode.Overflow;
        _title.verticalOverflow = VerticalWrapMode.Overflow;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(2000, 600);

        // Outline for readability (matching VRInputDebug style)
        var outline = titleGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        outline.effectDistance = new Vector2(2, -2);
    }
}
