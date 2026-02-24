// UIRayPointer.cs
// CYBERNOMAD -- Controller laser pointer for world-space UI.
// Attaches to a controller anchor (left or right).
// Renders a LineRenderer ray that hits world-space UI canvases.
// Uses manual plane intersection + RectTransform hit testing.
// Trigger button fires click/toggle events on hovered elements.
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Plaga44.UI
{
    [RequireComponent(typeof(LineRenderer))]
    public class UIRayPointer : MonoBehaviour
    {
        // ---- Config ----

        [Tooltip("Which controller this pointer belongs to.")]
        public OVRInput.Controller controller = OVRInput.Controller.RTouch;

        [Tooltip("Maximum ray distance (metres).")]
        public float maxDistance = 5.0f;

        // ---- Colours ----

        private static readonly Color RAY_DEFAULT = new Color(1.00f, 1.00f, 1.00f, 0.35f);
        private static readonly Color RAY_HOVER   = new Color(1.00f, 0.42f, 0.21f, 0.80f);

        // ---- Private ----

        private LineRenderer _line;
        private EventSystem  _eventSystem;

        private Selectable _hoveredSelectable;

        // ---- Lifecycle ----

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
        }

        private void Start()
        {
            _eventSystem = FindFirstObjectByType<EventSystem>();
            if (_eventSystem == null)
            {
                var esGO = new GameObject("EventSystem");
                _eventSystem = esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }

        private void Update()
        {
#if HAS_META_XR
            bool menuOpen = VRMenuManager.Instance != null && VRMenuManager.Instance.IsOpen;

            _line.enabled = menuOpen;
            if (!menuOpen)
            {
                ClearHover();
                return;
            }

            UpdatePointer();
#else
            _line.enabled = false;
#endif
        }

        private void OnDestroy()
        {
            ClearHover();
        }

        // ---- Core logic ----

        private void UpdatePointer()
        {
            var ray = new Ray(transform.position, transform.forward);
            Vector3 endPoint = ray.origin + ray.direction * maxDistance;
            Selectable newHovered = null;

            // Find all active canvases with GraphicRaycaster
            var raycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);
            float closestDist = maxDistance;

            foreach (var gr in raycasters)
            {
                if (!gr.gameObject.activeInHierarchy) continue;

                var canvas = gr.GetComponent<Canvas>();
                if (canvas == null) continue;

                var canvasRT = gr.GetComponent<RectTransform>();
                if (canvasRT == null) continue;

                // Ray-plane intersection with the canvas plane
                var plane = new Plane(gr.transform.forward, gr.transform.position);
                if (!plane.Raycast(ray, out float dist)) continue;
                if (dist >= closestDist || dist < 0f) continue;

                Vector3 hitWorld = ray.GetPoint(dist);

                // Check if hit is inside canvas rect bounds
                Vector3 hitLocal = canvasRT.InverseTransformPoint(hitWorld);
                var rect = canvasRT.rect;
                if (!rect.Contains(new Vector2(hitLocal.x, hitLocal.y))) continue;

                // Hit canvas -- find topmost interactive element at hit position
                var found = HitTestCanvas(canvas, canvasRT, new Vector2(hitLocal.x, hitLocal.y));
                if (found != null)
                {
                    newHovered = found;
                    closestDist = dist;
                    endPoint = hitWorld;
                }
                else
                {
                    // Hovering over canvas background but no interactive element
                    closestDist = dist;
                    endPoint = hitWorld;
                }
            }

            // Update hover feedback
            if (newHovered != _hoveredSelectable)
            {
                ClearHover();
                if (newHovered != null)
                {
                    _hoveredSelectable = newHovered;
                    SendPointerEvent(_hoveredSelectable.gameObject, ExecuteEvents.pointerEnterHandler);
                }
            }

            // Draw the ray
            _line.SetPosition(0, transform.position);
            _line.SetPosition(1, endPoint);
            Color rc = (newHovered != null) ? RAY_HOVER : RAY_DEFAULT;
            _line.startColor = rc;
            _line.endColor   = new Color(rc.r, rc.g, rc.b, 0f);

            // Trigger click
#if HAS_META_XR
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
            {
                if (_hoveredSelectable != null)
                    ExecuteClick(_hoveredSelectable);
            }
#endif
        }

        /// <summary>
        /// Walk all selectables on the canvas and find the topmost one whose RectTransform
        /// contains the given point in canvas local space.
        /// </summary>
        private Selectable HitTestCanvas(Canvas canvas, RectTransform canvasRT, Vector2 canvasLocalPoint)
        {
            // Canvas scale factor: convert canvas local units to RectTransform local units
            // (they're the same -- canvas local IS RectTransform local).
            var selectables = canvas.GetComponentsInChildren<Selectable>();
            Selectable best = null;
            float bestDepth = float.MaxValue;

            foreach (var sel in selectables)
            {
                if (!sel.IsInteractable()) continue;
                if (!sel.gameObject.activeInHierarchy) continue;

                var selRT = sel.GetComponent<RectTransform>();
                if (selRT == null) continue;

                // Convert canvas-local hit point to this selectable's local space
                // Canvas local -> world -> selectable local
                Vector3 worldHit = canvasRT.TransformPoint(new Vector3(canvasLocalPoint.x, canvasLocalPoint.y, 0f));
                Vector3 selLocal = selRT.InverseTransformPoint(worldHit);

                if (selRT.rect.Contains(new Vector2(selLocal.x, selLocal.y)))
                {
                    // Use hierarchy depth (siblingIndex sum) as z-order approximation
                    // -- shallowest in hierarchy wins for now (buttons are leaf nodes)
                    float depth = GetHierarchyDepth(sel.transform);
                    if (best == null || depth > bestDepth)
                    {
                        best = sel;
                        bestDepth = depth;
                    }
                }
            }

            return best;
        }

        private float GetHierarchyDepth(Transform t)
        {
            float d = 0;
            while (t != null) { d++; t = t.parent; }
            return d;
        }

        // ---- Hover / click helpers ----

        private void ClearHover()
        {
            if (_hoveredSelectable != null)
            {
                SendPointerEvent(_hoveredSelectable.gameObject, ExecuteEvents.pointerExitHandler);
                _hoveredSelectable = null;
            }
        }

        private void SendPointerEvent<T>(GameObject target,
            ExecuteEvents.EventFunction<T> handler) where T : IEventSystemHandler
        {
            if (_eventSystem == null || target == null) return;
            var evt = new PointerEventData(_eventSystem);
            ExecuteEvents.Execute(target, evt, handler);
        }

        private void ExecuteClick(Selectable sel)
        {
            if (sel == null) return;

            var btn = sel as Button;
            if (btn != null)
            {
                btn.onClick.Invoke();
#if HAS_META_XR
                StartCoroutine(PulseVibration(0.3f, 0.3f, 0.08f));
#endif
                return;
            }

            var toggle = sel as Toggle;
            if (toggle != null)
            {
                toggle.isOn = !toggle.isOn;
#if HAS_META_XR
                StartCoroutine(PulseVibration(0.2f, 0.2f, 0.06f));
#endif
                return;
            }

            // Generic click via EventSystem
            SendPointerEvent(sel.gameObject, ExecuteEvents.pointerClickHandler);
        }

#if HAS_META_XR
        private IEnumerator PulseVibration(float freq, float amp, float duration)
        {
            OVRInput.SetControllerVibration(freq, amp, controller);
            yield return new WaitForSeconds(duration);
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }
#endif

        // ---- LineRenderer setup ----

        private void ConfigureLineRenderer()
        {
            _line.positionCount = 2;
            _line.useWorldSpace = true;

            _line.startWidth = 0.004f;
            _line.endWidth   = 0.001f;

            Material mat = new Material(Shader.Find("Sprites/Default"));
            if (mat == null || mat.shader == null)
                mat = new Material(Shader.Find("Unlit/Color"));
            _line.material = mat;

            _line.startColor = RAY_DEFAULT;
            _line.endColor   = new Color(RAY_DEFAULT.r, RAY_DEFAULT.g, RAY_DEFAULT.b, 0f);

            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
        }
    }
}
