using UnityEngine;

/// <summary>
/// PlayerAvatar -- spawns PINEA as player body using Meta's OVRUnityHumanoidSkeletonRetargeter.
/// This is the CORRECT way to do body retargeting on Quest.
///
/// Setup:
/// 1. Spawn PINEA_rigged prefab
/// 2. Add OVRBody (body tracking source)
/// 3. Add OVRUnityHumanoidSkeletonRetargeter (retargets body tracking -> humanoid)
/// 4. Hide head bone
/// 5. Hide OVR controller hands
/// </summary>
public class PlayerAvatar : MonoBehaviour
{
    private const string AVATAR_PATH = "PLAGA44/Characters/PINEA/PINEA_rigged";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_PlayerAvatar");
        go.AddComponent<PlayerAvatar>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        Debug.Log("[AVATAR] Start -- setting up Meta retargeted body...");

        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null) { Debug.LogWarning("[AVATAR] no OVRCameraRig"); return; }

        // Spawn avatar
        var prefab = Resources.Load<GameObject>(AVATAR_PATH);
        if (prefab == null) { Debug.LogError($"[AVATAR] '{AVATAR_PATH}' not in Resources!"); return; }

        var avatar = Instantiate(prefab);
        avatar.name = "PlayerBody";
        avatar.transform.localScale = Vector3.one * 1.2f; // 20% bigger

        // Ensure Animator is Humanoid and ENABLED (retargeter needs it)
        var animator = avatar.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("[AVATAR] PINEA is not Humanoid!");
            return;
        }
        animator.enabled = true;

        // Add OVRBody if not present (body tracking data source)
        var ovrBody = avatar.GetComponent<OVRBody>();
        if (ovrBody == null)
            ovrBody = avatar.AddComponent<OVRBody>();

        // Add Meta's retargeter -- this does ALL the work
        var retargeter = avatar.GetComponent<OVRUnityHumanoidSkeletonRetargeter>();
        if (retargeter == null)
            retargeter = avatar.AddComponent<OVRUnityHumanoidSkeletonRetargeter>();

        Debug.Log("[AVATAR] OVRUnityHumanoidSkeletonRetargeter added -- Meta handles retargeting");

        // Hide head (first person)
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head != null)
        {
            head.localScale = Vector3.one * 0.01f;
            Debug.Log("[AVATAR] Head hidden");
        }

        // Hide OVR controller hand models
        HideOVRHands(rig);

        // Near clip -- increase to avoid seeing inside model
        var cam = Camera.main;
        if (cam != null)
        {
            cam.nearClipPlane = 0.15f;
            Debug.Log("[AVATAR] Near clip set to 0.15");
        }

        Debug.Log("[AVATAR] READY -- Meta retargeted body active");
    }

    void HideOVRHands(OVRCameraRig rig)
    {
        int hidden = 0;

        // SkinnedMeshRenderers (hand meshes)
        var smrs = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var r in smrs)
        {
            string n = r.gameObject.name.ToLower();
            if (n.Contains("hand") || n.Contains("controller"))
            {
                r.enabled = false;
                hidden++;
            }
        }

        // OVRControllerHelper renderers
        var helpers = rig.GetComponentsInChildren<OVRControllerHelper>(true);
        foreach (var h in helpers)
        {
            foreach (var r in h.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
                hidden++;
            }
        }

        Debug.Log($"[AVATAR] Hidden {hidden} OVR hand/controller renderers");
    }
}
