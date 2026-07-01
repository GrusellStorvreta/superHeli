using UnityEngine;

namespace SimCore
{
    public class SnowWashEffect : MonoBehaviour
    {
        [Header("Terrain")]
        [Tooltip("Terrain layer index that counts as snow (check your Terrain's Paint Texture layers)")]
        public int   snowLayerIndex = 0;
        [Tooltip("Minimum snow blend value (0–1) to enable the effect")]
        public float snowThreshold  = 0.3f;

        [Header("Rotor wash — continuous near ground")]
        public float maxAglM         = 8f;
        public float maxEmissionRate = 120f;

        [Header("Landing burst")]
        public ParticleSystem landingBurst;
        public int            burstCount    = 120;
        public float          burstMinSpeed = 1f;
        public bool           burstOnCrash  = true;

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
            if (!result.success && !burstOnCrash) return;

            Vector3 pos = _driver != null ? _driver.LastBodyPosition : transform.position;
            if (!IsOverSnow(pos)) return;

            var ps = landingBurst != null ? landingBurst : _ps;
            if (ps == null) return;

            float impact = _driver != null ? Mathf.Abs(_driver.LastBodyVelocity.y) : burstMinSpeed;
            if (impact < burstMinSpeed) return;

            float scale = Mathf.Clamp01(impact / 5f);
            ps.Emit(Mathf.RoundToInt(burstCount * scale));
        }

        void Update()
        {
            if (_ps == null || _driver == null)
            {
                SetEmission(0f);
                return;
            }

            Vector3 pos = _driver.LastBodyPosition;

            if (!IsOverSnow(pos))
            {
                SetEmission(0f);
                return;
            }

            float aglM = maxAglM + 1f;
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, maxAglM + 50f))
                aglM = hit.distance;

            float t = 1f - Mathf.Clamp01(aglM / maxAglM);
            SetEmission(t * maxEmissionRate);
        }

        bool IsOverSnow(Vector3 worldPos)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return false;

            TerrainData td = terrain.terrainData;
            if (snowLayerIndex >= td.alphamapLayers) return false;

            Vector3 tp   = worldPos - terrain.transform.position;
            int mapX = Mathf.Clamp((int)(tp.x / td.size.x * td.alphamapWidth),  0, td.alphamapWidth  - 1);
            int mapZ = Mathf.Clamp((int)(tp.z / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);

            float[,,] alphas = td.GetAlphamaps(mapX, mapZ, 1, 1);
            return alphas[0, 0, snowLayerIndex] > snowThreshold;
        }

        void SetEmission(float rate)
        {
            if (_ps == null) return;
            var emission = _ps.emission;
            emission.rateOverTime = rate;
        }
    }
}
