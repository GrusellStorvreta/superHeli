using System;
using System.Reflection;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Lightweight MonoBehaviour wrapper to drive SimCore.Simulator from a Unity scene.
// Adds Xbox controller mapping (legacy Input API), smoothing and websocket control frames.
// Compatible with Unity 2021 LTS. This runs in Edit mode if "runInEditor" is enabled.

namespace SimCore
{
    [ExecuteAlways]
    public class SimulatorDriver : MonoBehaviour
    {
        [Tooltip("Timestep used for simulation stepping in seconds")]
        public float dt = 0.02f;

        [Tooltip("If true, advance simulator using fixed-size steps accumulated from Time.deltaTime")]
        public bool useFixedTimestep = true;

        [Tooltip("Allow running the simulator in the Editor (Edit mode). Use with care.")]
        public bool runInEditor = false;

        // --- Input mapping (legacy Input API) ---
        [Header("Controller axis names (legacy Input.GetAxis)")]
        [Tooltip("Left stick horizontal axis name (recommended default: 'Horizontal')")]
        public string leftStickXAxis = "Horizontal";

        [Tooltip("Left stick vertical axis name (recommended default: 'Vertical')")]
        public string leftStickYAxis = "Vertical";

        [Tooltip("Right stick horizontal axis name (create in Input Manager if missing)")]
        public string rightStickXAxis = "Axis 4";

        [Tooltip("Right stick vertical axis name (create in Input Manager if missing)")]
        public string rightStickYAxis = "Axis 5";

        [Tooltip("Left trigger axis name (create in Input Manager: 0..1 expected or -1..1)")]
        public string leftTriggerAxis = "Axis 3";

        [Tooltip("Right trigger axis name (create in Input Manager: 0..1 expected or -1..1)")]
        public string rightTriggerAxis = "Axis 3";

        // Fallback axis name candidates (useful when Input Manager uses different names). These are probed during debug logging and auto-detection.
        private readonly string[] leftStickXCandidates = new string[] { "Horizontal", "LeftStickHorizontal", "LeftStickX", "Joystick1Horizontal", "Joystick1X" };
        private readonly string[] leftStickYCandidates = new string[] { "Vertical", "LeftStickVertical", "LeftStickY", "Joystick1Vertical", "Joystick1Y" };
        private readonly string[] rightStickXCandidates = new string[] { "RightStickHorizontal", "RightStickX", "Joystick2Horizontal", "Joystick2X" };
        private readonly string[] rightStickYAxisCandidates = new string[] { "RightStickVertical", "RightStickY", "RightStick", "Joystick2Vertical", "Joystick2Y", "CameraVertical", "Joystick6Vertical", "Joystick6Y" };
        private readonly string[] triggerCandidates = new string[] { "LeftTrigger", "RightTrigger", "Triggers", "Trigger", "3rdAxis", "4thAxis", "Joystick3", "Joystick4" };

        [Header("Auto-detect")]
        [Tooltip("If enabled, the driver will probe common axis names at runtime and pick ones that respond to the controller. Useful when Input Manager axis names differ.")]
        public bool enableAutoDetectAxes = true;

        // Whether auto-detection already completed for this session
        private bool axesAutoDetected = false;

        // Internal per-axis detection flags
        private bool leftXDetected = false, leftYDetected = false, rightYDetected = false, triggersDetected = false;

        // Combined-trigger left-trigger scanning support
        private bool combinedTriggerScanActive = false;
        private double combinedTriggerScanEnd = 0.0;
        private double combinedTriggerScanDuration = 2.0; // seconds to ask user to press left trigger
        private System.Collections.Generic.Dictionary<string, float> combinedTriggerScanMax = new System.Collections.Generic.Dictionary<string, float>();
        private float combinedTriggerScanThreshold = 0.05f; // threshold to consider axis responsive

        [Tooltip("Entity id to tag control input for simulator.SetControlInput(entityId, control)")]
        public string controlEntityId = "player";

        [Header("Input processing")]
        [Tooltip("Deadzone applied to stick axes (absolute threshold). Values within +/- deadzone are treated as zero.")]
        public float deadzone = 0.05f;

        [Tooltip("Smoothing time constant in seconds. Set to 0 for no smoothing.")]
        public float smoothingTime = 0.05f;

        [Tooltip("If true, apply exponential smoothing to inputs; otherwise inputs are immediate (after deadzone).")]
        public bool enableSmoothing = true;

        [Header("Keyboard mapping (holds/presses)")]
        [Tooltip("Collective change rate when using keyboard (units per second on A/Z)")]
        public float keyboardCollectiveRate = 0.5f;

        [Tooltip("Pedal change rate when using keyboard (units per second on N/M)")]
        public float keyboardPedalRate = 1.0f;

        // Internal keyboard collective state (0..1). Initialized to center 0.5 to match joystick mid.
        private float keyboardCollective = 0.5f;

        // Internal keyboard pedal state (-1..1). Initialized to 0 (centered).
        private float keyboardPedal = 0.0f;

        // TODO: add Input System (new) support and an inspector switch. For now we only support legacy Input.GetAxis and simple keyboard mapping.

        // The actual simulation core (assumed to be in namespace SimCore)
        private SimCore.Simulator simulator;

        // Accumulate delta for fixed-step stepping
        private double timeAccumulator = 0.0;

        // WebSocket client (no-op default)
        private IWebSocketClient websocketClient = new NoopWebSocketClient();

        // Internal smoothed state
        private float sm_collective = 0f; // 0..1
        private float sm_cyclic_x = 0f;   // -1..1
        private float sm_cyclic_y = 0f;   // -1..1
        private float sm_pedal = 0f;      // -1..1

        // Debug/logging helpers (rate-limited)
        private double _lastDebugLogTime = 0.0; // seconds (Time.realtimeSinceStartup)
        private float _lastLoggedCollective = -999f, _lastLoggedCyclicX = -999f, _lastLoggedCyclicY = -999f, _lastLoggedPedal = -999f;

        // Buffered control state when Simulator.SetControlInput is not available.
        public ControlInput LastBufferedControl { get; private set; } = new ControlInput();

        // Per-trigger buffered raw values (0..1) for HUD and external use
        public float LastBufferedLeftTrigger { get; private set; } = 0f;
        public float LastBufferedRightTrigger { get; private set; } = 0f;

        void Awake()
        {
            // Instantiate simulator core
            if (simulator == null)
            {
                simulator = new SimCore.Simulator();
            }
        }

        void OnEnable()
        {
            if (simulator == null)
                simulator = new SimCore.Simulator();
        }

        void OnDisable()
        {
            // Placeholder: if Simulator exposes Dispose/Shutdown, call it here.
        }

        void Update()
        {
            // Only run while playing or when the user explicitly allows running in Editor.
            if (!(Application.isPlaying || runInEditor))
                return;

            if (simulator == null)
                simulator = new SimCore.Simulator();

            float delta = Time.deltaTime;

            // Sample raw inputs once per Update (legacy Input API)
            // If enabled, attempt to auto-detect responsive axis names before sampling
            if (enableAutoDetectAxes && !axesAutoDetected)
            {
                TryAutoDetectAxes();
            }

            float rawLeftX = 0f;
            float rawLeftY = 0f;
            float rawRightX = 0f;
            float rawRightY = 0f;
            float rawLT = 0f;
            float rawRT = 0f;

#if ENABLE_INPUT_SYSTEM
            // Prefer Gamepad.current (Input System) if available; otherwise fall back to legacy Input.GetAxis.
            bool usedInputSystem = false;
#if ENABLE_INPUT_SYSTEM
            try
            {
                var gp = Gamepad.current;
                if (gp != null)
                {
                    rawLeftX = gp.leftStick.ReadValue().x;
                    rawLeftY = gp.leftStick.ReadValue().y;
                    rawRightX = gp.rightStick.ReadValue().x;
                    rawRightY = gp.rightStick.ReadValue().y;
                    rawLT = gp.leftTrigger.ReadValue(); // 0..1
                    rawRT = gp.rightTrigger.ReadValue(); // 0..1
                    usedInputSystem = true;
                }
            }
            catch (Exception)
            {
                usedInputSystem = false;
            }
#endif

            if (!usedInputSystem)
            {
                try
                {
                    rawLeftX = Input.GetAxis(leftStickXAxis);
                    rawLeftY = Input.GetAxis(leftStickYAxis);
                    rawRightX = Input.GetAxis(rightStickXAxis);
                    rawRightY = Input.GetAxis(rightStickYAxis);
                    rawLT = Input.GetAxis(leftTriggerAxis);
                    rawRT = Input.GetAxis(rightTriggerAxis);
                }
                catch (Exception)
                {
                    // Input axes may not be defined in Input Manager; fall back to zeros.
                }
            }
            try
            {
                rawLeftX = Input.GetAxis(leftStickXAxis);
                rawLeftY = Input.GetAxis(leftStickYAxis);
                rawRightX = Input.GetAxis(rightStickXAxis);
                rawRightY = Input.GetAxis(rightStickYAxis);
                rawLT = Input.GetAxis(leftTriggerAxis);
                rawRT = Input.GetAxis(rightTriggerAxis);
            }
            catch (Exception)
            {
                // Input axes may not be defined in Input Manager; fall back to zeros.
            }
#endif

            // Keyboard input detection - prefer new Input System when available
            bool kbUp = false, kbDown = false, kbLeft = false, kbRight = false;
            bool kbCollectiveUp = false, kbCollectiveDown = false;
            bool kbPedalLeft = false, kbPedalRight = false;

#if ENABLE_INPUT_SYSTEM
            // Prefer new Input System keyboard if available
            bool keyboardUsed = false;
            try
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    kbUp = kb.upArrowKey.isPressed;
                    kbDown = kb.downArrowKey.isPressed;
                    kbLeft = kb.leftArrowKey.isPressed;
                    kbRight = kb.rightArrowKey.isPressed;

                    kbCollectiveUp = kb.aKey.isPressed;
                    kbCollectiveDown = kb.zKey.isPressed;

                    kbPedalLeft = kb.nKey.isPressed;
                    kbPedalRight = kb.mKey.isPressed;

                    keyboardUsed = true;
                }
            }
            catch (Exception)
            {
                keyboardUsed = false;
            }

            if (!keyboardUsed)
            {
                // Fall back to legacy Input API (may throw if legacy input disabled)
                try
                {
                    kbUp = Input.GetKey(KeyCode.UpArrow);
                    kbDown = Input.GetKey(KeyCode.DownArrow);
                    kbLeft = Input.GetKey(KeyCode.LeftArrow);
                    kbRight = Input.GetKey(KeyCode.RightArrow);

                    kbCollectiveUp = Input.GetKey(KeyCode.A);
                    kbCollectiveDown = Input.GetKey(KeyCode.Z);

                    kbPedalLeft = Input.GetKey(KeyCode.N);
                    kbPedalRight = Input.GetKey(KeyCode.M);
                }
                catch (Exception)
                {
                    // No keyboard available via legacy API
                }
            }
#else
            // Fall back to legacy Input API only
            try
            {
                kbUp = Input.GetKey(KeyCode.UpArrow);
                kbDown = Input.GetKey(KeyCode.DownArrow);
                kbLeft = Input.GetKey(KeyCode.LeftArrow);
                kbRight = Input.GetKey(KeyCode.RightArrow);

                kbCollectiveUp = Input.GetKey(KeyCode.A);
                kbCollectiveDown = Input.GetKey(KeyCode.Z);

                kbPedalLeft = Input.GetKey(KeyCode.N);
                kbPedalRight = Input.GetKey(KeyCode.M);
            }
            catch (Exception)
            {
                // No keyboard available via legacy API
            }
#endif

            // Map axes to control ranges
            // Collective: prefer keyboard control if A/Z pressed, otherwise use RIGHT stick vertical
            float rawCollective;
            if (kbCollectiveUp || kbCollectiveDown)
            {
                keyboardCollective += ((kbCollectiveUp ? 1f : 0f) - (kbCollectiveDown ? 1f : 0f)) * keyboardCollectiveRate * delta;
                keyboardCollective = Mathf.Clamp01(keyboardCollective);
                rawCollective = keyboardCollective;
            }
            else
            {
                rawCollective = Mathf.Clamp01((rawRightY + 1f) / 2f);
                // keep keyboardCollective in sync if joystick is used
                keyboardCollective = rawCollective;
            }

            // Cyclic: arrow keys override joystick when held
            float rawCyclicX = 0f;
            float rawCyclicY = 0f;
            if (kbLeft || kbRight || kbUp || kbDown)
            {
                rawCyclicX = (kbRight ? 1f : 0f) + (kbLeft ? -1f : 0f);
                rawCyclicY = (kbUp ? 1f : 0f) + (kbDown ? -1f : 0f);
            }
            else
            {
                rawCyclicX = Mathf.Clamp(rawLeftX, -1f, 1f);
                rawCyclicY = Mathf.Clamp(rawLeftY, -1f, 1f);
            }

            // Pedal: keyboard keys N (left) and M (right) map to -1/1 but with rate-limited change; if none pressed, use triggers
            float rawPedal = 0f;
            if (kbPedalLeft || kbPedalRight)
            {
                float target = (kbPedalRight ? 1f : 0f) + (kbPedalLeft ? -1f : 0f);
                // Move keyboardPedal toward target at keyboardPedalRate units/sec
                if (keyboardPedal < target) keyboardPedal = Mathf.Min(target, keyboardPedal + keyboardPedalRate * delta);
                else if (keyboardPedal > target) keyboardPedal = Mathf.Max(target, keyboardPedal - keyboardPedalRate * delta);
                rawPedal = keyboardPedal;

                // map keyboard to left/right trigger indicators (digital)
                LastBufferedLeftTrigger = kbPedalLeft ? 1f : 0f;
                LastBufferedRightTrigger = kbPedalRight ? 1f : 0f;
            }
            else
            {
                // If triggers are mapped to the same combined axis (e.g., Axis 3), Input.GetAxis returns one value representing both triggers.
                if (!string.IsNullOrEmpty(leftTriggerAxis) && leftTriggerAxis == rightTriggerAxis)
                {
                    // Combined trigger axis (one axis represents both triggers). Commonly centered at 0 with left negative, right positive.
                    float combined = rawRT; // same as rawLT
                    // Split combined into separate left/right trigger values in [0,1]
                    float lt_val = combined < 0f ? -combined : 0f;
                    float rt_val = combined > 0f ? combined : 0f;
                        // Compute pedal as RT - LT in [-1,1]
                    rawPedal = Mathf.Clamp(rt_val - lt_val, -1f, 1f);
                    // Debug: log split trigger values to help diagnose missing left trigger
                    Debug.Log($"SimulatorDriver: combined trigger axis '{leftTriggerAxis}'={combined:F3} -> lt_val={lt_val:F3}, rt_val={rt_val:F3}, rawPedal={rawPedal:F3}");

                    // update buffered trigger indicators
                    LastBufferedLeftTrigger = lt_val;
                    LastBufferedRightTrigger = rt_val;

                    // If left trigger never produces negative combined (lt_val==0) and we see RT responding,
                    // start a short scan asking user to press the left trigger to find an alternative axis.
                    if (Mathf.Approximately(lt_val, 0f) && rt_val > 0.01f && !combinedTriggerScanActive)
                    {
                        combinedTriggerScanActive = true;
                        combinedTriggerScanEnd = Time.realtimeSinceStartupAsDouble + combinedTriggerScanDuration;
                        combinedTriggerScanMax.Clear();
                        foreach (var name in triggerCandidates) combinedTriggerScanMax[name] = 0f;
                        Debug.Log("SimulatorDriver: Detected combined trigger appears right-only. Please press and hold the LEFT trigger now for 2 seconds to auto-detect its axis.");
                    }

                    // If a scan is active, update per-candidate maxima
                    if (combinedTriggerScanActive)
                    {
                        double now = Time.realtimeSinceStartupAsDouble;
                        foreach (var name in triggerCandidates)
                        {
                            float v = 0f;
                            try { v = Input.GetAxis(name); } catch { continue; }
                            combinedTriggerScanMax[name] = Mathf.Max(combinedTriggerScanMax[name], Mathf.Abs(v));
                        }

                        if (now >= combinedTriggerScanEnd)
                        {
                            // Select best candidate (excluding the currently used combined axis name)
                            string best = null;
                            float bestVal = 0f;
                            foreach (var kv in combinedTriggerScanMax)
                            {
                                if (kv.Key == leftTriggerAxis) continue;
                                if (kv.Value > bestVal)
                                {
                                    bestVal = kv.Value;
                                    best = kv.Key;
                                }
                            }

                            if (best != null && bestVal > combinedTriggerScanThreshold)
                            {
                                leftTriggerAxis = best;
                                Debug.Log($"SimulatorDriver: Auto-detected left trigger axis '{best}' (max={bestVal:F3}). Updated mapping.");
                                // re-read rawLT from new axis
                                try { rawLT = Input.GetAxis(leftTriggerAxis); } catch { rawLT = 0f; }
                                // recompute lt/rt and pedal
                                float lt = rawLT; if (lt >= -1f && lt <= 1f) lt = (lt + 1f) / 2f;
                                float rt = rawRT; if (rt >= -1f && rt <= 1f) rt = (rt + 1f) / 2f;
                                rawPedal = Mathf.Clamp(rt - lt, -1f, 1f);
                            }
                            else
                            {
                                Debug.Log("SimulatorDriver: Left trigger scan did not find a responsive axis.");
                            }

                            combinedTriggerScanActive = false;
                        }
                    }

                    // keep keyboardPedal in sync when switching back to joystick
                    keyboardPedal = rawPedal;
                }                else
                {
                    // Handle triggers that may return -1..1 (convert to 0..1) or already 0..1.
                    float lt = rawLT;
                    float rt = rawRT;
                    if (lt >= -1f && lt <= 1f) lt = (lt + 1f) / 2f;
                    if (rt >= -1f && rt <= 1f) rt = (rt + 1f) / 2f;
                    rawPedal = (rt - lt);
                    // keep keyboardPedal in sync when switching back to joystick
                    keyboardPedal = rawPedal;

                    // update buffered trigger indicators
                    LastBufferedLeftTrigger = lt;
                    LastBufferedRightTrigger = rt;
                }
            }

            rawPedal = Mathf.Clamp(rawPedal, -1f, 1f);

            if (useFixedTimestep)
            {
                timeAccumulator += delta;
                while (timeAccumulator >= dt)
                {
                    // For each simulation step apply smoothing using step dt
                    ApplySmoothingAndSend(dt, rawCollective, rawCyclicX, rawCyclicY, rawPedal);

                    var frame = simulator.Step(dt);
                    timeAccumulator -= dt;

                    // Serialize and send frame over websocket if connected
                    try
                    {
                        if (websocketClient != null && websocketClient.IsConnected)
                        {
                            string json = SerializeFrameToJson(frame);
                            SendFrameJson(json);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("Failed to send frame JSON: " + ex.Message);
                    }
                }
            }
            else
            {
                // Variable timestep: apply smoothing with variable dt
                ApplySmoothingAndSend(delta, rawCollective, rawCyclicX, rawCyclicY, rawPedal);

                var frame = simulator.Step(delta);

                // Serialize and send on variable timestep path
                try
                {
                    if (websocketClient != null && websocketClient.IsConnected)
                    {
                        string json = SerializeFrameToJson(frame);
                        SendFrameJson(json);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Failed to send frame JSON: " + ex.Message);
                }
            }

            // Give websocket client a chance to dispatch incoming messages (no-op for NoopWebSocketClient).
            try
            {
                websocketClient?.DispatchMessageQueue();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("WebSocket dispatch failed: " + ex.Message);
            }
        }

        // Apply deadzone and exponential smoothing then send to simulator + websocket
        private void ApplySmoothingAndSend(float stepDt, float rawCollective, float rawCyclicX, float rawCyclicY, float rawPedal)
        {
            float alpha = 1f;
            if (enableSmoothing && smoothingTime > 0f)
            {
                alpha = stepDt / (smoothingTime + stepDt); // exponential smoothing weight
            }

            // Update smoothed values
            sm_collective = sm_collective + alpha * (rawCollective - sm_collective);
            sm_cyclic_x = sm_cyclic_x + alpha * (rawCyclicX - sm_cyclic_x);
            sm_cyclic_y = sm_cyclic_y + alpha * (rawCyclicY - sm_cyclic_y);
            sm_pedal = sm_pedal + alpha * (rawPedal - sm_pedal);

            // Deadzone: for cyclic and pedal operate around zero; for collective operate around midpoint 0.5
            if (Mathf.Abs(sm_cyclic_x) < deadzone) sm_cyclic_x = 0f;
            if (Mathf.Abs(sm_cyclic_y) < deadzone) sm_cyclic_y = 0f;
            if (Mathf.Abs(sm_pedal) < deadzone) sm_pedal = 0f;
            if (Mathf.Abs(sm_collective - 0.5f) < deadzone) sm_collective = 0.5f;

            // Build ControlInput
            var control = new ControlInput
            {
                collective = sm_collective,
                cyclic_x = sm_cyclic_x,
                cyclic_y = sm_cyclic_y,
                pedal = sm_pedal
            };

            // Send to simulator (try direct call or reflection shim)
            try
            {
                SetControlInputSafe(control);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("SetControlInput failed: " + ex.Message);
            }

            // Send control over websocket for external backends
            try
            {
                SendControlJson(control);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Failed to send control JSON: " + ex.Message);
            }

            // Update public buffer (for external simulators that read this driver via reflection)
            LastBufferedControl = control;

            // Rate-limited debug logging: show raw and smoothed values when they change notably or every 0.5s
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            const double DEBUG_INTERVAL = 0.5;
            const float DEBUG_THRESH = 0.01f;
            if (now - _lastDebugLogTime >= DEBUG_INTERVAL ||
                Mathf.Abs(_lastLoggedCollective - sm_collective) > DEBUG_THRESH ||
                Mathf.Abs(_lastLoggedCyclicX - sm_cyclic_x) > DEBUG_THRESH ||
                Mathf.Abs(_lastLoggedCyclicY - sm_cyclic_y) > DEBUG_THRESH ||
                Mathf.Abs(_lastLoggedPedal - sm_pedal) > DEBUG_THRESH)
            {
                _lastDebugLogTime = now;
                _lastLoggedCollective = sm_collective;
                _lastLoggedCyclicX = sm_cyclic_x;
                _lastLoggedCyclicY = sm_cyclic_y;
                _lastLoggedPedal = sm_pedal;

                // Build supplemental axis diagnostics to help debug missing mappings
                System.Text.StringBuilder axsb = new System.Text.StringBuilder();
                axsb.Append("axes:");
                try
                {
                    foreach (var n in rightStickYAxisCandidates)
                    {
                        float v = Input.GetAxis(n);
                        axsb.AppendFormat(" {0}={1:F2}", n, v);
                    }
                    foreach (var n in triggerCandidates)
                    {
                        float v = Input.GetAxis(n);
                        axsb.AppendFormat(" {0}={1:F2}", n, v);
                    }
                }
                catch (Exception)
                {
                    // Ignore any missing axis errors coming from Input.GetAxis
                }

                Debug.Log($"[SimulatorDriver] rawCollective={rawCollective:F3} rawCyclicX={rawCyclicX:F3} rawCyclicY={rawCyclicY:F3} rawPedal={rawPedal:F3} | sm_collective={sm_collective:F3} sm_cyclic_x={sm_cyclic_x:F3} sm_cyclic_y={sm_cyclic_y:F3} sm_pedal={sm_pedal:F3} {axsb}");
            }
        }

        // Try to auto-detect responsive axis names from common candidates.
        private void TryAutoDetectAxes()
        {
            try
            {
                // Detect left stick X
                if (!leftXDetected)
                {
                    foreach (var n in leftStickXCandidates)
                    {
                        float v = 0f;
                        try { v = Input.GetAxis(n); } catch { continue; }
                        if (Mathf.Abs(v) > 0.15f)
                        {
                            leftStickXAxis = n;
                            leftXDetected = true;
                            Debug.Log($"SimulatorDriver: auto-detected leftStickXAxis = '{n}' (value={v:F2})");
                            break;
                        }
                    }
                }

                // Detect left stick Y
                if (!leftYDetected)
                {
                    foreach (var n in leftStickYCandidates)
                    {
                        float v = 0f;
                        try { v = Input.GetAxis(n); } catch { continue; }
                        if (Mathf.Abs(v) > 0.15f)
                        {
                            leftStickYAxis = n;
                            leftYDetected = true;
                            Debug.Log($"SimulatorDriver: auto-detected leftStickYAxis = '{n}' (value={v:F2})");
                            break;
                        }
                    }
                }

                // Detect right stick Y (collective)
                if (!rightYDetected)
                {
                    foreach (var n in rightStickYAxisCandidates)
                    {
                        float v = 0f;
                        try { v = Input.GetAxis(n); } catch { continue; }
                        if (Mathf.Abs(v) > 0.15f)
                        {
                            rightStickYAxis = n;
                            rightYDetected = true;
                            Debug.Log($"SimulatorDriver: auto-detected rightStickYAxis = '{n}' (value={v:F2})");
                            break;
                        }
                    }
                }

                // Detect triggers (look for candidate axes that respond)
                if (!triggersDetected)
                {
                    var found = new System.Collections.Generic.List<string>();
                    foreach (var n in triggerCandidates)
                    {
                        float v = 0f;
                        try { v = Input.GetAxis(n); } catch { continue; }
                        if (Mathf.Abs(v) > 0.05f)
                        {
                            found.Add(n);
                        }
                    }

                    if (found.Count == 1)
                    {
                        // single axis found — assume it's a combined trigger axis; map it to both sides for now
                        leftTriggerAxis = found[0];
                        rightTriggerAxis = found[0];
                        triggersDetected = true;
                        Debug.Log($"SimulatorDriver: auto-detected single trigger axis '{found[0]}', mapping to both triggers");
                    }
                    else if (found.Count >= 2)
                    {
                        // Prefer names containing Left/Right; otherwise use order found
                        string left = null, right = null;
                        foreach (var n in found)
                        {
                            if (left == null && n.ToLower().Contains("left")) left = n;
                            if (right == null && n.ToLower().Contains("right")) right = n;
                        }
                        if (left == null) left = found[0];
                        if (right == null) right = found.Count > 1 ? found[1] : found[0];
                        leftTriggerAxis = left;
                        rightTriggerAxis = right;
                        triggersDetected = true;
                        Debug.Log($"SimulatorDriver: auto-detected triggers: left='{left}', right='{right}'");
                    }
                }

                // If all detections succeeded, mark overall done
                if (leftXDetected && leftYDetected && rightYDetected && triggersDetected)
                {
                    axesAutoDetected = true;
                    Debug.Log("SimulatorDriver: axis auto-detection complete. If mappings are incorrect, set names manually in the Inspector.");
                }
                else
                {
                    // Not yet detected — continue trying on subsequent frames until user moves controls
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("SimulatorDriver: axis auto-detection failed: " + ex.Message);
            }
        }

        // Attempt to call simulator.SetControlInput(entityId, control) or SetControlInput(control) via reflection.
        // If not present, we buffer the last control in LastBufferedControl so other code may read it.
        private void SetControlInputSafe(ControlInput control)
        {
            if (simulator == null)
                return;

            // Try direct strongly-typed call via reflection (may not compile if not present).
            MethodInfo[] methods = simulator.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                if (m.Name == "SetControlInput")
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(ControlInput))
                    {
                        m.Invoke(simulator, new object[] { controlEntityId, control });
                        return;
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ControlInput))
                    {
                        m.Invoke(simulator, new object[] { control });
                        return;
                    }
                }
            }

            // If we get here, direct SetControlInput not found. Buffer locally (LastBufferedControl already set by caller).
            // External simulator core can obtain this driver (via GameObject.Find or linkage) and read LastBufferedControl.
        }

        // Connect to a websocket URL (safe to call even before real package is installed)
        public void Connect(string url)
        {
            websocketClient.Connect(url);
        }

        // Send JSON string to connected websocket client
        public void SendFrameJson(string json)
        {
            if (websocketClient != null && websocketClient.IsConnected)
            {
                websocketClient.Send(json);
            }
        }

        // Send control input as JSON frame using same websocket client
        public void SendControlJson(ControlInput control)
        {
            if (websocketClient == null || !websocketClient.IsConnected)
                return;

            // Manual serialization to avoid dependency on Unity JsonUtility and to match existing frame format
            string json = "{\"type\":\"control\",\"collective\":" + control.collective.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"cyclic_x\":" + control.cyclic_x.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"cyclic_y\":" + control.cyclic_y.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"pedal\":" + control.pedal.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "}";
            websocketClient.Send(json);
        }

        // Simple JSON serializer for Frame to avoid dependency on Unity JsonUtility package
        private string SerializeFrameToJson(Frame frame)
        {
            // Manual serialization: {"timestamp":<num>,"entities":[{...},...]}
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append('{');
            sb.Append("\"timestamp\":");
            sb.Append(frame.timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append("\"entities\":[");
            bool first = true;
            foreach (var e in frame.entities)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"id\":\"").Append(EscapeJsonString(e.id)).Append("\"");
                sb.Append(',');
                sb.Append("\"p\":{");
                sb.Append("\"x\":" + e.p.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
                sb.Append("\"y\":" + e.p.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
                sb.Append("\"z\":" + e.p.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('}');
                sb.Append(',');
                sb.Append("\"q\":{");
                sb.Append("\"x\":" + e.q.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
                sb.Append("\"y\":" + e.q.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
                sb.Append("\"z\":" + e.q.z.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
                sb.Append("\"w\":" + e.q.w.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('}');
                sb.Append(',');
                sb.Append("\"v\":{");
                sb.Append("\"x\":" + e.v.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
                sb.Append("\"y\":" + e.v.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",");
                sb.Append("\"z\":" + e.v.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('}');
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        private string EscapeJsonString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t");
        }
    }
}
