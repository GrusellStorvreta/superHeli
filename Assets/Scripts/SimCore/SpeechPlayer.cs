using System.Collections;
using System.Collections.Generic;
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

        public bool IsSpeaking { get; private set; }

        private AudioHighPassFilter          _highPass;
        private AudioDistortionFilter        _distortion;
        private float[]                      _samples = new float[256];
        private Coroutine                    _crackleRoutine;
        private Coroutine                    _speakingRoutine;
        private Dictionary<string, List<int>> _pools = new Dictionary<string, List<int>>();

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
                var clip = NextClip(e);
                if (clip == null) return;

                audioSource.PlayOneShot(clip);

                if (_speakingRoutine != null) StopCoroutine(_speakingRoutine);
                _speakingRoutine = StartCoroutine(TrackSpeaking(clip.length));

                if (crackleSource != null && crackles != null && crackles.Length > 0 && Random.value < 0.3f)
                {
                    if (_crackleRoutine != null) StopCoroutine(_crackleRoutine);
                    _crackleRoutine = StartCoroutine(PlayCrackles(clip.length));
                }
                return;
            }
        }

        AudioClip NextClip(SpeechEntry e)
        {
            if (!_pools.TryGetValue(e.token, out var pool) || pool.Count == 0)
                _pools[e.token] = pool = ShuffledIndices(e.clips.Length);

            int idx = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return e.clips[idx];
        }

        static List<int> ShuffledIndices(int count)
        {
            var list = new List<int>(count);
            for (int i = 0; i < count; i++) list.Add(i);
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        IEnumerator TrackSpeaking(float duration)
        {
            IsSpeaking = true;
            yield return new WaitForSeconds(duration);
            IsSpeaking = false;
            _speakingRoutine = null;
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
