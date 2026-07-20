using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace SimCore
{
    // Flight-data recorder for tuning Simulation mode: samples attitude, rates,
    // velocity and control inputs at a fixed rate and periodically appends them
    // to a CSV under Application.persistentDataPath, so a flight can be reviewed
    // after the fact (e.g. checking bank angle against turn rate for a
    // coordinated turn) without needing a licensed pilot in the loop.
    // Only records while GameSettings.SimulationMode is on — Arcade physics
    // aren't what we're trying to validate against real aerodynamics.
    [RequireComponent(typeof(SimulatorDriver))]
    public class BlackBoxRecorder : MonoBehaviour
    {
        public float sampleRate = 10f;    // Hz
        public float flushInterval = 10f; // seconds

        private const string Header =
            "t,pitch,roll,heading,yawRate,vHorizontal,vVertical,sideslip,collective,cyclicX,cyclicY,pedal";

        private SimulatorDriver driver;
        private Rigidbody rb;
        private readonly List<string> buffer = new List<string>();
        private float sampleTimer;
        private float flushTimer;
        private float sessionStartTime;
        private string filePath;

        void Awake()
        {
            driver = GetComponent<SimulatorDriver>();
            rb = driver.rb != null ? driver.rb : GetComponent<Rigidbody>();
        }

        void Start()
        {
            sessionStartTime = Time.time;
            string dir = Path.Combine(Application.persistentDataPath, "BlackBox");
            Directory.CreateDirectory(dir);
            filePath = Path.Combine(dir, $"flight_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(filePath, Header + "\n");
            Debug.Log($"[BlackBoxRecorder] Logging to {filePath}");
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

            flushTimer += Time.fixedDeltaTime;
            if (flushTimer >= flushInterval)
            {
                flushTimer -= flushInterval;
                Flush();
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
            // the helicopter is actually moving — 0 = coordinated, nonzero = skidding.
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

        void Flush()
        {
            if (buffer.Count == 0 || filePath == null) return;
            File.AppendAllLines(filePath, buffer);
            buffer.Clear();
        }

        void OnDisable() => Flush();
        void OnApplicationQuit() => Flush();
    }
}
