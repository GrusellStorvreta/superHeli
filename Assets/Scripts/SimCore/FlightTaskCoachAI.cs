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
    // Task-based variant of the AI flight coach: assigns one maneuver from a
    // fixed, sequential list, samples telemetry for that task's own window,
    // then asks Claude to judge the attempt against the assigned task rather
    // than commenting on flying in general. Meant to run instead of
    // FlightCoachAI (toggle one off when the other is on) — they share the
    // same ICoachOutput/HUD, same manual on/off convention as
    // FlightCoachAI/FlightCoachTester.
    [RequireComponent(typeof(SimulatorDriver))]
    public class FlightTaskCoachAI : MonoBehaviour
    {
        public string model = "claude-haiku-4-5-20251001";
        public int maxTokens = 80;

        public float sampleRate = 10f; // Hz

        struct FlightTask
        {
            public string id;
            public string descriptionKey; // Loc key shown on the HUD
            public string promptText;     // English text sent to the API, independent of UI locale
            public float windowSeconds;   // how long the student gets before feedback
        }

        // Sequential list for now — reorder here; later this can become an
        // Inspector-editable list without changing the rest of the component.
        static readonly FlightTask[] Tasks =
        {
            new FlightTask
            {
                id = "turn_180_left",
                descriptionKey = "task.turn_180_left",
                promptText = "Come to 80 knots, then make a banking turn 180 degrees to the left.",
                windowSeconds = 45f,
            },
            new FlightTask
            {
                id = "hover_20s",
                descriptionKey = "task.hover_20s",
                promptText = "Come to a hover and maintain it for 20 seconds.",
                windowSeconds = 35f,
            },
            new FlightTask
            {
                id = "level_70kt_20s",
                descriptionKey = "task.level_70kt_20s",
                promptText = "Come to 70 knots and maintain level flight for 20 seconds.",
                windowSeconds = 30f,
            },
        };

        const string SystemPrompt =
            "You are a flight instructor reviewing a student pilot with no real-world " +
            "flying experience, flying a helicopter simulator. The student was just given " +
            "a specific task, then you'll receive telemetry (CSV rows, columns: " +
            "t,pitch,roll,heading,yawRate,vHorizontal,vVertical,sideslip,collective," +
            "cyclicX,cyclicY,pedal) recorded while they attempted it. Judge how well they " +
            "achieved that specific task. Reply with exactly one short, encouraging-but-honest " +
            "sentence (max ~25 words) — it's shown on a small HUD panel, so keep it brief.";

        const string ApiUrl = "https://api.anthropic.com/v1/messages";

        private SimulatorDriver driver;
        private Rigidbody rb;
        private ICoachOutput output;
        private string apiKey;

        private readonly List<string> buffer = new List<string>();
        private float sampleTimer;
        private float windowTimer;
        private float sessionStartTime;
        private bool requestInFlight;
        private int taskIndex;
        private string logPath;

        // Read by LessonHUD to draw the assigned task at the top of the screen,
        // mission-style — separate from the feedback comment, which goes through
        // ICoachOutput/HudCoachOutput at the bottom.
        public string CurrentTaskText { get; private set; } = "";
        public float TimeRemaining => Mathf.Max(0f, Tasks[taskIndex].windowSeconds - windowTimer);

        void Awake()
        {
            driver = GetComponent<SimulatorDriver>();
            rb = driver.rb != null ? driver.rb : GetComponent<Rigidbody>();

            apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[FlightTaskCoachAI] ANTHROPIC_API_KEY not set — task coaching disabled.");
                enabled = false;
                return;
            }

            output = GetComponent<ICoachOutput>();
            if (output == null) output = FindObjectOfType<HudCoachOutput>();
            if (output == null)
                Debug.LogWarning("[FlightTaskCoachAI] No ICoachOutput found — task/feedback will only be logged.");
        }

        void Start()
        {
            sessionStartTime = Time.time;

            string dir = Path.Combine(Application.persistentDataPath, "BlackBox", "Coaching");
            Directory.CreateDirectory(dir);
            logPath = Path.Combine(dir, $"tasks_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            AssignTask(0);
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

            windowTimer += Time.fixedDeltaTime;
            if (windowTimer >= Tasks[taskIndex].windowSeconds)
            {
                windowTimer -= Tasks[taskIndex].windowSeconds;
                EndWindow();
            }
        }

        void AssignTask(int index)
        {
            taskIndex = index % Tasks.Length;
            buffer.Clear();
            CurrentTaskText = Loc.Get(Tasks[taskIndex].descriptionKey);
            LogEntry($"TASK ({Tasks[taskIndex].id})", Tasks[taskIndex].promptText);
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

        void EndWindow()
        {
            FlightTask finished = Tasks[taskIndex];
            string csvChunk = buffer.Count > 0 ? string.Join("\n", buffer) : null;

            AssignTask(taskIndex + 1);

            if (csvChunk != null && !requestInFlight)
                StartCoroutine(RequestFeedback(finished, csvChunk));
        }

        IEnumerator RequestFeedback(FlightTask task, string csvChunk)
        {
            requestInFlight = true;

            string userContent = $"Task assigned: \"{task.promptText}\"\n\nTelemetry during the attempt:\n{csvChunk}";

            var body = new AnthropicRequest
            {
                model = model,
                max_tokens = maxTokens,
                system = SystemPrompt,
                messages = new List<AnthropicMessage>
                {
                    new AnthropicMessage { role = "user", content = userContent }
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
                    Debug.LogWarning($"[FlightTaskCoachAI] request failed: {req.error}\n{req.downloadHandler.text}");
                }
                else
                {
                    Debug.Log($"[FlightTaskCoachAI] raw response: {req.downloadHandler.text}");
                    string comment = TryParseComment(req.downloadHandler.text);
                    if (comment != null)
                    {
                        Debug.Log($"[FlightTaskCoachAI] feedback ({task.id}): {comment}");
                        output?.Say(comment);
                        LogEntry($"FEEDBACK ({task.id})", comment);
                    }
                    else
                    {
                        Debug.LogWarning("[FlightTaskCoachAI] could not parse feedback from response.");
                    }
                }
            }

            requestInFlight = false;
        }

        void LogEntry(string label, string text)
        {
            if (logPath == null) return;
            float t = Time.time - sessionStartTime;
            File.AppendAllText(logPath, $"[{t:F1}s] {label}: {text}\n\n");
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
                Debug.LogWarning($"[FlightTaskCoachAI] malformed response JSON: {e.Message}");
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
