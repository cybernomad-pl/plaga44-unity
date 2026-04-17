// VolumeConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: DefaultVolumeProfile (globalny post-processing URP)
//
// Public API:
//   VolumeConfig.Apply(VolumeConfig.INITIAL);
//   VolumeConfig.SetBloom(0.3f);
//   VolumeConfig.SetVignette(0.2f);
//   VolumeConfig.LogCurrent();
//
// Volume Profile zawiera efekty post-processingu:
//   Bloom, Tonemapping, Vignette, ColorAdjustments, MotionBlur,
//   DepthOfField, ChromaticAberration, FilmGrain, LensDistortion, etc.

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Plaga44.Editor
{
    public struct VolumeSettings
    {
        // Bloom
        public float bloomIntensity;        // 0 = off
        public float bloomThreshold;        // 0.9 default
        public float bloomScatter;          // 0.7 default

        // Tonemapping
        public int tonemappingMode;         // 0=None, 1=Neutral, 2=ACES

        // Vignette
        public float vignetteIntensity;     // 0 = off
        public float vignetteSmoothness;

        // Color Adjustments
        public float postExposure;
        public float contrast;              // -100 to 100
        public float saturation;            // -100 to 100

        // Motion Blur (NIE w VR -- powoduje motion sickness)
        public float motionBlurIntensity;   // 0 = off

        // Chromatic Aberration
        public float chromaticAberration;   // 0 = off

        // Film Grain
        public float filmGrainIntensity;    // 0 = off

        // Depth of Field
        public int dofMode;                 // 0=Off, 1=Gaussian, 2=Bokeh
    }

    public static class VolumeConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string PROFILE_PATH = "Assets/Settings/DefaultVolumeProfile.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly VolumeSettings INITIAL = new VolumeSettings
        {
            bloomIntensity       = 0f,      // off na Quest
            bloomThreshold       = 0.9f,
            bloomScatter         = 0.7f,
            tonemappingMode      = 0,       // None (HDR off wiec niepotrzebne)
            vignetteIntensity    = 0f,      // off (locomotion vignette osobno)
            vignetteSmoothness   = 0.2f,
            postExposure         = 0f,
            contrast             = 0f,
            saturation           = 0f,
            motionBlurIntensity  = 0f,      // NIGDY w VR
            chromaticAberration  = 0f,      // off
            filmGrainIntensity   = 0f,      // off
            dofMode              = 0,       // off
        };

        public static readonly VolumeSettings CINEMATIC = new VolumeSettings
        {
            bloomIntensity       = 0.3f,
            bloomThreshold       = 0.9f,
            bloomScatter         = 0.7f,
            tonemappingMode      = 2,       // ACES
            vignetteIntensity    = 0.25f,
            vignetteSmoothness   = 0.4f,
            postExposure         = 0.5f,
            contrast             = 10f,
            saturation           = 10f,
            motionBlurIntensity  = 0f,      // NIGDY w VR
            chromaticAberration  = 0.05f,
            filmGrainIntensity   = 0.1f,
            dofMode              = 0,
        };

        // ---------------------------------------------------------------------
        // Apply all
        // ---------------------------------------------------------------------

        public static bool Apply(VolumeSettings s)
        {
            var profile = LoadProfile();
            if (profile == null) return false;

            SetEffect<Bloom>(profile, b => {
                b.intensity.Override(s.bloomIntensity);
                b.threshold.Override(s.bloomThreshold);
                b.scatter.Override(s.bloomScatter);
            });

            SetEffect<Tonemapping>(profile, t => {
                t.mode.Override((TonemappingMode)s.tonemappingMode);
            });

            SetEffect<Vignette>(profile, v => {
                v.intensity.Override(s.vignetteIntensity);
                v.smoothness.Override(s.vignetteSmoothness);
            });

            SetEffect<ColorAdjustments>(profile, c => {
                c.postExposure.Override(s.postExposure);
                c.contrast.Override(s.contrast);
                c.saturation.Override(s.saturation);
            });

            SetEffect<MotionBlur>(profile, m => {
                m.intensity.Override(s.motionBlurIntensity);
            });

            SetEffect<ChromaticAberration>(profile, c => {
                c.intensity.Override(s.chromaticAberration);
            });

            SetEffect<FilmGrain>(profile, f => {
                f.intensity.Override(s.filmGrainIntensity);
            });

            SetEffect<DepthOfField>(profile, d => {
                d.mode.Override((DepthOfFieldMode)s.dofMode);
            });

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LOG} Volume applied: bloom={s.bloomIntensity} tonemap={s.tonemappingMode} " +
                      $"vignette={s.vignetteIntensity} exposure={s.postExposure} " +
                      $"contrast={s.contrast} motionBlur={s.motionBlurIntensity}");
            return true;
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetBloom(float intensity) =>
            TweakEffect<Bloom>(b => b.intensity.Override(intensity), $"bloom={intensity}");

        public static void SetBloomThreshold(float v) =>
            TweakEffect<Bloom>(b => b.threshold.Override(v), $"bloomThreshold={v}");

        public static void SetTonemapping(int mode) =>
            TweakEffect<Tonemapping>(t => t.mode.Override((TonemappingMode)mode), $"tonemap={mode}");

        public static void SetVignette(float intensity) =>
            TweakEffect<Vignette>(v => v.intensity.Override(intensity), $"vignette={intensity}");

        public static void SetExposure(float v) =>
            TweakEffect<ColorAdjustments>(c => c.postExposure.Override(v), $"exposure={v}");

        public static void SetContrast(float v) =>
            TweakEffect<ColorAdjustments>(c => c.contrast.Override(v), $"contrast={v}");

        public static void SetSaturation(float v) =>
            TweakEffect<ColorAdjustments>(c => c.saturation.Override(v), $"saturation={v}");

        public static void SetMotionBlur(float intensity) =>
            TweakEffect<MotionBlur>(m => m.intensity.Override(intensity), $"motionBlur={intensity}");

        public static void SetChromaticAberration(float v) =>
            TweakEffect<ChromaticAberration>(c => c.intensity.Override(v), $"chromatic={v}");

        public static void SetFilmGrain(float v) =>
            TweakEffect<FilmGrain>(f => f.intensity.Override(v), $"filmGrain={v}");

        public static void SetDoF(int mode) =>
            TweakEffect<DepthOfField>(d => d.mode.Override((DepthOfFieldMode)mode), $"dof={mode}");

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var profile = LoadProfile();
            if (profile == null) return;

            string info = $"{LOG} Volume:";
            if (profile.TryGet<Bloom>(out var b))
                info += $" bloom={b.intensity.value}";
            if (profile.TryGet<Tonemapping>(out var t))
                info += $" tonemap={t.mode.value}";
            if (profile.TryGet<Vignette>(out var v))
                info += $" vignette={v.intensity.value}";
            if (profile.TryGet<ColorAdjustments>(out var c))
                info += $" exposure={c.postExposure.value} contrast={c.contrast.value} sat={c.saturation.value}";
            if (profile.TryGet<MotionBlur>(out var m))
                info += $" motionBlur={m.intensity.value}";

            Debug.Log(info);
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Config/Volume/Apply INITIAL (all off)", false, 1)]
        static void MenuInitial() => Apply(INITIAL);

        [MenuItem("CYBERNOMAD/Config/Volume/Apply CINEMATIC", false, 2)]
        static void MenuCinematic() => Apply(CINEMATIC);

        [MenuItem("CYBERNOMAD/Config/Volume/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static VolumeProfile LoadProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);
            if (profile == null) Debug.LogError($"{LOG} VolumeProfile not found: {PROFILE_PATH}");
            return profile;
        }

        static void SetEffect<T>(VolumeProfile profile, System.Action<T> configure) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var effect))
            {
                effect = profile.Add<T>(overrides: false);
            }
            configure(effect);
        }

        static void TweakEffect<T>(System.Action<T> configure, string label) where T : VolumeComponent
        {
            var profile = LoadProfile();
            if (profile == null) return;
            SetEffect<T>(profile, configure);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} Volume tweak: {label}");
        }
    }
}
