using UnityEngine;

namespace SimCore
{
    // Forwards Say() to both configured outputs — lets FlightCoachAI /
    // FlightTaskCoachAI / FlightCoachTester keep calling a single
    // `output.Say(comment)` while it fans out to both the HUD panel and TTS
    // voice, without either producer needing to know both exist. Explicit
    // concrete-typed fields, not a MonoBehaviour[] array: dragging a
    // GameObject onto a broad-typed field resolves to an arbitrary component
    // of that type on the object, which risks silently grabbing the wrong
    // component when many scripts share one crowded GameObject (bit us once
    // already on MainMenuManager's equivalent array field).
    public class CoachOutputBroadcaster : MonoBehaviour, ICoachOutput
    {
        public HudCoachOutput hudOutput;
        public TtsCoachOutput ttsOutput;

        public void Say(string message)
        {
            hudOutput?.Say(message);
            ttsOutput?.Say(message);
        }
    }
}
