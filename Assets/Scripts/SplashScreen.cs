// SplashScreen.cs
// CYBERNOMAD -- Black screen in front of face with PLAGA '44 title.
// Stays until both index triggers are pressed simultaneously.
// Hides controller/hand models while active.
// Follows CenterEyeAnchor at fixed distance.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SplashScreen : MonoBehaviour
{
    public float fadeDuration = 1.0f;
    [Tooltip("Use <color=#CC3333> for red parts. Leave empty for Application.productName.")]
    public string displayName = "PLAGA <color=#CC3333>'44</color>";

    private Canvas _canvas;
    private Text _title;
    private Transform _centerEye;
    private bool _fading;
    private float _fadeTimer;
    private CanvasGroup _group;
    private List<Renderer> _hiddenRenderers = new List<Renderer>();

    // Distance from eyes -- far enough to be comfortable in VR
    private float displayDistance = 1.5f;
    private float displayScale = 0.001f;

    void Start()
    {
        CreateWorldCanvas();
    }

    void Update()
    {
        if (_centerEye == null)
        {
            FindCenterEye();
            if (_centerEye == null) return;
        }

        // Follow head
        _canvas.transform.position = _centerEye.position + _centerEye.forward * displayDistance;
        _canvas.transform.rotation = _centerEye.rotation;

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

        // Both index triggers
        if (BothTriggersPressed())
        {
            _fading = true;
            _fadeTimer = 0f;
        }
    }

    void OnDestroy()
    {
        ShowControllers();
    }

    private bool BothTriggersPressed()
    {
        float left = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        float right = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        return left > 0.5f && right > 0.5f;
    }

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
                    if (!_hiddenRenderers.Contains(r))
                        _hiddenRenderers.Add(r);
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
        if (rig != null)
            _centerEye = rig.centerEyeAnchor;
    }

    private void CreateWorldCanvas()
    {
        var canvasGO = new GameObject("SplashCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 9999;

        var rect = _canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(4000, 4000);
        rect.localScale = Vector3.one * displayScale;

        _group = canvasGO.AddComponent<CanvasGroup>();

        // Black background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = Color.black;
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

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
        _title = titleGO.AddComponent<Text>();
        _title.text = string.IsNullOrEmpty(displayName) ? Application.productName : displayName;
        _title.font = Font.CreateDynamicFontFromOSFont("Consolas", 52);
        _title.fontSize = 52;
        _title.color = Color.white;
        _title.alignment = TextAnchor.MiddleCenter;
        _title.supportRichText = true;
        _title.horizontalOverflow = HorizontalWrapMode.Overflow;
        _title.verticalOverflow = VerticalWrapMode.Overflow;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(2000, 200);

        // Subtitle
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
}
