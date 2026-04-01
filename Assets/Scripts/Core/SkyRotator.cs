// SkyRotator.cs
// CYBERNOMAD -- Slowly rotates skybox to simulate wind/cloud movement.
// Direction matches water scroll for visual consistency.

using UnityEngine;

public class SkyRotator : MonoBehaviour
{
    public static float RotationSpeed = 0.5f; // degrees per second

    private Material _skyMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_SkyRotator");
        go.AddComponent<SkyRotator>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        _skyMat = RenderSettings.skybox;
    }

    void Update()
    {
        if (_skyMat == null || !_skyMat.HasFloat("_Rotation")) return;

        float rot = _skyMat.GetFloat("_Rotation");
        rot += RotationSpeed * Time.deltaTime;
        if (rot > 360f) rot -= 360f;
        if (rot < 0f) rot += 360f;
        _skyMat.SetFloat("_Rotation", rot);
    }
}
