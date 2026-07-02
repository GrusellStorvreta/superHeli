using UnityEngine;

namespace SimCore
{
    public class SnowWashEffect : MonoBehaviour
    {
        [Header("Terrain — snow detection")]
        [Tooltip("Substring to match against terrain layer names (case-insensitive). Leave empty to use Snow Layer Index instead.")]
        public string snowLayerName  = "snow";
        [Tooltip("Fallback layer index if name matching finds nothing")]
        public int    snowLayerIndex = 0;
        [Tooltip("Minimum snow blend value (0–1) to enable the effect")]
        public float  snowThreshold  = 0.3f;

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
        private int             _resolvedLayerIndex = -1; // -1 = not yet resolved

        void Start()
        {
            _ps             = GetComponentInChildren<ParticleSystem>();
            _driver         = FindObjectOfType<SimulatorDriver>();
            _landingChecker = FindObjectOfType<LandingChecker>();
            if (_landingChecker != null) _landingChecker.OnLanding += OnLanding;

            // Disable emission immediately — Update controls rate from here
            if (_ps != null)
            {
                var emission = _ps.emission;
                emission.rateOverTime = 0f;
                _ps.Clear();
            }
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
            if (_ps == null || _driver == null) { SetEmission(0f); return; }

            Vector3 pos = _driver.LastBodyPosition;
            if (!IsOverSnow(pos)) { SetEmission(0f); return; }

            float aglM = maxAglM + 1f;
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, maxAglM + 50f))
                aglM = hit.distance;

            float t = 1f - Mathf.Clamp01(aglM / maxAglM);
            SetEmission(t * maxEmissionRate);
        }

        bool IsOverSnow(Vector3 worldPos)
        {
            foreach (var terrain in Terrain.activeTerrains)
            {
                Vector3 tp = terrain.transform.position;
                Vector3 ts = terrain.terrainData.size;
                if (worldPos.x < tp.x || worldPos.x > tp.x + ts.x ||
                    worldPos.z < tp.z || worldPos.z > tp.z + ts.z)
                    continue;

                TerrainData td = terrain.terrainData;
                int layerIdx = ResolveSnowLayer(td);
                if (layerIdx < 0 || layerIdx >= td.alphamapLayers) return false;

                Vector3 local = worldPos - tp;
                int mapX = Mathf.Clamp((int)(local.x / ts.x * td.alphamapWidth),  0, td.alphamapWidth  - 1);
                int mapZ = Mathf.Clamp((int)(local.z / ts.z * td.alphamapHeight), 0, td.alphamapHeight - 1);

                float[,,] alphas = td.GetAlphamaps(mapX, mapZ, 1, 1);
                return alphas[0, 0, layerIdx] > snowThreshold;
            }
            return false;
        }

        int ResolveSnowLayer(TerrainData td)
        {
            // Return cached result if already found
            if (_resolvedLayerIndex >= 0) return _resolvedLayerIndex;

            if (!string.IsNullOrEmpty(snowLayerName))
            {
                var layers = td.terrainLayers;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] != null &&
                        layers[i].name.IndexOf(snowLayerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _resolvedLayerIndex = i;
                        Debug.Log($"[SnowWashEffect] Found snow layer '{layers[i].name}' at index {i}");
                        return i;
                    }
                }
                Debug.LogWarning($"[SnowWashEffect] No terrain layer matching '{snowLayerName}' found — falling back to index {snowLayerIndex}");
            }

            _resolvedLayerIndex = snowLayerIndex;
            return _resolvedLayerIndex;
        }

        void SetEmission(float rate)
        {
            if (_ps == null) return;
            var emission = _ps.emission;
            emission.rateOverTime = rate;
        }
    }
}
