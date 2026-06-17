#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

[InitializeOnLoad]
public static class AutoConnectEditor
{
    // Track which drivers we've attempted to connect this editor session (by instanceID)
    private static System.Collections.Generic.HashSet<int> attempted = new System.Collections.Generic.HashSet<int>();

    static AutoConnectEditor()
    {
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        // Only act when not playing
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        bool auto = EditorPrefs.GetBool("SimCore.AutoConnect", false);
        if (!auto)
            return;

        string url = EditorPrefs.GetString("SimCore.WebSocketUrl", "");
        if (string.IsNullOrEmpty(url))
            return;

        // Find all SimulatorDriver instances in loaded scenes
        var drivers = UnityEngine.Object.FindObjectsOfType<SimCore.SimulatorDriver>();
        foreach (var d in drivers)
        {
            if (d == null)
                continue;

            int id = d.GetInstanceID();
            if (attempted.Contains(id))
                continue;

            try
            {
                d.Connect(url);
                attempted.Add(id);
                Debug.Log($"AutoConnect: requested Connect() on SimulatorDriver '{d.gameObject.name}' to {url}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("AutoConnect: failed to call Connect on SimulatorDriver: " + ex.Message);
            }
        }
    }
}
#endif