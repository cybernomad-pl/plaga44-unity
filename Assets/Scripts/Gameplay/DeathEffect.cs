using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// DeathEffect -- optional cosmetic layer on top of ragdoll death.
    ///
    /// Plays a particle system and/or a one-shot AudioClip at the moment of death.
    /// Designed as a placeholder: swap out particleSystem / audioClip references
    /// in the Inspector without changing code.
    ///
    /// Called automatically by MorsCerebri.Die() if this component is present.
    /// Can also be called manually: deathEffect.Play(hitPoint, hitDirection).
    /// </summary>
    public class DeathEffect : MonoBehaviour
    {
        [Header("Particles")]
        [Tooltip("Particle system to play at the hit point on death. Can be a prefab or scene instance.")]
        [SerializeField] private ParticleSystem bloodParticle;

        [Tooltip("If true, the particle system is spawned as an independent GameObject (won't " +
                 "be disabled when this GO becomes inactive). Recommended for ragdolls.")]
        [SerializeField] private bool spawnParticleIndependently = true;

        [Header("Audio")]
        [Tooltip("Sound played at death. Use a short impact/squelch SFX.")]
        [SerializeField] private AudioClip deathSound;

        [Tooltip("Volume for the death sound (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float soundVolume = 1f;

        [Tooltip("Pitch variation range +/- applied randomly to avoid repetition.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float pitchVariation = 0.1f;

        [Header("Debug")]
        [Tooltip("Log a message when effect plays (useful during scene testing without assets).")]
        [SerializeField] private bool logOnPlay = true;

        /// <summary>
        /// Triggers the death effect at the given world-space position.
        /// Called by MorsCerebri after ragdoll activation.
        /// </summary>
        /// <param name="position">World-space hit point.</param>
        /// <param name="direction">Impact direction (used to orient particle emission).</param>
        public void Play(Vector3 position, Vector3 direction)
        {
            PlayParticle(position, direction);
            PlaySound(position);

            if (logOnPlay)
                Debug.Log($"[DeathEffect] Effect played at {position} dir={direction} on {gameObject.name}");
        }

        // -----------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------

        private void PlayParticle(Vector3 position, Vector3 direction)
        {
            if (bloodParticle == null) return;

            Quaternion rotation = direction != Vector3.zero
                ? Quaternion.LookRotation(direction)
                : Quaternion.identity;

            if (spawnParticleIndependently)
            {
                // Instantiate so the particle outlives the (possibly deactivated) enemy GO
                ParticleSystem instance = Instantiate(bloodParticle, position, rotation);
                instance.Play();

                // Auto-destroy after particle finishes
                float lifetime = instance.main.duration + instance.main.startLifetime.constantMax;
                Destroy(instance.gameObject, lifetime + 0.5f);
            }
            else
            {
                bloodParticle.transform.SetPositionAndRotation(position, rotation);
                bloodParticle.Play();
            }
        }

        private void PlaySound(Vector3 position)
        {
            if (deathSound == null) return;

            float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

            // PlayClipAtPoint spawns a temporary AudioSource that survives GO deactivation
            // We can't set pitch via PlayClipAtPoint, so we create a temporary source instead
            GameObject tempGo = new GameObject("DeathSFX_Temp");
            tempGo.transform.position = position;

            AudioSource src = tempGo.AddComponent<AudioSource>();
            src.clip        = deathSound;
            src.volume      = soundVolume;
            src.pitch       = pitch;
            src.spatialBlend = 1f;  // full 3D
            src.Play();

            Destroy(tempGo, deathSound.length + 0.2f);
        }
    }
}
