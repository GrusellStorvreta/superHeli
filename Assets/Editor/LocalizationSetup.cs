using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

public static class LocalizationSetup
{
    const string TableName  = "UI";
    const string AssetDir   = "Assets/Localization";
    const string LocaleDir  = "Assets/Localization/Locales";

    static readonly Dictionary<string, string> EnglishStrings = new Dictionary<string, string>
    {
        // Main menu
        { "menu.title",         "SUPERHELI" },
        { "menu.subtitle",      "HELICOPTER SIMULATOR" },
        { "menu.free_flight",   "FREE FLIGHT" },
        { "menu.missions",      "MISSION MODE" },
        { "menu.settings",      "SETTINGS" },
        { "menu.quit",          "QUIT" },
        { "menu.back",          "← BACK" },
        { "menu.sound_on",      "SOUND   ON" },
        { "menu.sound_off",     "SOUND   OFF" },
        { "menu.volume",        "VOLUME   {0}%" },
        { "menu.level",         "LEVEL {0}" },

        // Pause menu
        { "pause.title",        "PAUSED" },
        { "pause.resume",       "RESUME" },
        { "pause.main_menu",    "MAIN MENU" },

        // Mission result screen
        { "result.complete",    "MISSION COMPLETE" },
        { "result.timesup",     "TIME'S UP" },
        { "result.failed",      "MISSION FAILED" },
        { "result.time",        "Completed in  {0:F1} sec" },
        { "result.continue",    "CONTINUE  →  LEVEL {0}" },
        { "result.back",        "BACK TO MENU" },
        { "result.try_again",   "TRY AGAIN" },

        // Crash / landing overlay
        { "crash.text",         "CRASH!" },
        { "crash.nice_landing", "Nice landing!" },

        // Mission HUD
        { "hud.in_zone",        "IN ZONE" },
        { "hud.out_of_zone",    "OUT OF ZONE — hold position" },

        // Mission instructions
        { "instr.climb_100",    "Climb to 100 ft AGL" },
        { "instr.hover_100",    "Hover at 100 ft  (±3 ft)  for 5 sec" },
        { "instr.land",         "Land the helicopter" },
        { "instr.climb_200",    "Climb to 200 ft AGL" },
        { "instr.hover_200",    "Hover at 200 ft  (±5 ft)  for 3 sec" },
        { "instr.checkpoint_1", "Fly through Checkpoint 1" },
        { "instr.checkpoint_2", "Fly through Checkpoint 2  (turn around!)" },
        { "instr.course",       "Fly through all {0} checkpoints!" },
        { "instr.climb_50",     "Climb to 50 ft AGL" },
        { "instr.rescue_zone",  "Fly to rescue zone" },
        { "instr.land_pickup",  "Land and pick up survivors" },
        { "instr.return_base",  "Return to base" },
    };

    [MenuItem("Window/SuperHeli/Setup Localization Tables")]
    public static void Run()
    {
        System.IO.Directory.CreateDirectory(LocaleDir);

        // Create or find English locale
        var locale = LocalizationEditorSettings.GetLocale(SystemLanguage.English);
        if (locale == null)
        {
            locale = Locale.CreateLocale(SystemLanguage.English);
            AssetDatabase.CreateAsset(locale, $"{LocaleDir}/English.asset");
            LocalizationEditorSettings.AddLocale(locale);
            Debug.Log("[LocalizationSetup] Created English locale.");
        }

        // Create or find the "UI" string table collection
        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null)
        {
            collection = LocalizationEditorSettings.CreateStringTableCollection(
                TableName, AssetDir, new List<Locale> { locale });
            Debug.Log("[LocalizationSetup] Created 'UI' string table collection.");
        }

        var table = collection.GetTable(locale.Identifier) as StringTable;
        if (table == null)
        {
            table = collection.AddNewTable(locale.Identifier) as StringTable;
            Debug.Log("[LocalizationSetup] Added English table to existing collection.");
        }

        foreach (var kv in EnglishStrings)
        {
            if (table.GetEntry(kv.Key) == null)
                table.AddEntry(kv.Key, kv.Value);
        }

        EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(table.SharedData);
        AssetDatabase.SaveAssets();

        Debug.Log($"[LocalizationSetup] Done — {EnglishStrings.Count} entries in '{TableName}' (English).");
    }
}
