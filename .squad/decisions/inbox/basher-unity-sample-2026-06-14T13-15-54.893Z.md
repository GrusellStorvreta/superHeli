# Basher Unity sample: created files

UTC timestamp: 2026-06-14T13:15:54.893Z

Created files:

- Assets/Scripts/SimCore/SimulatorDriver.cs
- Assets/Editor/SimSceneCreator.cs
- tools/visualization/unity/README.md (updated note)

Notes:
- A sample scene binary is NOT added to the repository. Instead, run the Editor menu item (Tools -> Create Sample Scene) in Unity to programmatically create and save the scene at Assets/Scenes/SampleScene.unity. The menu creates a GameObject named "Simulator" with the SimulatorDriver component.
- Networking/WebSocket hooks are intentionally left as commented placeholders; import a Unity-compatible WebSocket package (NativeWebSocket or websocket-sharp) and implement connections in SimulatorDriver if needed.
