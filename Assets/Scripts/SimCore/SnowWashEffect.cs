using UnityEngine;

namespace SimCore
{
    public class SnowWashEffect : MonoBehaviour
    {
        [Header("Rotor wash — continuous near ground")]
        public bool  snowEnabled     = true;
        public float maxAglM         = 8f;
        public float maxEmissionRate = 120f;

        [Header("Landing burst")]
        public ParticleSystem landingBurst;
        public int            burstCount    = 120;
        public float          burstMinSpeed = 1f;   // m/s vertical impact to trigger
        public bool           burstOnCrash  = true; // also burst on hard/failed landings

        private ParticleSystem  _ps;
        private SimulatorDriver _driver;
        private LandingChecker  _landingChecker;

        void Start()
        {
            _ps             = GetComponent<ParticleSystem>();
            _driver         = FindObjectOfType<SimulatorDriver>();
            _landingChecker = FindObjectOfType<LandingChecker>();
            if (_landingChecker != null) _landingChecker.OnLanding += OnLanding;
        }

        void OnDestroy()
        {
            if (_landingChecker != null) _landingChecker.OnLanding -= OnLanding;
        }

        void OnLanding(LandingResult result)
        {
            if (!snowEnabled) return;
            if (!result.success && !burstOnCrash) return;

            var ps = landingBurst != null ? landingBurst : _ps;
            if (ps == null) return;

            // Scale burst to impact speed — more snow for harder landings
            float impact = _driver != null ? Mathf.Abs(_driver.LastBodyVelocity.y) : burstMinSpeed;
            if (impact < burstMinSpeed) return;

            float scale = Mathf.Clamp01(impact / 5f); // full burst at 5 m/s
            ps.Emit(Mathf.RoundToInt(burstCount * scale));
        }

        void Update()
        {
            if (_ps == null || _driver == null || !snowEnabled)
            {
                SetEmission(0f);
                return;
            }

            Vector3 pos = _driver.LastBodyPosition;
            float aglM = maxAglM + 1f;

            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, maxAglM + 50f))
                aglM = hit.distance;

            float t = 1f - Mathf.Clamp01(aglM / maxAglM);
            SetEmission(t * maxEmissionRate);
        }

        void SetEmission(float rate)
        {
            if (_ps == null) return;
            var emission = _ps.emission;
            emission.rateOverTime = rate;
        }
    }
}
