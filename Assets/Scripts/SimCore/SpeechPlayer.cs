using System.Collections;
using UnityEngine;

namespace SimCore
{
    [System.Serializable]
    public class SpeechEntry
    {
        public string      token;
        public AudioClip[] clips;
    }

    public class SpeechPlayer : MonoBehaviour
    {
        [Header("Speech")]
        public AudioSource   audioSource;
        public SpeechEntry[] entries;

        [Header("Radio crackle")]
        public AudioSource  crackleSource;
        public AudioClip[]  crackles;
        [Range(0f, 1f)]
        public float        crackleVolume   = 0.3f;
        public Vector2      crackleInterval = new Vector2(0.3f, 1f);

        [Header("Radio filter")]
        public float        highPassCutoff   = 1500f;
        [Range(0f, 1f)]
        public float        baseDistortion   = 0.1f;
        [Range(0f, 1f)]
        public float        maxDistortion    = 0.4f;

        private AudioHighPassFilter   _highPass;
        private AudioDistortionFilter _distortion;
        private float[]               _samples = new float[256];
        private Coroutine             _crackleRoutine;

        void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            _highPass                = audioSource.gameObject.AddComponent<AudioHighPassFilter>();
            _highPass.cutoffFrequency = highPassCutoff;

            _distortion               = audioSource.gameObject.AddComponent<AudioDistortionFilter>();
            _distortion.distortionLevel = baseDistortion;
        }

        void Update()
        {
            if (!audioSource.isPlaying) return;

            audioSource.GetOutputData(_samples, 0);
            float sum = 0f;
            foreach (float s in _samples) sum += Mathf.Abs(s);
            float vol = sum / _samples.Length;

            _distortion.distortionLevel = Mathf.Lerp(baseDistortion, maxDistortion, vol * 10f);
        }

        public void Say(string token)
        {
            if (audioSource == null) return;

            foreach (var e in entries)
            {
                if (e.token != token || e.clips == null || e.clips.Length == 0) continue;
                var clip = e.clips[Random.Range(0, e.clips.Length)];
                if (clip == null) return;

                audioSource.PlayOneShot(clip);

                if (crackleSource != null && crackles != null && crackles.Length > 0 && Random.value < 0.3f)
                {
                    if (_crackleRoutine != null) StopCoroutine(_crackleRoutine);
                    _crackleRoutine = StartCoroutine(PlayCrackles(clip.length));
                }
                return;
            }
        }

        IEnumerator PlayCrackles(float duration)
        {
            Crackle(); // transmission start

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float wait = Random.Range(crackleInterval.x, crackleInterval.y);
                yield return new WaitForSeconds(wait);
                elapsed += wait;
                if (elapsed < duration) Crackle();
            }

            _crackleRoutine = null;
        }

        void Crackle()
        {
            var clip = crackles[Random.Range(0, crackles.Length)];
            if (clip != null) crackleSource.PlayOneShot(clip, crackleVolume);
        }
    }
}
