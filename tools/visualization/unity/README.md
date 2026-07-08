Unity visualization notes for superHeli (target: Unity 2021 LTS)

Opening the project
- Open Unity Hub, install Unity 2021 LTS, then choose "Add" and point to this repository root (/Users/anders/superHeli).
- Unity will generate project files (Library, Packages, etc.).

Packages added to Packages/manifest.json
- com.unity.mathematics
- com.unity.collections
- com.unity.burst
- com.unity.jobs
- com.unity.nuget.newtonsoft-json (provides Newtonsoft.Json for Unity)

Notes about package versions
- The manifest includes package names only. If Unity fails to resolve versions automatically, open Window -> Package Manager and install the packages manually (search by name).

Controller mapping (Xbox controller) - legacy Input Manager
- The SimulatorDriver (Assets/Scripts/SimCore/SimulatorDriver.cs) implements an Xbox mapping using the legacy Input.GetAxis API. Default axis names (configurable on the component):
  - Left stick vertical -> Collective (leftStickYAxis) default: "Vertical" (mapped to collective in [0,1] by (axis+1)/2; up increases collective)
  - Right stick horizontal -> cyclic_x (rightStickXAxis) default: "RightStickHorizontal" (range -1..1)
  - Right stick vertical -> cyclic_y (rightStickYAxis) default: "RightStickVertical" (range -1..1)
  - Left/Right triggers -> pedal differential (leftTriggerAxis/rightTriggerAxis) defaults: "LeftTrigger" and "RightTrigger". Pedal = RT - LT in [-1,1].

- The driver applies a configurable deadzone and optional exponential smoothing (smoothingTime in seconds). Collective is normalized to [0,1]; cyclic and pedal use [-1,1].

Unity Input Manager setup (legacy)
- Open Edit -> Project Settings -> Input Manager and add axes as needed. For example:
  - Add "RightStickHorizontal" mapped to Joystick Axis 4 (X depends on platform/controller mapping).
  - Add "RightStickVertical" mapped to Joystick Axis 5.
  - Add "LeftTrigger" mapped to Joystick Axis 9 (or as needed) configured to return 0..1 when unpressed -> pressed.
  - Add "RightTrigger" similarly.
- Axis numbering and mapping varies by OS and controller; test in the Editor and adjust the axis "Type" (Joystick Axis) and "Axis" index until the controller moves the expected Input.GetAxis.

Notes and TODOs
- This driver currently uses the legacy Input system. TODO: add Unity Input System support and an inspector switch.
- The Simulator core currently does not expose SetControlInput; the driver attempts to invoke SetControlInput(entityId, ControlInput) or SetControlInput(ControlInput) via reflection. If not found the last control is buffered in SimulatorDriver.LastBufferedControl for external code to read.
- Control frames are also sent over websocket as JSON: {"type":"control","collective":...}

WebSocket client (manual step)
- Recommended: NativeWebSocket (https://github.com/endel/NativeWebSocket) - available via OpenUPM or GitHub. Works on WebGL, Editor, mobile. Install via OpenUPM or add the package from Git URL.
- Alternative: WebSocketSharp (https://github.com/sta/websocket-sharp) - not available on UPM by default; import the DLL or source into Assets/Plugins.

Protobuf / gRPC
- protobuf-net: there are Unity-compatible builds (protobuf-net.Unity3D) and instructions on the protobuf-net repo. Alternatively use Google.Protobuf; Google.Protobuf has known compatibility caveats with Unity's Mono.
- Recommendation: build a .NET Standard 2.0 library for serialization (protobuf-net or Google.Protobuf) and include the compiled DLL via Assets/Plugins or use an asmdef referencing the DLL.

.NET compatibility notes
- Unity 2021 LTS uses the Mono/.NET framework compatibility level equivalent to .NET Standard 2.0. Do not assume APIs from .NET 5/6/7 are available.
- For shared simulator core libraries compiled outside Unity: target .NET Standard 2.0 to be compatible with Unity 2021.
- Use Unity asmdef files to isolate assemblies and set API compatibility. Avoid heavy use of reflection or unsupported runtime features.

Assembly definition
- A SimCore.asmdef is included at Assets/Scripts/SimCore/SimCore.asmdef to isolate core simulation code.

Build / Run tips
- Add Scenes under Assets/Scenes and mark them in Build Settings before building.
- For editor-time testing, create lightweight MonoBehaviour wrappers that instantiate SimCore.Simulator and drive Step(dt) from Update().
- Use the Burst and Jobs packages only after confirming code is compatible (Burst requires burst-compatible types and structs).

Manual import summary (these require manual action)
- NativeWebSocket or websocket-sharp: import via OpenUPM, Git URL, or copy DLL/source.
- protobuf-net / Google.Protobuf: include Unity-compatible DLL or follow repo instructions.

Support
- If you need, I can add example MonoBehaviours to hook the Simulator into the Unity scene and a sample scene demonstrating a running Step loop.
- Editor menu: open Unity and use Tools -> Create Sample Scene. That menu item will programmatically create Assets/Scenes/SampleScene.unity containing a GameObject named "Simulator" with the SimulatorDriver component. Do not commit .unity binary scene files to the repo; generate the scene from the Editor menu.
