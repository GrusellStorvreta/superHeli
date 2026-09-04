using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace SimCore
{
    // Speaks the AI coach's comments aloud via ElevenLabs TTS, routed through
    // the existing SpeechPlayer.audioSource so the synthesized line gets the
    // exact same radio effect (high-pass + dynamic distortion + crackle +
    // Interior mixer routing) as the pre-recorded canned phrases. Implements
    // ICoachOutput so it can be one of several outputs a CoachOutputBroadcaster
    // fans Say() out to (HUD text + spoken voice, both, not one replacing
    // the other).
    public class TtsCoachOutput : MonoBehaviour, ICoachOutput
    {
        // This component has no Update/OnGUI/etc., so Unity's Inspector won't
        // show the usual component enable/disable checkbox for it (there's
        // nothing for that checkbox to gate). This field is the real mute
        // switch — always visible in the Inspector regardless of that.
        [Tooltip("Uncheck to mute voice while keeping HUD text (via CoachOutputBroadcaster) working.")]
        public bool voiceEnabled = true;

        [Header("ElevenLabs")]
        [Tooltip("From your ElevenLabs account (Voice Library / My Voices) — cannot be defaulted.")]
        public string voiceId;
        [Tooltip("Low-latency/cost model.")]
        public string modelId = "eleven_flash_v2_5";

        [Header("Routing")]
        [Tooltip("Reuses this SpeechPlayer's AudioSource + radio-effect filters. Auto-found if left empty.")]
        public SpeechPlayer speechPlayer;

        // Sample rate and output_format are a matched pair — pcm_22050 means
        // "PCM at 22050 Hz". Not exposed as independent Inspector fields:
        // changing one without the other would silently pitch/garble the
        // audio. 22050 is chosen because 44.1k/48k PCM (and WAV) are gated to
        // ElevenLabs' Pro+ tier; 22050 isn't, and is plenty for a short
        // radio-filtered voice line.
        const string OutputFormat = "pcm_22050";
        const int    SampleRate   = 22050;

        // ElevenLabs' pcm_* formats are assumed mono. If a synthesized line
        // ever comes out pitched/garbled, that's the tell — bump this to 2
        // and retest; the decode math already generalizes over channel count.
        const int Channels = 1;

        const string ApiUrlFormat = "https://api.elevenlabs.io/v1/text-to-speech/{0}?output_format={1}";

        string _apiKey;
        bool   _requestInFlight;

        void Awake()
        {
            _apiKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[TtsCoachOutput] ELEVENLABS_API_KEY not set — TTS disabled.");
                enabled = false;
                return;
            }

            if (string.IsNullOrEmpty(voiceId))
            {
                Debug.LogWarning("[TtsCoachOutput] Voice Id not set in Inspector — TTS disabled.");
                enabled = false;
                return;
            }

            if (speechPlayer == null) speechPlayer = FindObjectOfType<SpeechPlayer>();
            if (speechPlayer == null)
            {
                Debug.LogWarning("[TtsCoachOutput] No SpeechPlayer found — TTS disabled (nowhere to route audio).");
                enabled = false;
            }
        }

        public void Say(string message)
        {
            if (!enabled || !voiceEnabled) return; // disabled components can still be called directly — guard explicitly
            if (string.IsNullOrEmpty(message)) return;

            if (_requestInFlight)
            {
                Debug.LogWarning($"[TtsCoachOutput] dropping TTS request (previous still in flight): \"{message}\"");
                return;
            }

            StartCoroutine(RequestSpeech(message));
        }

        IEnumerator RequestSpeech(string message)
        {
            _requestInFlight = true;

            var body = new ElevenLabsRequest { text = message, model_id = modelId };
            string bodyJson = JsonConvert.SerializeObject(body);
            string url = string.Format(ApiUrlFormat, voiceId, OutputFormat);

            using (var req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("xi-api-key", _apiKey);
                req.SetRequestHeader("content-type", "application/json");

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // Error bodies (e.g. 422 validation) are JSON text, so .text is safe here.
                    Debug.LogWarning($"[TtsCoachOutput] request failed: {req.error}\n{req.downloadHandler.text}");
                }
                else
                {
                    // Success body is raw application/octet-stream PCM bytes —
                    // must use .data here, not .text, which would UTF8-decode
                    // (and corrupt) arbitrary binary audio data.
                    byte[] pcmBytes = req.downloadHandler.data;
                    AudioClip clip = BuildClipFromPcm16(pcmBytes, SampleRate, Channels, "tts_coach_line");

                    if (clip != null)
                    {
                        Debug.Log($"[TtsCoachOutput] got {pcmBytes.Length} bytes, playing {clip.length:F2}s clip via {speechPlayer.name}");
                        speechPlayer.PlayClip(clip);
                    }
                    else Debug.LogWarning($"[TtsCoachOutput] received empty/invalid PCM data from ElevenLabs ({pcmBytes?.Length ?? 0} bytes).");
                }
            }

            _requestInFlight = false;
        }

        // Raw 16-bit little-endian PCM -> Unity AudioClip. stream:false + an
        // immediate SetData() is the standard non-streaming pattern since we
        // already have the full buffer in memory.
        static AudioClip BuildClipFromPcm16(byte[] pcmBytes, int sampleRate, int channels, string clipName)
        {
            if (pcmBytes == null || pcmBytes.Length < 2) return null;

            const int bytesPerSample = 2; // 16-bit
            int usableBytes  = pcmBytes.Length - (pcmBytes.Length % bytesPerSample); // guard odd trailing byte
            int totalSamples = usableBytes / bytesPerSample;                        // interleaved, all channels
            if (totalSamples <= 0 || totalSamples % channels != 0) return null;

            int lengthSamples = totalSamples / channels; // per-channel frame count Unity expects

            var data = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                int b = i * bytesPerSample;
                short sample = (short)(pcmBytes[b] | (pcmBytes[b + 1] << 8)); // little-endian, signed
                data[i] = sample / 32768f;                                    // -32768..32767 -> -1f..~1f
            }

            AudioClip clip = AudioClip.Create(clipName, lengthSamples, channels, sampleRate, stream: false);
            clip.SetData(data, 0);
            return clip;
        }

        class ElevenLabsRequest
        {
            public string text;
            public string model_id;
        }
    }
}
