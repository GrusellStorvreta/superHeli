using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[InitializeOnLoad]
public static class EventSystemFixer
{
    static EventSystemFixer()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            FixAll();
    }

    [MenuItem("Window/SuperHeli/Fix EventSystem Input")]
    public static void FixAll()
    {
        int fixed_ = 0;
        foreach (var standalone in Object.FindObjectsOfType<StandaloneInputModule>())
        {
            var go = standalone.gameObject;
            Object.DestroyImmediate(standalone);
            go.AddComponent<InputSystemUIInputModule>();
            fixed_++;
        }
        if (fixed_ > 0)
            Debug.Log($"[EventSystemFixer] Replaced {fixed_} StandaloneInputModule(s) with InputSystemUIInputModule.");
    }
}
