using UnityEngine;

namespace SimCore
{
    // Applies simulated physics state (position, rotation) to this GameObject's transform
    // and drives the rotor spin animation. All flight dynamics live in Simulator.cs.
    public class HelicopterPlayer : MonoBehaviour
    {
        public Transform rotorTransform;
        public Transform tailRotorTransform;
        public float maxRotorRPM     = 600f;
        public float tailRotorRPM    = 2400f;

        private SimulatorDriver _driver;

        void Start()
        {
            _driver = FindObjectOfType<SimulatorDriver>();
        }

        void Update()
        {
            if (_driver == null)
            {
                _driver = FindObjectOfType<SimulatorDriver>();
                if (_driver == null) return;
            }
             

            if (rotorTransform != null)
            {
                float degPerSec = maxRotorRPM * 6f;
                rotorTransform.Rotate(Vector3.up, degPerSec * Time.deltaTime, Space.Self);
            }

            if (tailRotorTransform != null)
            {
                float degPerSec = tailRotorRPM * 6f;
                tailRotorTransform.Rotate(Vector3.right, degPerSec * Time.deltaTime, Space.Self);
            }
        }
    }
}
