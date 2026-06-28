using UnityEngine;

namespace SimCore
{
    public class SnowWashEffect : MonoBehaviour
    {
        [Header("Settings")]
        public bool  snowEnabled    = true;
        public float maxAglM        = 8f;   // above this height, no effect
        public float maxEmissionRate = 120f;

        private ParticleSystem _ps;
        private SimulatorDriver _driver;

        void Start()
        {
            _ps     = GetComponent<ParticleSystem>();
            _driver = FindObjectOfType<SimulatorDriver>();
        }

        void Update()
        {
            if (_ps == null || _driver == null || !snowEnabled)
            {
                SetEmission(0f);
                return;
            }

            Vector3 pos = _driver.LastBodyPosition;
            float aglM = maxAglM + 1f; // default: above threshold = no effect

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
