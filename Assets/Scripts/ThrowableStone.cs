using UnityEngine;

namespace PLAGA44
{
    /// <summary>
    /// Component for throwable stone objects.
    /// Tracks who threw the stone and detects throw events.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowableStone : MonoBehaviour
    {
        [Header("Throw Detection")]
        [Tooltip("Minimum velocity magnitude to consider stone as thrown")]
        [SerializeField] private float throwVelocityThreshold = 0.5f;

        private Rigidbody rb;
        private bool isThrown = false;
        private GameObject lastThrower;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // Ensure continuous collision detection for fast-moving object
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private void FixedUpdate()
        {
            // Detect if stone has been thrown (high velocity after release)
            if (!isThrown && rb.linearVelocity.magnitude > throwVelocityThreshold)
            {
                isThrown = true;
                OnThrown();
            }
        }

        /// <summary>
        /// Sets who threw this stone. Call this when player grabs/releases the stone.
        /// </summary>
        public void SetThrower(GameObject thrower)
        {
            lastThrower = thrower;
            isThrown = false; // Reset throw state when grabbed
        }

        /// <summary>
        /// Gets the GameObject that last threw this stone.
        /// </summary>
        public GameObject GetLastThrower()
        {
            return lastThrower;
        }

        /// <summary>
        /// Returns whether the stone has been thrown.
        /// </summary>
        public bool IsThrown()
        {
            return isThrown;
        }

        /// <summary>
        /// Called when stone is detected as thrown.
        /// </summary>
        private void OnThrown()
        {
            // Optional: Add throw effects here (sound, trail, etc.)
        }

        /// <summary>
        /// Gets the current velocity of the stone.
        /// </summary>
        public Vector3 GetVelocity()
        {
            return rb.linearVelocity;
        }

        /// <summary>
        /// Gets the mass of the stone.
        /// </summary>
        public float GetMass()
        {
            return rb.mass;
        }
    }
}
