using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Black screen with "PLAGA '44 (testbed)" title.
/// Fades out when any controller button is pressed.
/// Add to scene via Step 3 or manually.
/// </summary>
public class SplashScreen : MonoBehaviour
{
    public float fadeDuration = 1.0f;
    public string titleText = "PLAGA '44\n<size=24>(testbed)</size>";

    private Canvas _canvas;
    private Image _bg;
    private Text _title;
    private bool _fading;
    private float _fadeTimer;

    void Start()
    {
        CreateUI();
    }

    void Update()
    {
        if (_fading)
        {
            _fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
            float alpha = 1f - t;
            _bg.color = new Color(0f, 0f, 0f, alpha);
            _title.color = new Color(1f, 1f, 1f, alpha);

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
            return;
        }

        // Wait for any input
        if (AnyInputPressed())
        {
            _fading = true;
            _fadeTimer = 0f;
        }
    }

    private bool AnyInputPressed()
    {
#if HAS_META_XR
        if (OVRInput.Get(OVRInput.Button.Any)) return true;
        // Also check thumbstick movement
        Vector2 lStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        Vector2 rStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        if (lStick.magnitude > 0.5f || rStick.magnitude > 0.5f) return true;
        // Triggers
        if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > 0.5f) return true;
        if (OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) > 0.5f) return true;
#endif
        return false;
    }

    private void CreateUI()
    {
        // World-space canvas that covers the entire view
        var canvasGO = new GameObject("SplashCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;

        canvasGO.AddComponent<CanvasScaler>();

        // Black background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        _bg = bgGO.AddComponent<Image>();
        _bg.color = Color.black;
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Title text
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        _title = titleGO.AddComponent<Text>();
        _title.text = titleText;
        _title.font = Font.CreateDynamicFontFromOSFont("Consolas", 48);
        _title.fontSize = 48;
        _title.color = Color.white;
        _title.alignment = TextAnchor.MiddleCenter;
        _title.supportRichText = true;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.sizeDelta = Vector2.zero;
    }
}
