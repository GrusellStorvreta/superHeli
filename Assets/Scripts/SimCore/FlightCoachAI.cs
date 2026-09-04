using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace SimCore
{
    // Live in-game flight coach: periodically samples telemetry (mirroring
    // BlackBoxRecorder's columns) and sends the last chunk to Claude for a
    // short coaching comment, delivered via ICoachOutput. Deliberately
    // independent of BlackBoxRecorder — duplicates its small sampling
    // calculation rather than depending on it, so either component can be
    // enabled/disabled in the Inspector with zero effect on the other.
    [RequireComponent(typeof(SimulatorDriver))]
    public class FlightCoachAI : MonoBehaviour
    {
        public string model = "claude-haiku-4-5-20251001";
        public int maxTokens = 220;

        public float sampleRate = 10f;    // Hz
        public float chunkInterval = 10f; // seconds

        const string SystemPrompt =
            "You are a flight instructor reviewing telemetry from a student pilot " +
            "with no real-world flying experience, flying a helicopter simulator. " +
            "You'll receive the last ~10 seconds of flight data as CSV rows " +
            "(columns: t,pitch,roll,heading,yawRate,vHorizontal,vVertical,sideslip," +
            "collective,cyclicX,cyclicY,pedal). Give one short, encouraging-but-honest " +
            "coaching comment (1-2 sentences) about what just happened.";

        const string ApiUrl = "https://api.anthropic.com/v1/messages";

        private SimulatorDriver driver;
        private Rigidbody rb;
        private ICoachOutput output;
        private string apiKey;

        private readonly List<string> buffer = new List<string>();
        private float sampleTimer;
        private float chunkTimer;
        private float sessionStartTime;
        private bool requestInFlight;
        private string logPath;

        void Awake()
        {
            driver = GetComponent<SimulatorDriver>();
            rb = driver.rb != null ? driver.rb : GetComponent<Rigidbody>();

            apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[FlightCoachAI] ANTHROPIC_API_KEY not set — coaching disabled.");
                enabled = false;
                return;
            }

            output = FindObjectOfType<CoachOutputBroadcaster>();
            if (output == null)
                Debug.LogWarning("[FlightCoachAI] No CoachOutputBroadcaster found — coaching comments will only be logged.");
        }

        void Start()
        {
            sessionStartTime = Time.time;

            string dir = Path.Combine(Application.persistentDataPath, "BlackBox", "Coaching");
            Directory.CreateDirectory(dir);
            logPath = Path.Combine(dir, $"coaching_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        }

        void FixedUpdate()
        {
            if (!Application.isPlaying || rb == null || !GameSettings.SimulationMode) return;

            sampleTimer += Time.fixedDeltaTime;
            if (sampleTimer >= 1f / sampleRate)
            {
                sampleTimer -= 1f / sampleRate;
                Sample();
            }

            chunkTimer += Time.fixedDeltaTime;
            if (chunkTimer >= chunkInterval)
            {
                chunkTimer -= chunkInterval;
                SendChunk();
            }
        }

        void Sample()
        {
            Vector3 euler = rb.rotation.eulerAngles;
            float pitch = NormalizeAngle(euler.x);
            float roll = NormalizeAngle(euler.z);
            float heading = euler.y;
            float yawRate = rb.angularVelocity.y * Mathf.Rad2Deg;

            Vector3 vel = rb.velocity;
            float vHorizontal = new Vector2(vel.x, vel.z).magnitude;
            float vVertical = vel.y;

            // Angle between where the nose points (flattened) and the direction
            // the helicopter is actually moving — same definition BlackBoxRecorder
            // uses, so the two data sources stay comparable.
            float sideslip = 0f;
            Vector3 flatFwd = rb.rotation * Vector3.forward; flatFwd.y = 0f;
            Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);
            if (flatFwd.sqrMagnitude > 0.001f && flatVel.sqrMagnitude > 1f)
                sideslip = Vector3.SignedAngle(flatFwd, flatVel, Vector3.up);

            ControlInput c = driver.LastBufferedControl;

            buffer.Add(string.Join(",", new[]
            {
                (Time.time - sessionStartTime).ToString("F2", CultureInfo.InvariantCulture),
                pitch.ToString("F2", CultureInfo.InvariantCulture),
                roll.ToString("F2", CultureInfo.InvariantCulture),
                heading.ToString("F2", CultureInfo.InvariantCulture),
                yawRate.ToString("F2", CultureInfo.InvariantCulture),
                vHorizontal.ToString("F2", CultureInfo.InvariantCulture),
                vVertical.ToString("F2", CultureInfo.InvariantCulture),
                sideslip.ToString("F2", CultureInfo.InvariantCulture),
                c.collective.ToString("F3", CultureInfo.InvariantCulture),
                c.cyclic_x.ToString("F3", CultureInfo.InvariantCulture),
                c.cyclic_y.ToString("F3", CultureInfo.InvariantCulture),
                c.pedal.ToString("F3", CultureInfo.InvariantCulture)
            }));
        }

        static float NormalizeAngle(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            return deg;
        }

        void SendChunk()
        {
            if (buffer.Count == 0) return;

            if (requestInFlight)
            {
                buffer.Clear(); // drop this cycle rather than let it grow unbounded
                return;
            }

            string csvChunk = string.Join("\n", buffer);
            buffer.Clear();
            StartCoroutine(RequestCoaching(csvChunk));
        }

        IEnumerator RequestCoaching(string csvChunk)
        {
            requestInFlight = true;

            var body = new AnthropicRequest
            {
                model = model,
                max_tokens = maxTokens,
                system = SystemPrompt,
                messages = new List<AnthropicMessage>
                {
                    new AnthropicMessage { role = "user", content = csvChunk }
                }
            };
            string bodyJson = JsonConvert.SerializeObject(body);

            using (var req = new UnityWebRequest(ApiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("x-api-key", apiKey);
                req.SetRequestHeader("anthropic-version", "2023-06-01");
                req.SetRequestHeader("content-type", "application/json");

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[FlightCoachAI] request failed: {req.error}\n{req.downloadHandler.text}");
                }
                else
                {
                    Debug.Log($"[FlightCoachAI] raw response: {req.downloadHandler.text}");
                    string comment = TryParseComment(req.downloadHandler.text);
                    if (comment != null)
                    {
                        Debug.Log($"[FlightCoachAI] coach: {comment}");
                        output?.Say(comment);
                        LogComment(comment);
                    }
                    else
                    {
                        Debug.LogWarning("[FlightCoachAI] could not parse a coaching comment from response.");
                    }
                }
            }

            requestInFlight = false;
        }

        void LogComment(string comment)
        {
            if (logPath == null) return;
            float t = Time.time - sessionStartTime;
            File.AppendAllText(logPath, $"[{t:F1}s] {comment}\n\n");
        }

        static string TryParseComment(string responseJson)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<AnthropicResponse>(responseJson);
                if (parsed?.stop_reason == "refusal") return null;
                var textBlock = parsed?.content?.Find(b => b.type == "text");
                return textBlock?.text;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FlightCoachAI] malformed response JSON: {e.Message}");
                return null;
            }
        }

        class AnthropicMessage
        {
            public string role;
            public string content;
        }

        class AnthropicRequest
        {
            public string model;
            public int max_tokens;
            public string system;
            public List<AnthropicMessage> messages;
        }

        class AnthropicContentBlock
        {
            public string type;
            public string text;
        }

        class AnthropicResponse
        {
            public string stop_reason;
            public List<AnthropicContentBlock> content;
        }
    }
}
