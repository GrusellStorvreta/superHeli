namespace SimCore
{
    public static class GameSettings
    {
        public enum Mode { FreeFlight, Mission }
        public static Mode CurrentMode  = Mode.FreeFlight;
        public static int  CurrentLevel = 1;

        public static int UnlockedLevels
        {
            get => UnityEngine.PlayerPrefs.GetInt("UnlockedLevels", 1);
            set
            {
                UnityEngine.PlayerPrefs.SetInt("UnlockedLevels", value);
                UnityEngine.PlayerPrefs.Save();
            }
        }
    }
}
