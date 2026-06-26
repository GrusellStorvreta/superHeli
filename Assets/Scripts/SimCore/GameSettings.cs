namespace SimCore
{
    public static class GameSettings
    {
        public enum Mode { FreeFlight, Mission }
        public static Mode CurrentMode  = Mode.FreeFlight;
        public static int  CurrentLevel = 1;

        public static int UnlockedLevels
        {
            get => UnityEngine.Mathf.Max(UnityEngine.PlayerPrefs.GetInt("UnlockedLevels", 1), 7);
            set { UnityEngine.PlayerPrefs.SetInt("UnlockedLevels", value); UnityEngine.PlayerPrefs.Save(); }
        }

        public static bool SoundEnabled
        {
            get => UnityEngine.PlayerPrefs.GetInt("SoundEnabled", 1) == 1;
            set { UnityEngine.PlayerPrefs.SetInt("SoundEnabled", value ? 1 : 0); UnityEngine.PlayerPrefs.Save(); }
        }

        public static float SoundVolume
        {
            get => UnityEngine.PlayerPrefs.GetFloat("SoundVolume", 1f);
            set { UnityEngine.PlayerPrefs.SetFloat("SoundVolume", UnityEngine.Mathf.Clamp01(value)); UnityEngine.PlayerPrefs.Save(); }
        }

        public static void ApplyAudioSettings()
        {
            UnityEngine.AudioListener.volume = SoundEnabled ? SoundVolume : 0f;
        }
    }
}
