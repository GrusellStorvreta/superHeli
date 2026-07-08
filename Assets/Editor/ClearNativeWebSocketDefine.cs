#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ClearNativeWebSocketDefine
{
    static ClearNativeWebSocketDefine()
    {
        EditorApplication.delayCall += RemoveDefine;
    }

    private static void RemoveDefine()
    {
        const string symbol = "NATIVEWEBSOCKET";
        foreach (BuildTargetGroup group in System.Enum.GetValues(typeof(BuildTargetGroup)))
        {
            if (group == BuildTargetGroup.Unknown) continue;
            try
            {
                var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group) ?? string.Empty;
                var list = new System.Collections.Generic.List<string>(defines.Split(new char[] {';'}, System.StringSplitOptions.RemoveEmptyEntries));
                if (list.Contains(symbol))
                {
                    list.RemoveAll(s => s == symbol);
                    string newDefines = string.Join(";", list.ToArray());
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newDefines);
                    Debug.Log($"[ClearNativeWebSocketDefine] Removed '{symbol}' from {group}");
                }
            }
            catch { }
        }
    }
}
#endif