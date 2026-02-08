// PLAGA '44 - Status Effects UI
// Active status effect icons (hypothermia, dehydration, radiation, etc.)
// CYBERNOMAD 2024-2026

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI.HUD
{
    /// <summary>
    /// Displays active status effect icons on the HUD.
    /// Each status effect has an icon, severity level, and optional timer.
    /// Icons pulse/flash at critical severity.
    /// </summary>
    public class StatusEffectsUI : MonoBehaviour
    {
        /// <summary>
        /// Defines a known status effect type with its visual representation.
        /// </summary>
        [System.Serializable]
        public class StatusEffectDefinition
        {
            public string effectId;
            public string displayName;       // Polish name shown to player
            public Sprite icon;
            public Color tintColor = Color.white;
            public Color criticalColor = Color.red;
        }

        /// <summary>
        /// Runtime state for an active status effect.
        /// </summary>
        public class ActiveStatusEffect
        {
            public string effectId;
            public float severity;          // 0-1 normalized
            public float duration;          // remaining seconds, -1 = indefinite
            public bool isCritical;
            public GameObject uiInstance;
            public Image iconImage;
            public Image severityFill;
            public Text timerLabel;
        }

        [Header("Layout")]
        [SerializeField] private RectTransform effectsContainer;
        [SerializeField] private GameObject statusEffectPrefab;

        [Header("Definitions")]
        [SerializeField] private List<StatusEffectDefinition> effectDefinitions = new List<StatusEffectDefinition>();

        [Header("Animation")]
        [SerializeField] private float criticalPulseSpeed = 3f;
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        // Runtime tracking
        private Dictionary<string, ActiveStatusEffect> activeEffects = new Dictionary<string, ActiveStatusEffect>();
        private Dictionary<string, StatusEffectDefinition> definitionLookup = new Dictionary<string, StatusEffectDefinition>();

        private float pulseTimer;

        private void Awake()
        {
            // Build lookup table
            foreach (var def in effectDefinitions)
            {
                if (!string.IsNullOrEmpty(def.effectId))
                {
                    definitionLookup[def.effectId] = def;
                }
            }

            // Register default effect definitions if none assigned in inspector
            RegisterDefaultEffects();
        }

        private void Update()
        {
            pulseTimer += Time.unscaledDeltaTime;

            foreach (var kvp in activeEffects)
            {
                UpdateEffectVisual(kvp.Value);

                // Count down duration
                if (kvp.Value.duration > 0f)
                {
                    kvp.Value.duration -= Time.deltaTime;
                    if (kvp.Value.duration <= 0f)
                    {
                        RemoveEffect(kvp.Key);
                    }
                }
            }
        }

        // ----- Public API -----

        /// <summary>
        /// Adds or updates a status effect on the HUD.
        /// </summary>
        /// <param name="effectId">Unique effect identifier (e.g., "hypothermia", "dehydration")</param>
        /// <param name="severity">Severity 0-1 (0 = mild, 1 = lethal)</param>
        /// <param name="duration">Duration in seconds, -1 for indefinite</param>
        public void SetEffect(string effectId, float severity, float duration = -1f)
        {
            severity = Mathf.Clamp01(severity);
            bool isCritical = severity >= 0.75f;

            if (activeEffects.ContainsKey(effectId))
            {
                // Update existing
                var effect = activeEffects[effectId];
                effect.severity = severity;
                effect.duration = duration;
                effect.isCritical = isCritical;
            }
            else
            {
                // Create new
                ActiveStatusEffect effect = CreateEffectInstance(effectId, severity, duration, isCritical);
                if (effect != null)
                {
                    activeEffects[effectId] = effect;
                }
            }
        }

        /// <summary>
        /// Removes a status effect from the HUD.
        /// </summary>
        public void RemoveEffect(string effectId)
        {
            if (activeEffects.TryGetValue(effectId, out ActiveStatusEffect effect))
            {
                if (effect.uiInstance != null)
                {
                    // Could animate fade-out here
                    Destroy(effect.uiInstance);
                }
                activeEffects.Remove(effectId);
            }
        }

        /// <summary>
        /// Clears all active status effects.
        /// </summary>
        public void ClearAllEffects()
        {
            foreach (var kvp in activeEffects)
            {
                if (kvp.Value.uiInstance != null)
                    Destroy(kvp.Value.uiInstance);
            }
            activeEffects.Clear();
        }

        /// <summary>
        /// Bulk update from a list of effect IDs and severities.
        /// Removes effects not in the list.
        /// </summary>
        public void UpdateFromPhysiology(Dictionary<string, float> effectSeverities)
        {
            // Add/update effects present in the update
            foreach (var kvp in effectSeverities)
            {
                if (kvp.Value > 0.01f)
                {
                    SetEffect(kvp.Key, kvp.Value);
                }
                else
                {
                    RemoveEffect(kvp.Key);
                }
            }

            // Remove effects not in the update
            List<string> toRemove = new List<string>();
            foreach (var activeId in activeEffects.Keys)
            {
                if (!effectSeverities.ContainsKey(activeId))
                {
                    toRemove.Add(activeId);
                }
            }
            foreach (string id in toRemove)
            {
                RemoveEffect(id);
            }
        }

        /// <summary>
        /// Returns true if any status effect is currently at critical severity.
        /// </summary>
        public bool HasCriticalEffect()
        {
            foreach (var kvp in activeEffects)
            {
                if (kvp.Value.isCritical) return true;
            }
            return false;
        }

        // ----- Internal -----

        private ActiveStatusEffect CreateEffectInstance(string effectId, float severity, float duration, bool isCritical)
        {
            if (effectsContainer == null || statusEffectPrefab == null)
            {
                Debug.LogWarning($"[StatusEffectsUI] Cannot create effect '{effectId}': missing container or prefab.");
                return null;
            }

            StatusEffectDefinition def = null;
            definitionLookup.TryGetValue(effectId, out def);

            GameObject instance = Instantiate(statusEffectPrefab, effectsContainer);
            instance.name = $"StatusEffect_{effectId}";

            var effect = new ActiveStatusEffect
            {
                effectId = effectId,
                severity = severity,
                duration = duration,
                isCritical = isCritical,
                uiInstance = instance,
                iconImage = instance.GetComponent<Image>(),
                severityFill = instance.transform.Find("SeverityFill")?.GetComponent<Image>(),
                timerLabel = instance.GetComponentInChildren<Text>()
            };

            // Apply definition visuals
            if (def != null)
            {
                if (effect.iconImage != null && def.icon != null)
                    effect.iconImage.sprite = def.icon;
                if (effect.iconImage != null)
                    effect.iconImage.color = def.tintColor;
            }

            return effect;
        }

        private void UpdateEffectVisual(ActiveStatusEffect effect)
        {
            if (effect.uiInstance == null) return;

            StatusEffectDefinition def = null;
            definitionLookup.TryGetValue(effect.effectId, out def);

            // Update severity fill
            if (effect.severityFill != null)
            {
                effect.severityFill.fillAmount = effect.severity;
            }

            // Update timer label
            if (effect.timerLabel != null && effect.duration > 0f)
            {
                int seconds = Mathf.CeilToInt(effect.duration);
                int min = seconds / 60;
                int sec = seconds % 60;
                effect.timerLabel.text = $"{min}:{sec:D2}";
            }

            // Pulse critical effects
            if (effect.isCritical && effect.iconImage != null)
            {
                float pulse = (Mathf.Sin(pulseTimer * criticalPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                Color baseColor = def != null ? def.criticalColor : Color.red;
                Color normalTint = def != null ? def.tintColor : Color.white;
                effect.iconImage.color = Color.Lerp(normalTint, baseColor, pulse);
            }
        }

        private void RegisterDefaultEffects()
        {
            // Only add defaults if no definitions were set in inspector
            if (effectDefinitions.Count > 0) return;

            AddDefaultDefinition("hypothermia", "Hipotermia", new Color(0.3f, 0.6f, 1f));
            AddDefaultDefinition("dehydration", "Odwodnienie", new Color(1f, 0.8f, 0.2f));
            AddDefaultDefinition("starvation", "Glod", new Color(0.8f, 0.5f, 0.2f));
            AddDefaultDefinition("radiation", "Promieniowanie", new Color(0.4f, 1f, 0.2f));
            AddDefaultDefinition("infection", "Infekcja", new Color(0.7f, 0.9f, 0.1f));
            AddDefaultDefinition("blood_loss", "Krwotok", new Color(0.8f, 0.1f, 0.1f));
            AddDefaultDefinition("exhaustion", "Wyczerpanie", new Color(0.6f, 0.6f, 0.6f));
            AddDefaultDefinition("pain", "Bol", new Color(1f, 0.4f, 0.4f));
            AddDefaultDefinition("concussion", "Kontuzja", new Color(0.9f, 0.9f, 0.3f));
        }

        private void AddDefaultDefinition(string id, string displayName, Color tint)
        {
            var def = new StatusEffectDefinition
            {
                effectId = id,
                displayName = displayName,
                tintColor = tint,
                criticalColor = Color.red
            };
            effectDefinitions.Add(def);
            definitionLookup[id] = def;
        }
    }
}
