using UnityEngine;

/// <summary>
/// GrabLogger -- loguje pozycje/rotacje/skale KAZDEGO trzymanego obiektu co 0.5s.
/// Auto-setup: znajduje wszystkie OVRGrabbable i podpina sie.
/// Logi mozna potem uzyc do ustawienia defaultowych transformow.
/// </summary>
public class GrabLogger : MonoBehaviour
{
    private float _logInterval = 0.5f;
    private float _timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
#if LOCOMOTION_ONLY
        return;
#endif
        var go = new GameObject("_GrabLogger");
        go.AddComponent<GrabLogger>();
        DontDestroyOnLoad(go);
        Debug.Log("[GrabLogger] Active -- logging grabbed object transforms every 0.5s");
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0) return;
        _timer = _logInterval;

        var grabbables = FindObjectsByType<OVRGrabbable>(FindObjectsSortMode.None);
        foreach (var g in grabbables)
        {
            if (!g.isGrabbed) continue;

            var t = g.transform;
            Debug.Log($"[GRAB] {g.name} " +
                      $"pos=({t.localPosition.x:F4},{t.localPosition.y:F4},{t.localPosition.z:F4}) " +
                      $"rot=({t.localEulerAngles.x:F1},{t.localEulerAngles.y:F1},{t.localEulerAngles.z:F1}) " +
                      $"scale=({t.localScale.x:F4},{t.localScale.y:F4},{t.localScale.z:F4}) " +
                      $"worldPos=({t.position.x:F4},{t.position.y:F4},{t.position.z:F4}) " +
                      $"worldRot=({t.eulerAngles.x:F1},{t.eulerAngles.y:F1},{t.eulerAngles.z:F1})");
        }
    }
}
