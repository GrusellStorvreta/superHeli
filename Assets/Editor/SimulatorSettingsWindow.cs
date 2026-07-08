#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class SimulatorSettingsWindow : EditorWindow
{
    private string websocketUrl;
    private bool autoConnect;

    [MenuItem("Tools/Simulator Settings")]
    public static void ShowWindow()
    {
        GetWindow<SimulatorSettingsWindow>("Simulator Settings");
    }

    void OnEnable()
    {
        websocketUrl = EditorPrefs.GetString("SimCore.WebSocketUrl", "ws://localhost:8765");
        autoConnect = EditorPrefs.GetBool("SimCore.AutoConnect", false);
    }

    void OnGUI()
    {
        GUILayout.Label("Simulator WebSocket Settings", EditorStyles.boldLabel);
        websocketUrl = EditorGUILayout.TextField("WebSocket URL", websocketUrl);
        autoConnect = EditorGUILayout.Toggle("Auto-connect in Editor", autoConnect);

        EditorGUILayout.Space();

        if (GUILayout.Button("Save"))
        {
            EditorPrefs.SetString("SimCore.WebSocketUrl", websocketUrl);
            EditorPrefs.SetBool("SimCore.AutoConnect", autoConnect);
            Debug.Log("Simulator settings saved.");
        }

        if (GUILayout.Button("Connect Now (selected scene objects)"))
        {
            // Connect all SimulatorDriver instances in loaded scenes
            var drivers = UnityEngine.Object.FindObjectsOfType<SimCore.SimulatorDriver>();
            foreach (var d in drivers)
            {
                if (d != null)
                {
                    d.Connect(websocketUrl);
                    Debug.Log($"Requested Connect on SimulatorDriver '{d.gameObject.name}'");
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Set the WebSocket URL used by SimulatorDriver and enable auto-connect in the Editor. Auto-connect will attempt to connect to all SimulatorDriver instances that have 'runInEditor' enabled.", MessageType.Info);
    }
}
#endif