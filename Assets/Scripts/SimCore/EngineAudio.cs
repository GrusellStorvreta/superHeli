using UnityEngine;

namespace SimCore
{
    public class EngineAudio : MonoBehaviour
    {
        [Header("Audio sources")]
        public AudioSource exteriorAudio;
        public AudioSource interiorAudio;

        [Header("Pitch")]
        public float pitchMin = 0.85f;
        public float pitchMax = 1.15f;

        [Header("Volume")]
        public float volumeMin = 0.6f;
        public float volumeMax = 1.0f;

        [Header("Response")]
        [Tooltip("Smoothing time in seconds — higher = slower response")]
        public float smoothingTime = 0.5f;

        private SimulatorDriver _driver;
        private float           _smoothedCollective;

        void Start()
        {
            _driver = FindObjectOfType<SimulatorDriver>();
            _smoothedCollective = 0.5f;
        }

        void Update()
        {
            if (_driver == null) { _driver = FindObjectOfType<SimulatorDriver>(); return; }

            float target = (float)_driver.LastBufferedControl.collective;
            float alpha  = Time.deltaTime / (smoothingTime + Time.deltaTime);
            _smoothedCollective += alpha * (target - _smoothedCollective);

            float pitch  = Mathf.Lerp(pitchMin,  pitchMax,  _smoothedCollective);
            float volume = Mathf.Lerp(volumeMin, volumeMax, _smoothedCollective);

            Apply(exteriorAudio, pitch, volume);
            Apply(interiorAudio, pitch, volume);
        }

        static void Apply(AudioSource src, float pitch, float volume)
        {
            if (src == null || src.mute) return;
            src.pitch  = pitch;
            src.volume = volume;
        }
    }
}
