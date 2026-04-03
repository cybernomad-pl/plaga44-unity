// M249GripFix.cs
// CYBERNOMAD -- Fixes M249 orientation when grabbed.
// FBX model is rotated -90 on X. When grabbed, barrel should point
// along controller's forward (index finger direction).

using UnityEngine;

public class M249GripFix : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_M249GripFix");
        go.AddComponent<M249GripFix>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        // Find all M249 instances and fix their grab setup
        Invoke(nameof(FixAll), 1f); // delay to let spawner create items
    }

    public static void FixAll()
    {
        var grabbables = FindObjectsByType<OVRGrabbable>(FindObjectsSortMode.None);
        foreach (var g in grabbables)
        {
            if (!g.gameObject.name.Contains("M249")) continue;
            FixGrip(g);
        }
    }

    public static void FixGrip(OVRGrabbable grabbable)
    {
        var go = grabbable.gameObject;

        // Check if already fixed
        if (go.transform.Find("_GripOffset") != null) return;

        // Create grip offset child
        var gripOffset = new GameObject("_GripOffset");
        gripOffset.transform.SetParent(go.transform, false);

        // The model is rotated -90 on X in local space (FBX convention).
        // When OVRGrabber grabs, it aligns the object's forward with controller forward.
        // We need the barrel (model's local +Y after -90X rotation = world +Z) to align
        // with controller forward.
        //
        // Snap offset rotation: rotate so barrel aligns with grab forward
        gripOffset.transform.localPosition = Vector3.zero;
        gripOffset.transform.localRotation = Quaternion.Euler(0, 0, 0);

        // Enable snap orientation on the OVRGrabbable
        // Use reflection since fields are serialized private
        var type = typeof(OVRGrabbable);

        var snapOrient = type.GetField("m_snapOrientation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (snapOrient != null) snapOrient.SetValue(grabbable, true);

        var snapPos = type.GetField("m_snapPosition",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (snapPos != null) snapPos.SetValue(grabbable, true);

        var snapOffsetField = type.GetField("m_snapOffset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (snapOffsetField != null) snapOffsetField.SetValue(grabbable, gripOffset.transform);

        // Set grip offset rotation: barrel forward = controller forward
        // Model -90X means model's +Y is barrel. We need +Y -> +Z (controller forward)
        gripOffset.transform.localRotation = Quaternion.Euler(90, 0, 0);
        // Grip position: handle is roughly at center
        gripOffset.transform.localPosition = new Vector3(0, 0, -0.05f);

        Debug.Log($"[PLAGA44] M249GripFix: snap orientation applied to {go.name}");
    }
}
