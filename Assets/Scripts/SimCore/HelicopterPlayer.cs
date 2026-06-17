using UnityEngine;

namespace SimCore
{
    [RequireComponent(typeof(Rigidbody))]
    // Applies simulated physics state (position, rotation) to this GameObject's transform
    // and drives the rotor spin animation. All flight dynamics live in Simulator.cs.
    public class HelicopterPlayer : MonoBehaviour
    {
        public Transform rotorTransform;
        public float maxRotorRPM = 600f;

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
                float collective = Mathf.Clamp01((float)_driver.LastBufferedControl.collective);
                float degPerSec  = collective * maxRotorRPM * 6f; // rpm → deg/s
                rotorTransform.Rotate(Vector3.up, degPerSec * Time.deltaTime, Space.Self);
            }
        }
    }
}
