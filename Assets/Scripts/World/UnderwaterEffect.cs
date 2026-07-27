// =============================================================================
// UnderwaterEffect.cs
// PLAGA '44 -- efekt zanurzenia. Gdy glowa gracza (CenterEyeAnchor) zejdzie
// PONIZEJ poziomu wody, przed oczami pojawia sie "kopula" metnego turkusowego
// dymu (ParticleSystem wokol glowy) + underwater fog + petla dzwieku.
// Metnosc (alpha czastek + gestosc mgly + glosnosc) ROSNIE Z GLEBOKOSCIA:
// przy powierzchni ledwo widoczna, na dnie zalanej mapy najgestsza.
//
// Poziom wody: auto-detekcja z tafli "3D_Water" w scenie (bierze NAJWYZSZA Y).
// Gdy brak tafli -> uzywa serializowanego WaterLevel + LogWarning (NIE zgaduje
// innej wartosci -- caller/inspector decyduje).
//
// Buduje sie editor-time przez UnderwaterSetup (krok Bootstrap), ktory wiruje
// particleMaterial + underwaterClip. Runtime tylko wykonuje. ZERO fallbackow:
// brak glowy/materialu -> LogError + wylaczenie komponentu.
// =============================================================================

using UnityEngine;

namespace Plaga44.World
{
    [DisallowMultipleComponent]
    public class UnderwaterEffect : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Underwater]";
        private const string OvrRigName = "OVRCameraRig";
        private const string HeadPath = "TrackingSpace/CenterEyeAnchor";
        private const string WaterNameFragment = "3D_Water";

        [Header("Poziom wody")]
        [Tooltip("Fallback gdy w scenie NIE ma tafli 3D_Water. Gdy sa -- nadpisany najwyzsza Y tafli.")]
        [SerializeField] private float waterLevel = 16.8f;
        [Tooltip("Glebokosc (m) ponizej tafli, przy ktorej metnosc osiaga maksimum.")]
        [SerializeField] private float maxDepth = 10f;

        [Header("Wyglad metnej wody")]
        [Tooltip("Kolor zawiesiny -- metny turkus.")]
        [SerializeField] private Color murkColor = new Color(0.10f, 0.38f, 0.42f, 1f);
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.18f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.85f;
        [SerializeField] private float minFogDensity = 0.02f;
        [SerializeField] private float maxFogDensity = 0.14f;

        [Header("Zaleznosci (wiruje UnderwaterSetup)")]
        [SerializeField] private Material particleMaterial;
        [SerializeField] private AudioClip underwaterClip;

        // Runtime
        private Transform _head;
        private ParticleSystem _dome;
        private AudioSource _audio;
        private bool _submerged;

        // Zapamietany globalny stan mgly (przywracany po wynurzeniu).
        private bool _origFog;
        private Color _origFogColor;
        private FogMode _origFogMode;
        private float _origFogDensity;

        private void Start()
        {
            _head = ResolveHead();
            if (_head == null)
            {
                Debug.LogError($"{LOG} Brak glowy gracza ({OvrRigName}/{HeadPath} ani Camera.main) -- efekt wylaczony.");
                enabled = false;
                return;
            }
            if (particleMaterial == null)
            {
                Debug.LogError($"{LOG} Brak particleMaterial (nie zwirowany przez UnderwaterSetup) -- efekt wylaczony.");
                enabled = false;
                return;
            }

            DetectWaterLevel();
            CacheFog();
            BuildDome();
            BuildAudio();
        }

        private Transform ResolveHead()
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig != null)
            {
                var h = rig.transform.Find(HeadPath);
                if (h != null) return h;
            }
            return Camera.main != null ? Camera.main.transform : null;
        }

        // Poziom wody = NAJWYZSZA Y sposrod tafli "3D_Water" w scenie.
        // Zalana mapa: gracz jest "pod woda" gdy zejdzie ponizej najwyzszej tafli.
        private void DetectWaterLevel()
        {
            float found = float.NegativeInfinity;
            int count = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.Contains(WaterNameFragment))
                {
                    if (t.position.y > found) found = t.position.y;
                    count++;
                }
            }
            if (count > 0)
            {
                waterLevel = found;
                Debug.Log($"{LOG} Poziom wody wykryty z {count} tafli -> Y={waterLevel:F2}");
            }
            else
            {
                Debug.LogWarning($"{LOG} Brak tafli '{WaterNameFragment}' w scenie -- uzywam serializowanego waterLevel={waterLevel:F2}.");
            }
        }

        private void CacheFog()
        {
            _origFog = RenderSettings.fog;
            _origFogColor = RenderSettings.fogColor;
            _origFogMode = RenderSettings.fogMode;
            _origFogDensity = RenderSettings.fogDensity;
        }

        // Kopula czastek wokol glowy -- child CenterEyeAnchor, local space (podaza za glowa).
        private void BuildDome()
        {
            var go = new GameObject("_UnderwaterDome");
            go.transform.SetParent(_head, false);
            go.transform.localPosition = Vector3.zero;

            _dome = go.AddComponent<ParticleSystem>();
            _dome.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _dome.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // czastki trzymaja sie glowy
            main.startLifetime = 2.2f;
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
            main.startColor = WithAlpha(murkColor, minAlpha);
            main.maxParticles = 90;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = _dome.emission;
            emission.rateOverTime = 22f;

            var shape = _dome.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.55f; // tuz wokol glowy -> "kopula przed oczami"

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = particleMaterial;
            renderer.sortingFudge = -10f; // rysuj blisko kamery
        }

        private void BuildAudio()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = underwaterClip;   // moze byc null -> po prostu bez dzwieku (nie crashuje)
            _audio.loop = true;
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;       // 2D -- zawsze w uszach gracza
            _audio.volume = 0f;
            if (underwaterClip == null)
                Debug.LogWarning($"{LOG} Brak underwaterClip -- efekt bez dzwieku.");
        }

        private void Update()
        {
            if (_head == null || _dome == null) return;

            float depth = waterLevel - _head.position.y;
            bool sub = depth > 0f;

            if (sub != _submerged)
            {
                _submerged = sub;
                if (sub) EnterWater();
                else ExitWater();
            }

            if (sub) UpdateMurk(depth);
        }

        private void EnterWater()
        {
            _dome.Play();
            if (underwaterClip != null) _audio.Play();
        }

        private void ExitWater()
        {
            _dome.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (underwaterClip != null) _audio.Stop();
            // Przywroc globalna mgle do stanu sprzed zanurzenia.
            RenderSettings.fog = _origFog;
            RenderSettings.fogColor = _origFogColor;
            RenderSettings.fogMode = _origFogMode;
            RenderSettings.fogDensity = _origFogDensity;
        }

        // Metnosc rosnie z glebokoscia: t=0 przy powierzchni, t=1 na maxDepth.
        private void UpdateMurk(float depth)
        {
            float t = Mathf.Clamp01(depth / Mathf.Max(0.01f, maxDepth));
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

            var main = _dome.main;
            main.startColor = WithAlpha(murkColor, alpha);

            var emission = _dome.emission;
            emission.rateOverTime = Mathf.Lerp(16f, 42f, t);

            if (underwaterClip != null)
                _audio.volume = Mathf.Lerp(0.3f, 1f, t);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(murkColor.r, murkColor.g, murkColor.b, 1f);
            RenderSettings.fogDensity = Mathf.Lerp(minFogDensity, maxFogDensity, t);
        }

        private void OnDisable()
        {
            // Bezpieczenstwo: nie zostawiaj globalnej mgly wlaczonej gdy komponent gasnie pod woda.
            if (_submerged)
            {
                _submerged = false;
                RenderSettings.fog = _origFog;
                RenderSettings.fogColor = _origFogColor;
                RenderSettings.fogMode = _origFogMode;
                RenderSettings.fogDensity = _origFogDensity;
            }
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
