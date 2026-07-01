using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimCore
{
    public class MissionManager : MonoBehaviour
    {
        public SimulatorDriver driver;

        [Header("Level")]
        [Tooltip("Default level when starting directly from the Editor (overridden at runtime by MainMenuManager)")]
        public int              defaultLevelNumber = 1;

        [Header("Level 2")]
        public CheckpointRing[] level2Checkpoints;
        public Transform[]      level2HoverAnchors;  // size 3: hover1, hover2, hover3
        public HoverZoneMarker  hoverZoneMarker;

        [Header("Level 7")]
        public Transform             rescueZoneTransform;
        public Transform             baseTransform;
        public RescueNPC[]           rescueNPCs;
        public GameObject            level7NPCParent;
        public Level7NPCController   level7NPCs;

        [Header("Checkpoint Courses (Levels 3–6)")]
        public CheckpointCourse[] courses;

        [Header("Result Screen")]
        public MissionResultScreen resultScreen;

        // -------------------------------------------------------

        [Serializable]
        public class CheckpointCourse
        {
            public string          courseName;
            public int             levelNumber;
            public CheckpointRing[] rings;
            public float           timeLimit    = 90f;
            public int             visibleAhead = 3;
        }

        // -------------------------------------------------------

        public enum Phase { Idle, Running, Success, Failed }
        public Phase    CurrentPhase       { get; private set; } = Phase.Idle;
        public float    TimeRemaining      { get; private set; }
        public float    HoverProgress      { get; private set; }
        public bool     HoverInZone        { get; private set; }
        public bool     IsHoverTask        { get; private set; }
        public string   CurrentInstruction { get; private set; } = "";
        public float    FinalTime          { get; private set; }
        public Vector3? NavigationTarget   { get; private set; }

        private const float MToFt = 3.28084f;

        // --- Level data ---

        struct LevelEnvironment
        {
            public float windSpeed;
            public float windDirectionDeg;
            public float windTurbulence;
            public float windPulseMagnitude;
            public float windPulseFrequency;
        }

        class TaskDef
        {
            public enum Kind { ClimbToAGL, HoverAtAGL, FlyThrough, Land, NavigateTo, PickupNPCs, CourseRun }
            public Kind           kind;
            public float          targetAglFt;
            public float          hoverDuration;
            public float          maxDeviationFt;
            public CheckpointRing checkpoint;
            public string         instruction;
            public float          radiusM;
            public Transform      targetTransform;
            public Transform      hoverAnchorTransform;
            public bool           showHoverMarker = true;
            public float          taskTimeLimit   = 0f;  // 0 = use mission-wide timeLimit
        }

        private TaskDef[] tasks;
        private float     timeLimit;

        // --- Runtime state ---

        private Dictionary<int, Action> _levelBuilders;
        private LevelEnvironment        _environment;
        private bool                    _initializedExternally;
        private int                     taskIdx;
        private float                   hoverTimer;
        private Vector3                 hoverAnchor;
        private bool                    landingReceived;
        private bool                    _checkpointPassed;
        private LandingChecker          landingChecker;

        // Course run state
        private CheckpointCourse _activeCourse;
        private int               _courseRingIdx;
        private bool              _courseComplete;
        private float             _completionPauseTimer;

        private CrashHandler _crashHandler;

        // -------------------------------------------------------

        void Start()
        {
            if (_initializedExternally) return; // Initialize() already called — skip to avoid double-init
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            landingChecker = FindObjectOfType<LandingChecker>();
            if (landingChecker != null) landingChecker.OnLanding += OnLanding;
            _crashHandler = FindObjectOfType<CrashHandler>();
            if (_crashHandler != null) _crashHandler.OnCrashReset += HandleCrashReset;
            BuildLevel();
            StartMission();
        }

        public void Initialize(int level)
        {
            _initializedExternally = true;
            defaultLevelNumber = level;
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            if (landingChecker == null)
            {
                landingChecker = FindObjectOfType<LandingChecker>();
                if (landingChecker != null) landingChecker.OnLanding += OnLanding;
            }
            if (_crashHandler == null)
            {
                _crashHandler = FindObjectOfType<CrashHandler>();
                if (_crashHandler != null) _crashHandler.OnCrashReset += HandleCrashReset;
            }
            BuildLevel();
            StartMission();
        }

        void BuildLevel()
        {
            if (_levelBuilders == null)
            {
                _levelBuilders = new Dictionary<int, Action>
                {
                    { 1, BuildLevel1 },
                    { 2, BuildLevel2 },
                    { 7, BuildLevel7 },
                };

                if (courses != null)
                    foreach (var course in courses)
                    {
                        var c = course;
                        _levelBuilders[c.levelNumber] = () => BuildLevelCourse(c);
                    }
            }

            if (!_levelBuilders.TryGetValue(defaultLevelNumber, out var build))
                build = BuildLevel1;

            build();
        }

        // -------------------------------------------------------
        // Level builders
        // -------------------------------------------------------

        void BuildLevel1()
        {
            _environment = new LevelEnvironment { windSpeed = 0f };
            timeLimit    = 30f;
            tasks        = new TaskDef[]
            {
                new TaskDef { kind = TaskDef.Kind.ClimbToAGL, targetAglFt = 100f,
                              instruction = "Climb to 100 ft AGL" },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 100f,
                              hoverDuration = 5f, maxDeviationFt = 3f,
                              instruction   = "Hover at 100 ft  (±3 ft)  for 5 sec" },
                new TaskDef { kind = TaskDef.Kind.Land,
                              instruction = "Land the helicopter" },
            };
        }

        void BuildLevel2()
        {
            _environment = new LevelEnvironment { windSpeed = 0f };
            timeLimit    = 120f;

            var ring1   = level2Checkpoints != null && level2Checkpoints.Length > 0 ? level2Checkpoints[0] : null;
            var ring2   = level2Checkpoints != null && level2Checkpoints.Length > 1 ? level2Checkpoints[1] : null;
            var anchor1 = level2HoverAnchors != null && level2HoverAnchors.Length > 0 ? level2HoverAnchors[0] : null;
            var anchor2 = level2HoverAnchors != null && level2HoverAnchors.Length > 1 ? level2HoverAnchors[1] : null;
            var anchor3 = level2HoverAnchors != null && level2HoverAnchors.Length > 2 ? level2HoverAnchors[2] : null;

            tasks = new TaskDef[]
            {
                new TaskDef { kind = TaskDef.Kind.ClimbToAGL, targetAglFt = 200f,
                              taskTimeLimit = 30f,
                              instruction = "Climb to 200 ft AGL" },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              hoverAnchorTransform = anchor1,
                              taskTimeLimit = 20f,
                              instruction   = "Hover at 200 ft  (±5 ft)  for 3 sec" },
                new TaskDef { kind = TaskDef.Kind.FlyThrough, checkpoint = ring1,
                              taskTimeLimit = 45f,
                              instruction = "Fly through Checkpoint 1" },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              hoverAnchorTransform = anchor2,
                              taskTimeLimit = 20f,
                              instruction   = "Hover at 200 ft  (±5 ft)  for 3 sec" },
                new TaskDef { kind = TaskDef.Kind.FlyThrough, checkpoint = ring2,
                              taskTimeLimit = 45f,
                              instruction = "Fly through Checkpoint 2  (turn around!)" },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              hoverAnchorTransform = anchor3,
                              taskTimeLimit = 20f,
                              instruction   = "Hover at 200 ft  (±5 ft)  for 3 sec" },
                new TaskDef { kind = TaskDef.Kind.Land,
                              taskTimeLimit = 30f,
                              instruction = "Land the helicopter" },
            };
        }

        void BuildLevelCourse(CheckpointCourse course)
        {
            _environment  = new LevelEnvironment { windSpeed = 0f };
            timeLimit     = course.timeLimit;
            _activeCourse = course;

            int ringCount = course.rings?.Length ?? 0;
            tasks = new TaskDef[]
            {
                new TaskDef { kind = TaskDef.Kind.CourseRun,
                              instruction = $"Fly through all {ringCount} checkpoints!" },
                new TaskDef { kind = TaskDef.Kind.Land,
                              instruction = "Land the helicopter" },
            };
        }

        void BuildLevel7()
        {
            _environment = new LevelEnvironment
            {
                windSpeed          = 4f,
                windDirectionDeg   = 210f,
                windTurbulence     = 0.4f,
                windPulseMagnitude = 0.2f,
                windPulseFrequency = 0.5f,
            };
            timeLimit = 120f;
            tasks = new TaskDef[]
            {
                new TaskDef { kind = TaskDef.Kind.ClimbToAGL, targetAglFt = 50f,
                              instruction = "Climb to 50 ft AGL" },
                new TaskDef { kind = TaskDef.Kind.NavigateTo, targetTransform = rescueZoneTransform,
                              radiusM = 100f, instruction = "Fly to rescue zone" },
                new TaskDef { kind = TaskDef.Kind.PickupNPCs,
                              instruction = "Land and pick up survivors" },
                new TaskDef { kind = TaskDef.Kind.NavigateTo, targetTransform = baseTransform,
                              radiusM = 80f, instruction = "Return to base" },
                new TaskDef { kind = TaskDef.Kind.Land,
                              instruction = "Land the helicopter" },
            };
        }

        // -------------------------------------------------------
        // Course run — sliding window
        // -------------------------------------------------------

        void StartCourseRun()
        {
            if (_activeCourse == null || _activeCourse.rings == null) return;

            UnsubscribeFromCurrentCourseRing();
            _courseRingIdx  = 0;
            _courseComplete = false;

            foreach (var r in _activeCourse.rings)
                if (r != null) r.gameObject.SetActive(false);

            ShowCourseWindow();
            SubscribeToCurrentCourseRing();
            RefreshInstruction();
        }

        void ShowCourseWindow()
        {
            if (_activeCourse == null || _activeCourse.rings == null) return;
            int end = Mathf.Min(_courseRingIdx + _activeCourse.visibleAhead, _activeCourse.rings.Length);
            for (int i = _courseRingIdx; i < end; i++)
            {
                var r = _activeCourse.rings[i];
                if (r == null) continue;
                r.gameObject.SetActive(true); // SetActive first so Awake() runs before ResetRing
                r.ResetRing();
            }
        }

        void SubscribeToCurrentCourseRing()
        {
            if (_activeCourse == null || _courseRingIdx >= _activeCourse.rings.Length) return;
            var ring = _activeCourse.rings[_courseRingIdx];
            if (ring != null) ring.OnPassedThrough += OnCourseRingPassed;
        }

        void UnsubscribeFromCurrentCourseRing()
        {
            if (_activeCourse == null || _courseRingIdx >= _activeCourse.rings.Length) return;
            var ring = _activeCourse.rings[_courseRingIdx];
            if (ring != null) ring.OnPassedThrough -= OnCourseRingPassed;
        }

        void OnCourseRingPassed()
        {
            if (_activeCourse == null) return;

            // Hide the ring just passed
            _activeCourse.rings[_courseRingIdx]?.gameObject.SetActive(false);

            _courseRingIdx++;

            if (_courseRingIdx >= _activeCourse.rings.Length)
            {
                _courseComplete       = true;
                _completionPauseTimer = 2.5f;
                RefreshInstruction();
                return;
            }

            // Reveal the next ring at the far end of the window
            int newTail = _courseRingIdx + _activeCourse.visibleAhead - 1;
            if (newTail < _activeCourse.rings.Length && _activeCourse.rings[newTail] != null)
            {
                _activeCourse.rings[newTail].ResetRing();
                _activeCourse.rings[newTail].gameObject.SetActive(true);
            }

            SubscribeToCurrentCourseRing();
            RefreshInstruction();
            UpdateNavigationTarget();
        }

        // -------------------------------------------------------
        // Mission flow
        // -------------------------------------------------------

        void StartMission()
        {
            ApplyEnvironment(_environment);

            if (level2Checkpoints != null)
                foreach (var r in level2Checkpoints)
                    if (r != null) r.gameObject.SetActive(false);

            taskIdx           = 0;
            TimeRemaining     = tasks.Length > 0 && tasks[0].taskTimeLimit > 0
                                    ? tasks[0].taskTimeLimit
                                    : timeLimit;
            hoverTimer        = 0f;
            HoverProgress     = 0f;
            HoverInZone       = false;
            IsHoverTask       = false;
            landingReceived   = false;
            _checkpointPassed = false;
            _courseComplete   = false;
            CurrentPhase      = Phase.Running;
            hoverZoneMarker?.gameObject.SetActive(false);

            // Initialize first task if it's a course run
            if (tasks.Length > 0 && tasks[0].kind == TaskDef.Kind.CourseRun)
                StartCourseRun();

            RefreshInstruction();
            UpdateNavigationTarget();
        }

        void Update()
        {
            if (CurrentPhase != Phase.Running) return;
            if (driver == null) { driver = FindObjectOfType<SimulatorDriver>(); return; }

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Fail();
                return;
            }

            EvaluateTask();
        }

        void EvaluateTask()
        {
            var t   = tasks[taskIdx];
            var pos = driver.LastBodyPosition;
            float agl = GetAGL(pos);

            IsHoverTask = t.kind == TaskDef.Kind.HoverAtAGL;

            switch (t.kind)
            {
                case TaskDef.Kind.ClimbToAGL:
                    if (agl >= t.targetAglFt)
                        AdvanceTask(pos);
                    break;

                case TaskDef.Kind.HoverAtAGL:
                    float altDev   = Mathf.Abs(agl - t.targetAglFt);
                    float horizDev = new Vector2(pos.x - hoverAnchor.x,
                                                 pos.z - hoverAnchor.z).magnitude * MToFt;
                    HoverInZone = altDev <= t.maxDeviationFt && horizDev <= t.maxDeviationFt;
                    hoverZoneMarker?.SetInZone(HoverInZone);

                    if (HoverInZone)
                    {
                        hoverTimer   += Time.deltaTime;
                        HoverProgress = Mathf.Clamp01(hoverTimer / t.hoverDuration);
                        if (hoverTimer >= t.hoverDuration)
                            AdvanceTask(pos);
                    }
                    else
                    {
                        hoverTimer    = 0f;
                        HoverProgress = 0f;
                    }
                    break;

                case TaskDef.Kind.FlyThrough:
                    if (_checkpointPassed)
                        AdvanceTask(pos);
                    break;

                case TaskDef.Kind.CourseRun:
                    if (_courseComplete)
                    {
                        _completionPauseTimer -= Time.deltaTime;
                        if (_completionPauseTimer <= 0f)
                            AdvanceTask(pos);
                    }
                    break;

                case TaskDef.Kind.Land:
                    if (landingReceived)
                        Succeed();
                    break;

                case TaskDef.Kind.NavigateTo:
                    if (t.targetTransform != null)
                    {
                        float dist = Vector3.Distance(pos, t.targetTransform.position);
                        if (dist <= t.radiusM) AdvanceTask(pos);
                    }
                    break;

                case TaskDef.Kind.PickupNPCs:
                    if (level7NPCs != null && level7NPCs.IsComplete) Succeed();
                    else if (rescueNPCs != null && AllBoarded()) AdvanceTask(pos);
                    break;
            }
        }

        bool AllBoarded()
        {
            foreach (var npc in rescueNPCs)
                if (npc == null || !npc.IsBoarded) return false;
            return true;
        }

        void AdvanceTask(Vector3 pos)
        {
            var prev = tasks[taskIdx];

            // Clean up outgoing task
            if (prev.kind == TaskDef.Kind.FlyThrough && prev.checkpoint != null)
                prev.checkpoint.OnPassedThrough -= OnCheckpointPassed;

            if (prev.kind == TaskDef.Kind.HoverAtAGL)
                hoverZoneMarker?.gameObject.SetActive(false);

            taskIdx++;
            if (taskIdx >= tasks.Length) { Succeed(); return; }

            var next = tasks[taskIdx];

            // Set up incoming task
            if (next.kind == TaskDef.Kind.HoverAtAGL)
            {
                hoverAnchor = next.hoverAnchorTransform != null
                    ? new Vector3(next.hoverAnchorTransform.position.x, pos.y,
                                  next.hoverAnchorTransform.position.z)
                    : pos;

                if (hoverZoneMarker != null && next.showHoverMarker)
                {
                    hoverZoneMarker.Place(hoverAnchor, next.targetAglFt, next.maxDeviationFt);
                    hoverZoneMarker.gameObject.SetActive(true);
                }
            }

            if (next.kind == TaskDef.Kind.FlyThrough && next.checkpoint != null)
            {
                next.checkpoint.gameObject.SetActive(true);
                next.checkpoint.ResetRing();
                _checkpointPassed = false;
                next.checkpoint.OnPassedThrough += OnCheckpointPassed;
            }

            if (next.kind == TaskDef.Kind.CourseRun)
                StartCourseRun();

            if (next.kind == TaskDef.Kind.PickupNPCs && level7NPCParent != null)
                level7NPCParent.SetActive(true);

            hoverTimer      = 0f;
            HoverProgress   = 0f;
            landingReceived = false;
            if (next.taskTimeLimit > 0) TimeRemaining = next.taskTimeLimit;
            RefreshInstruction();
            UpdateNavigationTarget();
        }

        void UpdateNavigationTarget()
        {
            if (taskIdx >= tasks.Length) { NavigationTarget = null; return; }
            var t = tasks[taskIdx];

            switch (t.kind)
            {
                case TaskDef.Kind.NavigateTo:
                    NavigationTarget = t.targetTransform != null
                        ? (Vector3?)t.targetTransform.position : null;
                    break;
                case TaskDef.Kind.HoverAtAGL:
                    NavigationTarget = t.hoverAnchorTransform != null
                        ? (Vector3?)t.hoverAnchorTransform.position : null;
                    break;
                case TaskDef.Kind.FlyThrough:
                    NavigationTarget = t.checkpoint != null
                        ? (Vector3?)t.checkpoint.transform.position : null;
                    break;
                case TaskDef.Kind.CourseRun:
                    NavigationTarget = _activeCourse != null && _courseRingIdx < _activeCourse.rings.Length
                        ? (Vector3?)_activeCourse.rings[_courseRingIdx].transform.position : null;
                    break;
                case TaskDef.Kind.Land:
                    NavigationTarget = driver?.spawnPoint != null
                        ? (Vector3?)driver.spawnPoint.position : null;
                    break;
                default:
                    NavigationTarget = null;
                    break;
            }
        }

        void OnCheckpointPassed() => _checkpointPassed = true;

        void HandleCrashReset()
        {
            if (CurrentPhase != Phase.Running) return;

            UnsubscribeCurrentCheckpoint();
            UnsubscribeFromCurrentCourseRing();

            taskIdx           = 0;
            hoverTimer        = 0f;
            HoverProgress     = 0f;
            HoverInZone       = false;
            IsHoverTask       = false;
            landingReceived   = false;
            _checkpointPassed = false;

            if (level2Checkpoints != null)
                foreach (var r in level2Checkpoints)
                    if (r != null) r.gameObject.SetActive(false);

            if (rescueNPCs != null)
                foreach (var npc in rescueNPCs)
                    if (npc != null) npc.Unboard();

            if (_activeCourse != null && tasks[0].kind == TaskDef.Kind.CourseRun)
            {
                TimeRemaining = timeLimit;
                StartCourseRun();
            }

            RefreshInstruction();
            UpdateNavigationTarget();
        }

        void OnLanding(LandingResult result)
        {
            if (CurrentPhase != Phase.Running) return;
            if (!result.success) return;

            if (tasks[taskIdx].kind == TaskDef.Kind.Land)
                landingReceived = true;
        }

        void Succeed()
        {
            FinalTime    = timeLimit - TimeRemaining;
            CurrentPhase = Phase.Success;
            IsHoverTask  = false;
            hoverZoneMarker?.gameObject.SetActive(false);

            GameSettings.UnlockedLevels = Mathf.Max(GameSettings.UnlockedLevels, defaultLevelNumber + 1);

            resultScreen?.ShowSuccess(defaultLevelNumber);
        }

        void Fail()
        {
            string reason = $"Time ran out\n{tasks[taskIdx].instruction}";
            CurrentPhase  = Phase.Failed;
            IsHoverTask   = false;
            hoverZoneMarker?.gameObject.SetActive(false);
            resultScreen?.ShowFailure(reason, defaultLevelNumber);
        }

        void UnsubscribeCurrentCheckpoint()
        {
            if (tasks == null || taskIdx >= tasks.Length) return;
            var t = tasks[taskIdx];
            if (t.kind == TaskDef.Kind.FlyThrough && t.checkpoint != null)
                t.checkpoint.OnPassedThrough -= OnCheckpointPassed;
        }

        void RefreshInstruction()
        {
            if (CurrentPhase == Phase.Success) { CurrentInstruction = "MISSION COMPLETE"; return; }
            if (CurrentPhase == Phase.Failed)  { CurrentInstruction = "TIME'S UP";        return; }
            if (taskIdx >= tasks.Length)       { CurrentInstruction = "";                  return; }

            var t = tasks[taskIdx];
            if (t.kind == TaskDef.Kind.CourseRun && _activeCourse != null)
            {
                int total = _activeCourse.rings.Length;
                CurrentInstruction = _courseComplete
                    ? "Great flying!  Now land the helicopter"
                    : $"Checkpoint  {_courseRingIdx + 1} / {total}";
            }
            else
            {
                CurrentInstruction = t.instruction;
            }
        }

        void ApplyEnvironment(LevelEnvironment env)
        {
            var wz = FindObjectOfType<WindZone>();
            if (wz == null) return;
            wz.windMain           = env.windSpeed;
            wz.windTurbulence     = env.windTurbulence;
            wz.windPulseMagnitude = env.windPulseMagnitude;
            wz.windPulseFrequency = env.windPulseFrequency;
            wz.transform.rotation = Quaternion.Euler(0f, env.windDirectionDeg, 0f);
            FindObjectOfType<WindForce>()?.RefreshWindZone();
        }

        float GetAGL(Vector3 pos) =>
            Mathf.Max(0f, (pos.y - TerrainUtils.GetGroundY(pos)) * MToFt);

        void OnDestroy()
        {
            UnsubscribeCurrentCheckpoint();
            UnsubscribeFromCurrentCourseRing();
            if (landingChecker != null)
                landingChecker.OnLanding -= OnLanding;
            if (_crashHandler != null)
                _crashHandler.OnCrashReset -= HandleCrashReset;
        }
    }
}
