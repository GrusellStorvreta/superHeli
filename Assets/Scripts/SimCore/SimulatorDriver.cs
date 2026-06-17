using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimCore
{
    [RequireComponent(typeof(Rigidbody))]
    public class SimulatorDriver : MonoBehaviour
    {
        [Tooltip("Entity id used with simulator.SetControlInput")]
        public string controlEntityId = "player";

        [Tooltip("Spawn the helicopter here on Start. Auto-finds 'SpawnPoint' in scene if unset.")]
        public Transform spawnPoint;

        [Header("Input processing")]
        public float deadzone = 0.05f;
        public float smoothingTime = 0.05f;
        public bool enableSmoothing = true;

        [Header("Keyboard")]
        public float keyboardCollectiveRate = 0.5f;
        public float keyboardPedalRate = 1.0f;

        private SimCore.Simulator simulator;
        private HeliInput heliInput;
        private Rigidbody rb;

        private float sm_collective, sm_cyclic_x, sm_cyclic_y, sm_pedal;
        private float keyboardCollective = 0.5f;
        private float keyboardPedal = 0f;

        public ControlInput LastBufferedControl { get; private set; } = new ControlInput();
        public float LastBufferedLeftTrigger { get; private set; }
        public float LastBufferedRightTrigger { get; private set; }

        public Vector3 LastBodyPosition => rb != null ? rb.position : transform.position;
        public Quaternion LastBodyRotation => rb != null ? rb.rotation : transform.rotation;
        public Vector3 LastBodyVelocity => rb != null ? rb.velocity : Vector3.zero;

        private IWebSocketClient websocketClient = new NoopWebSocketClient();

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 1000f;
            rb.drag = 0f;
            rb.angularDrag = 0f;
            rb.useGravity = true;

            if (spawnPoint == null)
                spawnPoint = GameObject.Find("SpawnPoint")?.transform;

            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
            }

            simulator = new SimCore.Simulator();

            heliInput = GetComponent<HeliInput>();
            if (heliInput == null)
                heliInput = gameObject.AddComponent<HeliInput>();
        }

        void Update()
        {
            if (!Application.isPlaying || heliInput == null) return;

            float delta = Time.deltaTime;

            float rawCollective = heliInput.collective;
            float rawPedal = heliInput.yaw;
            float rawCyclicX = heliInput.move.x;
            float rawCyclicY = heliInput.move.y;

#if ENABLE_INPUT_SYSTEM
            try
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    bool kbUp    = kb.upArrowKey.isPressed;
                    bool kbDown  = kb.downArrowKey.isPressed;
                    bool kbLeft  = kb.leftArrowKey.isPressed;
                    bool kbRight = kb.rightArrowKey.isPressed;
                    bool kbCollUp   = kb.aKey.isPressed;
                    bool kbCollDown = kb.zKey.isPressed;
                    bool kbPedalL   = kb.nKey.isPressed;
                    bool kbPedalR   = kb.mKey.isPressed;

                    if (kbLeft || kbRight || kbUp || kbDown)
                    {
                        rawCyclicX = (kbRight ? 1f : 0f) + (kbLeft ? -1f : 0f);
                        rawCyclicY = (kbUp    ? 1f : 0f) + (kbDown ? -1f : 0f);
                    }

                    if (kbCollUp || kbCollDown)
                    {
                        keyboardCollective += ((kbCollUp ? 1f : 0f) - (kbCollDown ? 1f : 0f)) * keyboardCollectiveRate * delta;
                        keyboardCollective = Mathf.Clamp01(keyboardCollective);
                        rawCollective = keyboardCollective;
                    }
                    else
                    {
                        keyboardCollective = rawCollective;
                    }

                    if (kbPedalL || kbPedalR)
                    {
                        float target = (kbPedalR ? 1f : 0f) + (kbPedalL ? -1f : 0f);
                        keyboardPedal = Mathf.MoveTowards(keyboardPedal, target, keyboardPedalRate * delta);
                        rawPedal = keyboardPedal;
                        LastBufferedLeftTrigger  = kbPedalL ? 1f : 0f;
                        LastBufferedRightTrigger = kbPedalR ? 1f : 0f;
                    }
                    else
                    {
                        keyboardPedal = rawPedal;
                        LastBufferedLeftTrigger  = Mathf.Max(0f, -rawPedal);
                        LastBufferedRightTrigger = Mathf.Max(0f,  rawPedal);
                    }
                }
            }
            catch { }
#endif

            // Exponential smoothing
            float alpha = 1f;
            if (enableSmoothing && smoothingTime > 0f)
                alpha = delta / (smoothingTime + delta);

            sm_collective += alpha * (rawCollective - sm_collective);
            sm_cyclic_x   += alpha * (rawCyclicX   - sm_cyclic_x);
            sm_cyclic_y   += alpha * (rawCyclicY   - sm_cyclic_y);
            sm_pedal      += alpha * (rawPedal     - sm_pedal);

            if (Mathf.Abs(sm_cyclic_x) < deadzone)             sm_cyclic_x = 0f;
            if (Mathf.Abs(sm_cyclic_y) < deadzone)             sm_cyclic_y = 0f;
            if (Mathf.Abs(sm_pedal) < deadzone)                sm_pedal    = 0f;
            if (Mathf.Abs(sm_collective - 0.5f) < deadzone)    sm_collective = 0.5f;

            var control = new ControlInput
            {
                collective = sm_collective,
                cyclic_x   = sm_cyclic_x,
                cyclic_y   = sm_cyclic_y,
                pedal      = sm_pedal
            };

            simulator.SetControlInput(controlEntityId, control);
            LastBufferedControl = control;

            try { SendControlJson(control); } catch { }
            try { websocketClient?.DispatchMessageQueue(); } catch { }

            if (heliInput.resetPressed) ResetToSpawnPoint();
        }

        void ResetToSpawnPoint()
        {
            if (spawnPoint == null) return;
            rb.position = spawnPoint.position;
            rb.rotation = spawnPoint.rotation;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        void FixedUpdate()
        {
            if (!Application.isPlaying || simulator == null || rb == null) return;

            double dt = Time.fixedDeltaTime;
            simulator.UpdateActuators(controlEntityId, dt);

            Vector3 force  = simulator.ComputeForce(controlEntityId, rb.rotation, rb.velocity);
            Vector3 torque = simulator.ComputeTorque(controlEntityId, rb.rotation, rb.angularVelocity);

            rb.AddForce(force);
            rb.AddTorque(torque, ForceMode.Acceleration);
        }

        public void Connect(string url) => websocketClient.Connect(url);

        public void SendControlJson(ControlInput control)
        {
            if (websocketClient == null || !websocketClient.IsConnected) return;
            string json = "{\"type\":\"control\""
                + ",\"collective\":" + control.collective.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"cyclic_x\":"   + control.cyclic_x.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"cyclic_y\":"   + control.cyclic_y.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"pedal\":"      + control.pedal.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "}";
            websocketClient.Send(json);
        }
    }
}
