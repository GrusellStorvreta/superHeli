using UnityEngine;

namespace SimCore
{
    // Animator Controller setup:
    //   Parameters:  Trigger "doHeadslap", Trigger "doTalking", Trigger "doClapping"
    //   States:      SittingIdle (default, loop)
    //                Talking    → SittingIdle (has exit time)
    //                Headslap   → SittingIdle (has exit time)
    //                Clapping   → SittingIdle (has exit time)
    //   Transitions: Any State → Headslap  (doHeadslap,  no exit time, can't transition to self)
    //                Any State → Clapping  (doClapping,  no exit time, can't transition to self)
    //                Any State → Talking   (doTalking,   no exit time, can't transition to self)
    //   Apply Root Motion: OFF on all clips

    public class FlightInstructor : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;

        [Header("Clip Lengths (seconds) — match your actual clips")]
        public float headSlapLength = 2.0f;
        public float talkingLength  = 3.5f;
        public float clappingLength = 3.0f;

        [Header("Headslap — flying fast AND low")]
        public float         headSlapMinSpeedKts = 30f;
        public float         headSlapMaxAglFt    = 20f;
        [Range(0f, 1f)]
        public float         headSlapChance      = 0.3f;
        public float         headSlapCooldown    = 10f;

        [Header("Talking — random idle chatter")]
        public Vector2       talkIntervalRange   = new Vector2(15f, 40f);
        [Range(0f, 1f)]
        public float         talkChance          = 0.6f;

        [Header("Clapping — positive events")]
        [Range(0f, 1f)]
        public float         clappingChanceSuccess = 0.7f;   // mission complete
        [Range(0f, 1f)]
        public float         clappingChanceHover   = 0.5f;   // hover task nailed
        [Range(0f, 1f)]
        public float         clappingChanceLanding = 0.6f;   // nice landing

        private SimulatorDriver         _driver;
        private MissionManager          _mission;
        private LandingChecker          _landingChecker;

        private float                   _headSlapCooldown;
        private float                   _talkTimer;
        private float                   _busyTimer;           // non-idle animation playing
        private bool                    _hoverWasComplete;
        private MissionManager.Phase    _prevPhase;

        private const float MsToKts = 1.94384f;
        private const float MToFt   = 3.28084f;

        void Start()
        {
            _driver  = FindObjectOfType<SimulatorDriver>();
            _mission = FindObjectOfType<MissionManager>();
            if (animator == null) animator = GetComponent<Animator>();
            _prevPhase = _mission != null ? _mission.CurrentPhase : MissionManager.Phase.Idle;

            _landingChecker = FindObjectOfType<LandingChecker>();
            if (_landingChecker != null) _landingChecker.OnLanding += OnLanding;

            ScheduleNextTalk();
        }

        void OnDestroy()
        {
            if (_landingChecker != null) _landingChecker.OnLanding -= OnLanding;
        }

        void OnLanding(LandingResult result)
        {
            if (result.success && Random.value < clappingChanceLanding)
                PlayClapping();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _headSlapCooldown -= dt;
            _talkTimer        -= dt;
            _busyTimer        -= dt;

            if (_driver == null) return;

            CheckHeadslap();
            if (_busyTimer <= 0f) CheckTalk();
            CheckMissionEvents();

            if (_mission != null) _prevPhase = _mission.CurrentPhase;
        }

        // --- Condition checks ---

        void CheckHeadslap()
        {
            if (_headSlapCooldown > 0f) return;

            float speedKts = _driver.LastBodyVelocity.magnitude * MsToKts;
            if (speedKts < headSlapMinSpeedKts) return;

            Vector3 pos   = _driver.LastBodyPosition;
            float   aglFt = Mathf.Max(0f, (pos.y - TerrainUtils.GetGroundY(pos)) * MToFt);
            if (aglFt > headSlapMaxAglFt) return;

            if (Random.value < headSlapChance)
                PlayHeadslap();
        }

        void CheckTalk()
        {
            if (_talkTimer > 0f) return;
            if (Random.value < talkChance)
                PlayTalking();
            ScheduleNextTalk();
        }

        void CheckMissionEvents()
        {
            if (_mission == null) return;

            // Mission just succeeded → clapping
            if (_prevPhase == MissionManager.Phase.Running &&
                _mission.CurrentPhase == MissionManager.Phase.Success)
            {
                if (Random.value < clappingChanceSuccess)
                    PlayClapping();
                return;
            }

            // Hover task just completed → clapping
            if (_mission.IsHoverTask && _mission.HoverProgress >= 1f && !_hoverWasComplete)
            {
                _hoverWasComplete = true;
                if (Random.value < clappingChanceHover)
                    PlayClapping();
            }
            else if (!_mission.IsHoverTask || _mission.HoverProgress < 0.9f)
            {
                _hoverWasComplete = false;
            }
        }

        // --- Playback helpers ---

        void PlayHeadslap()
        {
            _headSlapCooldown = headSlapCooldown;
            _busyTimer        = headSlapLength;
            animator?.SetTrigger("doHeadslap");
        }

        void PlayTalking()
        {
            _busyTimer = talkingLength;
            animator?.SetTrigger("doTalking");
        }

        void PlayClapping()
        {
            _busyTimer = clappingLength;
            animator?.SetTrigger("doClapping");
        }

        // --- Public hooks (call these from other scripts if needed) ---

        public void ReactHeadslap() => PlayHeadslap();
        public void ReactClap()     => PlayClapping();
        public void ReactTalk()     => PlayTalking();

        void ScheduleNextTalk()
        {
            _talkTimer = Random.Range(talkIntervalRange.x, talkIntervalRange.y);
        }
    }
}
