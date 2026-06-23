using UnityEngine;

namespace SimCore
{
    [RequireComponent(typeof(Rigidbody))]
    public class WindForce : MonoBehaviour
    {
        [Tooltip("Wind push coefficient in N/(m/s). Match SimulatorConfig.linearDrag so wind and drag are symmetric.")]
        public float dragCoefficient = 150f;

        private Rigidbody _rb;
        private WindZone  _windZone;
        private float     _noiseOffset;

        void Awake()
        {
            _rb          = GetComponent<Rigidbody>();
            _windZone    = FindObjectOfType<WindZone>();
            _noiseOffset = Random.Range(0f, 1000f);

            if (_windZone == null)
                Debug.Log("[WindForce] No WindZone in scene — wind disabled.");
        }

        // Called by MissionManager after swapping levels so the cached reference stays valid.
        public void RefreshWindZone() => _windZone = FindObjectOfType<WindZone>();

        void FixedUpdate()
        {
            if (_rb == null || _windZone == null) return;

            Vector3 wind = ComputeWindVelocity();
            if (wind.sqrMagnitude < 0.0001f) return;

            // Push formula: windVelocity * coeff
            // Combined with Simulator's existing drag (-velocity * coeff) this equals
            // (windVelocity - velocity) * coeff — the correct aerodynamic apparent-wind force.
            _rb.AddForce(wind * dragCoefficient);
        }

        Vector3 ComputeWindVelocity()
        {
            float t = Time.time;

            // Smooth random turbulence via Perlin noise
            float turbulence = (Mathf.PerlinNoise(t * _windZone.windTurbulence + _noiseOffset, 0f)
                                * 2f - 1f)
                               * _windZone.windTurbulence;

            // Rhythmic gusts via sine pulse
            float pulse = Mathf.Sin(t * _windZone.windPulseFrequency * Mathf.PI * 2f)
                          * _windZone.windPulseMagnitude;

            float speed = _windZone.windMain + turbulence + pulse;

            // WindZone.transform.forward is the wind direction in world space.
            return _windZone.transform.forward * speed;
        }
    }
}
