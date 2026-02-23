// EmotionDetector.cs
// CYBERNOMAD -- Classifies player emotion from OVRFaceExpressions blendshapes.
// Emotions: Fear, Surprise, Anger, Joy, Neutral.
// Fires OnEmotionChanged(Emotion) event when dominant emotion changes.
// Useful for gameplay: NPC can react to player's real facial expression.
//
// Requires: com.meta.xr.sdk.core (auto-detected via HAS_META_XR define)
//
// Usage:
//   var detector = gameObject.AddComponent<EmotionDetector>();
//   detector.OnEmotionChanged += e => npc.ReactTo(e);

using System;
using UnityEngine;

namespace Plaga44.FaceTracking
{
    /// <summary>
    /// Detected emotion category derived from facial blendshape analysis.
    /// </summary>
    public enum Emotion
    {
        Neutral,
        Fear,
        Surprise,
        Anger,
        Joy
    }

    /// <summary>
    /// Analyzes facial blendshape weights from FaceTrackingManager and classifies
    /// the player's dominant emotion. Fires OnEmotionChanged when the emotion changes.
    /// </summary>
    public class EmotionDetector : MonoBehaviour
    {
        private const string LOG = "[EmotionDetector]";

        // ── Inspector ────────────────────────────────────────────────────

        [Tooltip("Minimum blendshape weight sum for an emotion to override Neutral.")]
        [Range(0.05f, 1.0f)]
        public float EmotionThreshold = 0.15f;

        [Tooltip("How many seconds an emotion must be dominant before firing the event.")]
        [Range(0f, 2f)]
        public float HysteresisTime = 0.25f;

        // ── Events ───────────────────────────────────────────────────────

        /// <summary>
        /// Fired whenever the dominant emotion changes. Passes the new emotion.
        /// </summary>
        public event Action<Emotion> OnEmotionChanged;

        // ── Public state ─────────────────────────────────────────────────

        /// <summary>Current confirmed dominant emotion.</summary>
        public Emotion CurrentEmotion { get; private set; } = Emotion.Neutral;

        /// <summary>Score of the currently confirmed emotion [0..1] range.</summary>
        public float CurrentScore { get; private set; }

        // ── Private state ────────────────────────────────────────────────

        private Emotion _candidateEmotion = Emotion.Neutral;
        private float _candidateTimer;

        // ── Scoring table ────────────────────────────────────────────────
        //
        // Each emotion is scored as a weighted sum of relevant blendshapes.
        // Weights reflect how strongly each AU (Action Unit) contributes.
        //
        // Reference mapping (OVR AU -> emotion):
        //   FEAR:     BrowLowererL/R + LidTightenerL/R + UpperLidRaiserL/R + LipStretcherL/R
        //   SURPRISE: BrowRaiserInnerL/R + UpperLidRaiserL/R + JawOpen
        //   ANGER:    BrowLowererL/R + NoseWrinklerL/R + LipCornerPullerL/R + UpperLipRaiserL/R
        //   JOY:      LipCornerPullerL/R + CheekRaiserL/R + OrbicularisOrisL/R
        //
#if HAS_META_XR
        private static readonly (OVRFaceExpressions.FaceExpression expr, Emotion emotion, float weight)[]
            ScoringTable = new[]
        {
            // ── FEAR ──────────────────────────────────────────────────
            (OVRFaceExpressions.FaceExpression.BrowLowererL,       Emotion.Fear,     0.6f),
            (OVRFaceExpressions.FaceExpression.BrowLowererR,       Emotion.Fear,     0.6f),
            (OVRFaceExpressions.FaceExpression.LidTightenerL,      Emotion.Fear,     0.5f),
            (OVRFaceExpressions.FaceExpression.LidTightenerR,      Emotion.Fear,     0.5f),
            (OVRFaceExpressions.FaceExpression.UpperLidRaiserL,    Emotion.Fear,     0.7f),
            (OVRFaceExpressions.FaceExpression.UpperLidRaiserR,    Emotion.Fear,     0.7f),
            (OVRFaceExpressions.FaceExpression.LipStretcherL,      Emotion.Fear,     0.4f),
            (OVRFaceExpressions.FaceExpression.LipStretcherR,      Emotion.Fear,     0.4f),

            // ── SURPRISE ──────────────────────────────────────────────
            (OVRFaceExpressions.FaceExpression.InnerBrowRaiserL,   Emotion.Surprise, 0.8f),
            (OVRFaceExpressions.FaceExpression.InnerBrowRaiserR,   Emotion.Surprise, 0.8f),
            (OVRFaceExpressions.FaceExpression.OuterBrowRaiserL,   Emotion.Surprise, 0.6f),
            (OVRFaceExpressions.FaceExpression.OuterBrowRaiserR,   Emotion.Surprise, 0.6f),
            (OVRFaceExpressions.FaceExpression.UpperLidRaiserL,    Emotion.Surprise, 0.5f),
            (OVRFaceExpressions.FaceExpression.UpperLidRaiserR,    Emotion.Surprise, 0.5f),
            (OVRFaceExpressions.FaceExpression.JawOpen,            Emotion.Surprise, 0.9f),

            // ── ANGER ────────────────────────────────────────────────
            (OVRFaceExpressions.FaceExpression.BrowLowererL,       Emotion.Anger,    0.8f),
            (OVRFaceExpressions.FaceExpression.BrowLowererR,       Emotion.Anger,    0.8f),
            (OVRFaceExpressions.FaceExpression.NoseWrinklerL,      Emotion.Anger,    0.7f),
            (OVRFaceExpressions.FaceExpression.NoseWrinklerR,      Emotion.Anger,    0.7f),
            (OVRFaceExpressions.FaceExpression.LipCornerPullerL,   Emotion.Anger,    0.3f),
            (OVRFaceExpressions.FaceExpression.LipCornerPullerR,   Emotion.Anger,    0.3f),
            (OVRFaceExpressions.FaceExpression.UpperLipRaiserL,    Emotion.Anger,    0.5f),
            (OVRFaceExpressions.FaceExpression.UpperLipRaiserR,    Emotion.Anger,    0.5f),
            (OVRFaceExpressions.FaceExpression.LidTightenerL,      Emotion.Anger,    0.4f),
            (OVRFaceExpressions.FaceExpression.LidTightenerR,      Emotion.Anger,    0.4f),

            // ── JOY ──────────────────────────────────────────────────
            (OVRFaceExpressions.FaceExpression.LipCornerPullerL,   Emotion.Joy,      0.9f),
            (OVRFaceExpressions.FaceExpression.LipCornerPullerR,   Emotion.Joy,      0.9f),
            (OVRFaceExpressions.FaceExpression.CheekRaiserL,       Emotion.Joy,      0.8f),
            (OVRFaceExpressions.FaceExpression.CheekRaiserR,       Emotion.Joy,      0.8f),
            (OVRFaceExpressions.FaceExpression.LipCornerDepressorL,Emotion.Joy,     -0.3f), // negative: suppresses joy if corners down
            (OVRFaceExpressions.FaceExpression.LipCornerDepressorR,Emotion.Joy,     -0.3f),
        };
#endif

        // Max possible score per emotion -- used to normalise into [0..1]
        private static readonly float[] EmotionMaxScores = new float[(int)Emotion.Joy + 1];

        // ── Static init ──────────────────────────────────────────────────

        static EmotionDetector()
        {
#if HAS_META_XR
            // Accumulate max positive weights per emotion for normalization
            foreach (var (_, emotion, weight) in ScoringTable)
            {
                if (weight > 0f)
                    EmotionMaxScores[(int)emotion] += weight;
            }
#endif
        }

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Update()
        {
            var manager = FaceTrackingManager.Instance;
            if (manager == null || !manager.IsTracking)
            {
                TrySetEmotion(Emotion.Neutral, 0f);
                return;
            }

            Emotion dominant;
            float score;
            AnalyzeBlendshapes(manager, out dominant, out score);
            TrySetEmotion(dominant, score);
        }

        // ── Analysis ─────────────────────────────────────────────────────

        private void AnalyzeBlendshapes(FaceTrackingManager manager,
            out Emotion dominant, out float score)
        {
#if HAS_META_XR
            // Accumulate weighted scores per emotion
            float fearScore     = 0f;
            float surpriseScore = 0f;
            float angerScore    = 0f;
            float joyScore      = 0f;

            foreach (var (expr, emotion, weight) in ScoringTable)
            {
                float w = manager.GetExpression(expr) * weight;
                switch (emotion)
                {
                    case Emotion.Fear:     fearScore     += w; break;
                    case Emotion.Surprise: surpriseScore += w; break;
                    case Emotion.Anger:    angerScore    += w; break;
                    case Emotion.Joy:      joyScore      += w; break;
                }
            }

            // Clamp negatives to zero
            fearScore     = Mathf.Max(0f, fearScore);
            surpriseScore = Mathf.Max(0f, surpriseScore);
            angerScore    = Mathf.Max(0f, angerScore);
            joyScore      = Mathf.Max(0f, joyScore);

            // Normalize by max possible score
            float maxFear     = EmotionMaxScores[(int)Emotion.Fear];
            float maxSurprise = EmotionMaxScores[(int)Emotion.Surprise];
            float maxAnger    = EmotionMaxScores[(int)Emotion.Anger];
            float maxJoy      = EmotionMaxScores[(int)Emotion.Joy];

            if (maxFear     > 0f) fearScore     /= maxFear;
            if (maxSurprise > 0f) surpriseScore /= maxSurprise;
            if (maxAnger    > 0f) angerScore    /= maxAnger;
            if (maxJoy      > 0f) joyScore      /= maxJoy;

            // Find winner
            float best = EmotionThreshold;
            dominant = Emotion.Neutral;
            score = 0f;

            if (fearScore > best)     { best = fearScore;     dominant = Emotion.Fear;     score = fearScore; }
            if (surpriseScore > best) { best = surpriseScore; dominant = Emotion.Surprise; score = surpriseScore; }
            if (angerScore > best)    { best = angerScore;    dominant = Emotion.Anger;    score = angerScore; }
            if (joyScore > best)      { best = joyScore;      dominant = Emotion.Joy;      score = joyScore; }
#else
            dominant = Emotion.Neutral;
            score = 0f;
#endif
        }

        // ── Hysteresis / state change ────────────────────────────────────

        private void TrySetEmotion(Emotion candidate, float score)
        {
            if (candidate == _candidateEmotion)
            {
                _candidateTimer += Time.deltaTime;
                if (_candidateTimer >= HysteresisTime && candidate != CurrentEmotion)
                {
                    CurrentEmotion = candidate;
                    CurrentScore   = score;
                    OnEmotionChanged?.Invoke(CurrentEmotion);
                    Debug.Log($"{LOG} Emotion: {CurrentEmotion} ({CurrentScore:F2})");
                }
            }
            else
            {
                _candidateEmotion = candidate;
                _candidateTimer   = 0f;
            }
        }
    }
}
