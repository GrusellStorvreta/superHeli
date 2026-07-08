# Basher controller mapping decision

Timestamp: 2026-06-14T16:43:25Z

Author: Basher (automated changes by Copilot)

Summary
- Implemented Xbox controller mapping in Unity SimulatorDriver using legacy Input.GetAxis.

Axis names (defaults in SimulatorDriver component):
- leftStickYAxis = "Vertical"            -> Left stick vertical
- rightStickXAxis = "RightStickHorizontal" -> Right stick horizontal
- rightStickYAxis = "RightStickVertical"   -> Right stick vertical
- leftTriggerAxis = "LeftTrigger"         -> Left trigger
- rightTriggerAxis = "RightTrigger"       -> Right trigger

Mapping decisions
- Collective: mapped from left stick Y. Raw axis [-1..1] -> normalized collective in [0..1] by (raw + 1) / 2. Chosen so that pushing stick up increases collective (up -> 1.0). Documented in README.
- Cyclic: right stick X/Y mapped directly to cyclic_x and cyclic_y in [-1..1]. cyclic_x is roll-like, cyclic_y is pitch-like. No sign inversion applied by default (caller can invert axis in Input Manager or adjust component fields).
- Pedal: computed as differential pedal = RT - LT. We expect trigger axes to be 0..1 when configured in Input Manager; result is in [-1..1] where positive means RT>LT (yaw right). Optionally the team may expose separate left/right pedal axes if desired.

Smoothing & deadzone
- Deadzone applied (default 0.05) to cyclic and pedal around zero; collective deadzone around midpoint 0.5.
- Exponential smoothing implemented with a public smoothingTime (seconds). Alpha computed as stepDt / (smoothingTime + stepDt). Set smoothingTime = 0 for no smoothing.

Simulator wiring
- Driver attempts to call simulator.SetControlInput(entityId, ControlInput) or simulator.SetControlInput(ControlInput) via reflection. If not found the driver buffers the latest ControlInput in SimulatorDriver.LastBufferedControl for the simulator core or other components to read.

Websocket
- ControlInput frames are sent as JSON: {"type":"control","collective":...,"cyclic_x":...,"cyclic_y":...,"pedal":...} using existing websocket client.

Files changed
- Assets/Scripts/SimCore/SimulatorDriver.cs: added ControlInput class, mapping, smoothing, reflection shim, websocket send helper.
- tools/visualization/unity/README.md: documented mappings and Input Manager setup.

Notes / TODO
- This initial implementation uses the legacy Input system (Input.GetAxis). Add an inspector switch and Input System (new) support in a follow-up change.
- The simulator core (Rusty) should implement SetControlInput(entityId, ControlInput) and actuator lag (first-order low-pass) per team directive.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
