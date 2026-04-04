using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// M249 Disassembly -- index finger trigger podswietla czesc, grip = odlacz.
///
/// Workflow:
/// 1. Wskazujesz index fingerem na czesc M249
/// 2. Czesc sie podswietla (emission glow)
/// 3. Grip = odlaczasz czesc (staje sie osobny grabbable)
/// 4. Mozesz ja odlozyc, obejrzec, podlaczyc z powrotem
///
/// Czesci: grip_trigger, handguard, magazine, receiver, stock + bipod
/// </summary>
public class M249Disassembly : MonoBehaviour
{
    [Header("Config")]
    public Color highlightColor = new Color(0.3f, 0.8f, 1f, 1f); // cyan glow
    public float highlightEmission = 0.5f;
    public float detachForce = 0.5f; // lekki impuls przy odlaczeniu
    public float reattachDistance = 0.15f; // dystans snap-back

    [Header("State")]
    public Transform highlightedPart;
    public List<DetachedPart> detachedParts = new List<DetachedPart>();

    [System.Serializable]
    public class DetachedPart
    {
        public Transform part;
        public Transform originalParent;
        public Vector3 originalLocalPos;
        public Quaternion originalLocalRot;
    }

    // Cache
    private Dictionary<Renderer, Color> _originalEmission = new Dictionary<Renderer, Color>();
    private Transform _lastHighlighted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        // Szukaj M249 w scenie
        var m249s = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in m249s)
        {
            if (!t.name.Contains("M249") || t.name.Contains("_part_") || t.name.Contains("_Mesh")) continue;
            if (t.GetComponent<M249Disassembly>() != null) continue;

            // Sprawdz czy ma dzieci (czesci)
            if (t.childCount > 0)
            {
                t.gameObject.AddComponent<M249Disassembly>();
                Debug.Log($"[M249] Disassembly added to {t.name} ({t.childCount} parts)");
            }
        }
    }

    void Update()
    {
        // Block weapon interaction while any menu is open
        if (Plaga44.UI.VRMenuManager.MenuOpen || VRQualityMenu.MenuOpen) return;

        CheckIndexFingerPointing();
        CheckDisassembleInput();
        CheckReattach();
    }

    void CheckIndexFingerPointing()
    {
        // Raycast z prawego index fingera
        Vector3 fingerPos = Vector3.zero;
        Vector3 fingerDir = Vector3.forward;

        // Probuj uzyc OVRSkeleton do pozycji palca
        bool hasFingerData = false;

#if HAS_META_XR
        // Prawy kontroler -- index trigger direction
        fingerPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        fingerDir = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch) * Vector3.forward;
        hasFingerData = true;

        // Tez lewy
        if (OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger) > 0.5f)
        {
            fingerPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            fingerDir = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch) * Vector3.forward;
        }
#endif

        if (!hasFingerData) return;

        // Index trigger nacisnienty?
        float rightIndex = 0f;
        float leftIndex = 0f;
#if HAS_META_XR
        rightIndex = OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger);
        leftIndex = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger);
#endif

        bool indexActive = rightIndex > 0.3f || leftIndex > 0.3f;
        if (!indexActive)
        {
            ClearHighlight();
            return;
        }

        // Raycast
        if (Physics.Raycast(fingerPos, fingerDir, out RaycastHit hit, 2f))
        {
            // Sprawdz czy trafiona czesc jest dzieckiem tego M249
            Transform hitPart = hit.transform;
            if (IsMyPart(hitPart))
            {
                SetHighlight(hitPart);
            }
            else
            {
                ClearHighlight();
            }
        }
        else
        {
            ClearHighlight();
        }
    }

    bool IsMyPart(Transform t)
    {
        // Sprawdz czy t jest bezposrednim lub posrednim dzieckiem tego M249
        Transform check = t;
        while (check != null)
        {
            if (check == transform) return true;
            check = check.parent;
        }
        // Sprawdz tez odlaczone czesci
        foreach (var dp in detachedParts)
        {
            if (dp.part == t) return true;
        }
        return false;
    }

    void SetHighlight(Transform part)
    {
        if (part == _lastHighlighted) return;

        ClearHighlight();
        _lastHighlighted = part;
        highlightedPart = part;

        // Podswietl -- emission glow
        var renderers = part.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                if (!_originalEmission.ContainsKey(r))
                    _originalEmission[r] = mat.GetColor("_EmissionColor");

                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", highlightColor * highlightEmission);
            }
        }

        // Haptic pulse
#if HAS_META_XR
        OVRInput.SetControllerVibration(0.5f, 0.2f, OVRInput.Controller.RTouch);
#endif
    }

    void ClearHighlight()
    {
        if (_lastHighlighted == null) return;

        var renderers = _lastHighlighted.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (_originalEmission.TryGetValue(r, out Color orig))
            {
                foreach (var mat in r.materials)
                    mat.SetColor("_EmissionColor", orig);
            }
        }

        _lastHighlighted = null;
        highlightedPart = null;
        _originalEmission.Clear();

#if HAS_META_XR
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
#endif
    }

    void CheckDisassembleInput()
    {
        if (highlightedPart == null) return;

        // Grip = disassemble
        bool gripPressed = false;
#if HAS_META_XR
        gripPressed = OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch)
                   || OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
#endif

        if (gripPressed)
        {
            DetachPart(highlightedPart);
        }
    }

    void DetachPart(Transform part)
    {
        // Zapamietaj oryginalna pozycje
        var dp = new DetachedPart
        {
            part = part,
            originalParent = part.parent,
            originalLocalPos = part.localPosition,
            originalLocalRot = part.localRotation,
        };
        detachedParts.Add(dp);

        // Odlacz od rodzica
        part.SetParent(null);

        // Dodaj rigidbody jesli nie ma
        var rb = part.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = part.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
        }
        rb.isKinematic = false;
        rb.useGravity = true;

        // Lekki impuls do przodu
        rb.AddForce(part.forward * detachForce, ForceMode.Impulse);

        // Dodaj OVRGrabbable jesli nie ma
        var grabbable = part.GetComponent<OVRGrabbable>();
        if (grabbable == null)
        {
            grabbable = part.gameObject.AddComponent<OVRGrabbable>();
        }

        // Haptic feedback -- mocny
#if HAS_META_XR
        OVRInput.SetControllerVibration(1f, 0.8f, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(1f, 0.8f, OVRInput.Controller.LTouch);
#endif

        ClearHighlight();

        Debug.Log($"[M249] DETACHED: {part.name} -- now grabbable, has physics");
    }

    void CheckReattach()
    {
        // Jesli odlaczona czesc jest blisko oryginalnej pozycji i nie jest trzymana -- snap back
        for (int i = detachedParts.Count - 1; i >= 0; i--)
        {
            var dp = detachedParts[i];
            if (dp.part == null) { detachedParts.RemoveAt(i); continue; }

            var grabbable = dp.part.GetComponent<OVRGrabbable>();
            if (grabbable != null && grabbable.isGrabbed) continue;

            Vector3 targetWorld = dp.originalParent.TransformPoint(dp.originalLocalPos);
            float dist = Vector3.Distance(dp.part.position, targetWorld);

            if (dist < reattachDistance)
            {
                // Snap back
                var rb = dp.part.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                dp.part.SetParent(dp.originalParent);
                dp.part.localPosition = dp.originalLocalPos;
                dp.part.localRotation = dp.originalLocalRot;

                detachedParts.RemoveAt(i);

#if HAS_META_XR
                OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.RTouch);
#endif

                Debug.Log($"[M249] REATTACHED: {dp.part.name}");
            }
        }
    }
}
