using UnityEngine.Localization.Settings;

namespace SimCore
{
    public static class Loc
    {
        const string TableName = "UI";

        public static string Get(string key)
        {
            if (!LocalizationSettings.InitializationOperation.IsDone) return key;
            return LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);
        }

        public static string Get(string key, params object[] args)
        {
            if (!LocalizationSettings.InitializationOperation.IsDone) return key;
            return string.Format(LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key), args);
        }
    }
}
