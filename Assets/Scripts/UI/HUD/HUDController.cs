// PLAGA '44 - HUD Controller
// Minimal survival HUD: health, hunger, thirst, temperature bars
// CYBERNOMAD 2024-2026

using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI.HUD
{
    /// <summary>
    /// Controls the in-game survival HUD overlay.
    /// Displays vital stat bars (health, hunger, thirst, temperature)
    /// with color-coded warning states. Designed for VR readability.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Vital Bars")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider hungerBar;
        [SerializeField] private Slider thirstBar;
        [SerializeField] private Slider temperatureBar;

        [Header("Bar Fill Images")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image hungerFill;
        [SerializeField] private Image thirstFill;
        [SerializeField] private Image temperatureFill;

        [Header("Value Labels")]
        [SerializeField] private Text healthLabel;
        [SerializeField] private Text hungerLabel;
        [SerializeField] private Text thirstLabel;
        [SerializeField] private Text temperatureLabel;

        [Header("HUD Container")]
        [SerializeField] private CanvasGroup hudCanvasGroup;

        [Header("Warning Thresholds")]
        [SerializeField] private float criticalThreshold = 20f;
        [SerializeField] private float warningThreshold = 40f;

        [Header("Colors - CYBERNOMAD Palette")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.8f, 0.2f);       // #33CC33 green
        [SerializeField] private Color warningColor = new Color(1f, 0.65f, 0f);           // #FFA500 amber
        [SerializeField] private Color criticalColor = new Color(0.9f, 0.1f, 0.1f);       // #E61A1A red
        [SerializeField] private Color coldColor = new Color(0.3f, 0.6f, 1f);             // #4D99FF blue (hypothermia)
        [SerializeField] private Color hotColor = new Color(1f, 0.3f, 0f);                // #FF4D00 orange (heat)

        [Header("Temperature Range")]
        [SerializeField] private float minTemperature = 30f;  // Hypothermia death zone
        [SerializeField] private float maxTemperature = 42f;  // Hyperthermia death zone
        [SerializeField] private float normalTempLow = 36f;
        [SerializeField] private float normalTempHigh = 37.5f;

        [Header("Animation")]
        [SerializeField] private float criticalPulseSpeed = 2f;
        [SerializeField] private float barLerpSpeed = 5f;

        // Cached target values for smooth lerping
        private float targetHealth;
        private float targetHunger;
        private float targetThirst;
        private float targetTemperature;

        private bool isVisible = true;
        private float pulseTimer;

        private void Start()
        {
            // Initialize bars to full
            targetHealth = 100f;
            targetHunger = 100f;
            targetThirst = 100f;
            targetTemperature = 36.6f;

            SetBarsImmediate();
        }

        private void Update()
        {
            if (!isVisible) return;

            // Smoothly lerp bars to target values
            LerpBars();

            // Pulse critical bars
            PulseCriticalBars();
        }

        // ----- Public API -----

        /// <summary>
        /// Updates all vital displays from physiology data.
        /// Called by the physiology system each frame or on change.
        /// </summary>
        public void UpdateVitals(float health, float hunger, float thirst, float bodyTemp)
        {
            targetHealth = Mathf.Clamp(health, 0f, 100f);
            targetHunger = Mathf.Clamp(hunger, 0f, 100f);
            targetThirst = Mathf.Clamp(thirst, 0f, 100f);
            targetTemperature = Mathf.Clamp(bodyTemp, minTemperature, maxTemperature);
        }

        /// <summary>
        /// Show or hide the HUD with a fade.
        /// </summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = visible ? 1f : 0f;
                hudCanvasGroup.interactable = visible;
                hudCanvasGroup.blocksRaycasts = visible;
            }
        }

        /// <summary>
        /// Set HUD opacity (e.g., dim during cutscenes).
        /// </summary>
        public void SetOpacity(float alpha)
        {
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        // ----- Internal -----

        private void LerpBars()
        {
            float dt = Time.unscaledDeltaTime * barLerpSpeed;

            if (healthBar != null)
            {
                healthBar.value = Mathf.Lerp(healthBar.value, targetHealth / 100f, dt);
                UpdateBarColor(healthFill, targetHealth, normalColor, warningColor, criticalColor);
                UpdateLabel(healthLabel, targetHealth, "{0:F0}%");
            }

            if (hungerBar != null)
            {
                hungerBar.value = Mathf.Lerp(hungerBar.value, targetHunger / 100f, dt);
                UpdateBarColor(hungerFill, targetHunger, normalColor, warningColor, criticalColor);
                UpdateLabel(hungerLabel, targetHunger, "{0:F0}%");
            }

            if (thirstBar != null)
            {
                thirstBar.value = Mathf.Lerp(thirstBar.value, targetThirst / 100f, dt);
                UpdateBarColor(thirstFill, targetThirst, normalColor, warningColor, criticalColor);
                UpdateLabel(thirstLabel, targetThirst, "{0:F0}%");
            }

            if (temperatureBar != null)
            {
                float tempNormalized = Mathf.InverseLerp(minTemperature, maxTemperature, targetTemperature);
                temperatureBar.value = Mathf.Lerp(temperatureBar.value, tempNormalized, dt);
                UpdateTemperatureColor(temperatureFill, targetTemperature);
                UpdateLabel(temperatureLabel, targetTemperature, "{0:F1}\u00B0C");
            }
        }

        private void UpdateBarColor(Image fill, float value, Color normal, Color warning, Color critical)
        {
            if (fill == null) return;

            if (value <= criticalThreshold)
                fill.color = critical;
            else if (value <= warningThreshold)
                fill.color = Color.Lerp(critical, warning, (value - criticalThreshold) / (warningThreshold - criticalThreshold));
            else
                fill.color = Color.Lerp(warning, normal, (value - warningThreshold) / (100f - warningThreshold));
        }

        private void UpdateTemperatureColor(Image fill, float temp)
        {
            if (fill == null) return;

            if (temp < normalTempLow)
            {
                // Cold - blue gradient
                float t = Mathf.InverseLerp(minTemperature, normalTempLow, temp);
                fill.color = Color.Lerp(criticalColor, coldColor, t);
            }
            else if (temp > normalTempHigh)
            {
                // Hot - orange/red gradient
                float t = Mathf.InverseLerp(normalTempHigh, maxTemperature, temp);
                fill.color = Color.Lerp(normalColor, hotColor, t);
            }
            else
            {
                // Normal range
                fill.color = normalColor;
            }
        }

        private void UpdateLabel(Text label, float value, string format)
        {
            if (label != null)
            {
                label.text = string.Format(format, value);
            }
        }

        private void PulseCriticalBars()
        {
            pulseTimer += Time.unscaledDeltaTime * criticalPulseSpeed;
            float pulse = (Mathf.Sin(pulseTimer * Mathf.PI * 2f) + 1f) * 0.5f; // 0-1 oscillation

            PulseBar(healthFill, targetHealth, pulse);
            PulseBar(hungerFill, targetHunger, pulse);
            PulseBar(thirstFill, targetThirst, pulse);
        }

        private void PulseBar(Image fill, float value, float pulse)
        {
            if (fill == null) return;
            if (value > criticalThreshold) return;

            // Modulate alpha for critical pulse effect
            Color c = fill.color;
            c.a = Mathf.Lerp(0.5f, 1f, pulse);
            fill.color = c;
        }

        private void SetBarsImmediate()
        {
            if (healthBar != null) healthBar.value = targetHealth / 100f;
            if (hungerBar != null) hungerBar.value = targetHunger / 100f;
            if (thirstBar != null) thirstBar.value = targetThirst / 100f;
            if (temperatureBar != null)
            {
                float tempNorm = Mathf.InverseLerp(minTemperature, maxTemperature, targetTemperature);
                temperatureBar.value = tempNorm;
            }
        }
    }
}
