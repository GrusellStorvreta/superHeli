namespace SimCore
{
    // Level-specific components implement this to restore their start-of-mission
    // state. MissionManager broadcasts OnLevelReset() on mission start and after
    // a crash, reaching inactive objects too — they never get Awake/OnEnable,
    // so they cannot subscribe to CrashHandler.OnCrashReset themselves.
    public interface ILevelResetHandler
    {
        void OnLevelReset();
    }
}
