// Ensures post-processing is enabled on VR camera at runtime.
// OVRCameraRig creates CenterEyeAnchor camera dynamically --
// editor-time settings don't apply to it.

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class VRPostProcessEnable : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnableOnAllCameras()
    {
        // Delay one frame so OVRCameraRig has created its cameras
        var helper = new GameObject("_PostProcessHelper").AddComponent<PostProcessHelper>();
        helper.StartCoroutine(helper.EnableNextFrame());
    }
}

public class PostProcessHelper : MonoBehaviour
{
    public System.Collections.IEnumerator EnableNextFrame()
    {
        yield return null; // wait one frame
        yield return null; // wait another for safety

        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int enabled = 0;
        foreach (var cam in cams)
        {
            // OVRCameraRig cameras default to SolidColor (black) -- force Skybox
            if (cam.clearFlags != CameraClearFlags.Skybox)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                Debug.Log($"[POSTFX] {cam.name}: clearFlags -> Skybox");
            }

            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (!data.renderPostProcessing)
            {
                data.renderPostProcessing = true;
                enabled++;
            }
        }
        Debug.Log($"[POSTFX] Enabled post-processing on {enabled} cameras (total {cams.Length}).");
        Destroy(gameObject);
    }
}
