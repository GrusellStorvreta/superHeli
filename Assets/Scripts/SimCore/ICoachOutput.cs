namespace SimCore
{
    // Seam between the AI coach's analysis and however its comments reach the
    // player — a HUD panel today, text-to-speech later, with no change needed
    // on the analysis side when that swap happens.
    public interface ICoachOutput
    {
        void Say(string message);
    }
}
