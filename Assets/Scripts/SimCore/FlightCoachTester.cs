using UnityEngine;

namespace SimCore
{
    // Cycles through canned phrases on a timer, without contacting the Claude
    // API — lets you test the ICoachOutput/HUD pipeline for free. Fully
    // independent: only needs an ICoachOutput, no SimulatorDriver and no
    // dependency on FlightCoachAI or BlackBoxRecorder. Toggle this component
    // off (or remove it) when you want the real AI coach running instead.
    public class FlightCoachTester : MonoBehaviour
    {
        public float interval = 8f; // seconds between phrases

        static readonly string[] Phrases =
        {
            "Nice flying, son",
            "Try to be more easy on the controls",
            "Flying a helicopter is like dancing with the air, you got to feel the machine being held by the atmosphere",
            "Stop thinking about what you are doing and start flying"
        };

        private ICoachOutput output;
        private float timer;

        void Awake()
        {
            output = FindObjectOfType<CoachOutputBroadcaster>();
            if (output == null)
                Debug.LogWarning("[FlightCoachTester] No CoachOutputBroadcaster found — nothing to display.");
        }

        void Update()
        {
            if (output == null) return;

            timer += Time.deltaTime;
            if (timer >= interval)
            {
                timer -= interval;
                string phrase = Phrases[Random.Range(0, Phrases.Length)];
                Debug.Log($"[FlightCoachTester] {phrase}");
                output.Say(phrase);
            }
        }
    }
}
