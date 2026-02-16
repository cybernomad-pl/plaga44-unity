using UnityEngine;
using UnityEditor;

namespace PLAGA44.Editor
{
    /// <summary>
    /// Editor utility to setup stone throw test scene.
    /// Creates throwable stone and target mannequin with hit zones.
    /// </summary>
    public class StoneThrowSetup
    {
        [MenuItem("CYBERNOMAD/Combat/Setup Stone Throw Test")]
        private static void SetupStoneThrowTest()
        {
            // Create stone
            GameObject stone = CreateThrowableStone();
            Debug.Log($"Created throwable stone: {stone.name}");

            // Create target mannequin
            GameObject mannequin = CreateTargetMannequin();
            Debug.Log($"Created target mannequin: {mannequin.name}");

            // Select mannequin in hierarchy for easy inspection
            Selection.activeGameObject = mannequin;

            Debug.Log("Stone throw test setup complete. Stone positioned at (0,1,0), Mannequin at (0,1,3)");
        }

        /// <summary>
        /// Creates a throwable stone GameObject with ThrowableStone component.
        /// </summary>
        private static GameObject CreateThrowableStone()
        {
            // Create sphere primitive
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "ThrowableStone";

            // Scale down to reasonable stone size
            stone.transform.localScale = Vector3.one * 0.15f;
            stone.transform.position = new Vector3(0, 1, 0);

            // Setup rigidbody
            Rigidbody rb = stone.GetComponent<Rigidbody>();
            if (rb == null)
                rb = stone.AddComponent<Rigidbody>();

            rb.mass = 0.5f; // Small stone
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;

            // Add throwable component
            stone.AddComponent<ThrowableStone>();

            return stone;
        }

        /// <summary>
        /// Creates a target mannequin with hit zones and MorsCerebri component.
        /// </summary>
        private static GameObject CreateTargetMannequin()
        {
            // Create root mannequin object
            GameObject mannequin = new GameObject("TargetMannequin");
            mannequin.transform.position = new Vector3(0, 1, 3);

            // Add MorsCerebri component
            MorsCerebri morsCerebri = mannequin.AddComponent<MorsCerebri>();

            // Create body (capsule)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(mannequin.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            // Add rigidbody to body (for ragdoll)
            Rigidbody bodyRb = body.AddComponent<Rigidbody>();
            bodyRb.mass = 70f; // Average human mass
            bodyRb.isKinematic = true; // Start kinematic, ragdoll will enable

            // Add HitZone to body
            HitZone bodyZone = body.AddComponent<HitZone>();
            bodyZone.SetZoneType(HitZoneType.Body);

            // Create head (sphere)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(mannequin.transform);
            head.transform.localPosition = new Vector3(0, 1.2f, 0);
            head.transform.localScale = Vector3.one * 0.3f;

            // Add rigidbody to head (for ragdoll)
            Rigidbody headRb = head.AddComponent<Rigidbody>();
            headRb.mass = 5f; // Head mass
            headRb.isKinematic = true; // Start kinematic, ragdoll will enable

            // Add HitZone to head
            HitZone headZone = head.AddComponent<HitZone>();
            headZone.SetZoneType(HitZoneType.Head);

            // Auto-setup ragdoll
            morsCerebri.AutoSetupRagdoll();

            return mannequin;
        }

        [MenuItem("CYBERNOMAD/Combat/Clear Stone Throw Test")]
        private static void ClearStoneThrowTest()
        {
            // Find and destroy test objects
            GameObject stone = GameObject.Find("ThrowableStone");
            if (stone != null)
            {
                Object.DestroyImmediate(stone);
                Debug.Log("Removed ThrowableStone");
            }

            GameObject mannequin = GameObject.Find("TargetMannequin");
            if (mannequin != null)
            {
                Object.DestroyImmediate(mannequin);
                Debug.Log("Removed TargetMannequin");
            }

            Debug.Log("Stone throw test cleared");
        }
    }
}
