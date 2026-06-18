using UnityEngine;

namespace SimCore
{
    public struct LandingResult
    {
        public bool success;
        public string failReason;
    }

    public class LandingChecker : MonoBehaviour
    {
        [Header("Velocity limits")]
        public float maxVerticalSpeed = 1.5f;    // m/s
        public float maxHorizontalSpeed = 2.0f;  // m/s

        [Header("Orientation")]
        public float maxTiltAngle = 10f;         // degrees from upright

        [Header("Ground flatness")]
        public float sampleRadius = 3f;          // metres around contact point
        public int samplePoints = 8;             // points sampled around the circle
        public float maxHeightVariation = 0.5f;  // max allowed height difference in metres

        // Fired on every touchdown — subscribe to react to landing outcomes.
        public event System.Action<LandingResult> OnLanding;

        public LandingResult LastResult { get; private set; }

        private Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (rb == null) return;
            Vector3 contactPoint = collision.contacts[0].point;
            var result = CheckLanding(rb, contactPoint);
            LastResult = result;
            OnLanding?.Invoke(result);
            Debug.Log(result.success
                ? "[Landing] Lyckad landning!"
                : $"[Landing] Misslyckad: {result.failReason}");
        }

        // Call this manually if needed outside of collision events.
        public LandingResult CheckLanding(Rigidbody rb, Vector3 contactPoint)
        {
            float vertSpeed = Mathf.Abs(rb.velocity.y);
            if (vertSpeed > maxVerticalSpeed)
                return Fail($"För hög sjunkningshastighet: {vertSpeed:F1} m/s");

            float horizSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
            if (horizSpeed > maxHorizontalSpeed)
                return Fail($"För hög horisontell hastighet: {horizSpeed:F1} m/s");

            float tilt = Vector3.Angle(rb.transform.up, Vector3.up);
            if (tilt > maxTiltAngle)
                return Fail($"För stor lutning: {tilt:F1}°");

            if (!IsGroundFlat(contactPoint, out float variation))
                return Fail($"Ojämn mark: {variation:F1} m höjdskillnad");

            return new LandingResult { success = true };
        }

        private bool IsGroundFlat(Vector3 center, out float variation)
        {
            variation = 0f;
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return true;

            float min = float.MaxValue;
            float max = float.MinValue;

            // Sample center point + ring of points around it
            Sample(terrain, center, ref min, ref max);
            for (int i = 0; i < samplePoints; i++)
            {
                float angle = i * (360f / samplePoints) * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * sampleRadius;
                Sample(terrain, p, ref min, ref max);
            }

            variation = max - min;
            return variation <= maxHeightVariation;
        }

        private static void Sample(Terrain terrain, Vector3 pos, ref float min, ref float max)
        {
            float h = terrain.SampleHeight(pos);
            if (h < min) min = h;
            if (h > max) max = h;
        }

        private static LandingResult Fail(string reason) =>
            new LandingResult { success = false, failReason = reason };
    }
}
