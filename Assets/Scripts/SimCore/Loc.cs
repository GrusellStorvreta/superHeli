using UnityEngine.Localization.Settings;

namespace SimCore
{
    public static class Loc
    {
        const string TableName = "UI";

        static bool Ready =>
            LocalizationSettings.InitializationOperation.IsDone &&
            LocalizationSettings.SelectedLocale != null;

        public static string Get(string key)
        {
            if (!Ready) return key;
            return LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);
        }

        public static string Get(string key, params object[] args)
        {
            if (!Ready) return key;
            return string.Format(LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key), args);
        }
    }
}
