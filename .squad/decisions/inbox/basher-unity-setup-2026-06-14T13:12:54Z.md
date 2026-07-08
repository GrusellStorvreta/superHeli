timestamp: 2026-06-14T13:12:54Z

action: Created minimal Unity project scaffold for superHeli targeting Unity 2021 LTS.

what I changed:
- Packages/manifest.json: added recommended UPM package entries (mathematics, collections, burst, jobs, nuget.newtonsoft-json).
- Assets/Scripts/SimCore/Simulator.cs: placeholder Simulator, Frame, EntitySnapshot, SimulatorConfig classes (Unity-safe types, no advanced APIs).
- Assets/Scripts/SimCore/SimCore.asmdef: assembly definition (assemblyName: "SimCore").
- tools/visualization/unity/README.md: instructions for opening project in Unity 2021 LTS, package notes, WebSocket and protobuf import guidance, .NET compatibility notes, and build/run tips.

manual imports required:
- NativeWebSocket (recommended) or websocket-sharp: not reliably available via UPM in all registries. Install via OpenUPM or import source/DLL from their GitHub.
- protobuf-net / Google.Protobuf: Unity-compatible DLL or package must be imported manually; prefer building a .NET Standard 2.0 DLL and placing it in Assets/Plugins.

notes on runtime constraints:
- Unity 2021 LTS uses Mono/.NET compatibility (approx .NET Standard 2.0). The team decision to use .NET 7 for simulator core (outside Unity) is fine, but any code intended to run inside Unity must target .NET Standard 2.0 and avoid APIs introduced after .nStandard 2.0/Mono.
- Use asmdef files to separate Unity-facing code and limit API surface.

If further changes are desired, I can add example MonoBehaviour adapters and a sample scene to demonstrate the Simulator stepping loop.
