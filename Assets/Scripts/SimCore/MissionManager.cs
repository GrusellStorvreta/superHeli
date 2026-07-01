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
        public class CourseWaypoint
        {
            public enum Kind { Checkpoint, HoverBox }
            public Kind           kind     = Kind.Checkpoint;
            public CheckpointRing ring;       // if Checkpoint
            public HoverBoxMarker hoverBox;   // if HoverBox
        }

        [Serializable]
        public class CheckpointCourse
        {
            public string           courseName;
            public int              levelNumber;
            public CourseWaypoint[] waypoints;
            public float            timeLimit    = 90f;
            public int              visibleAhead = 3;
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

        public event System.Action OnMissionStarted;
        public event System.Action OnMissionSuccess;
        public event System.Action OnCheckpointPassed;
        public event System.Action OnHoverTaskComplete;
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
        private int               _courseWaypointIdx;
        private bool              _courseComplete;
        private float             _completionPauseTimer;

        CourseWaypoint CurrentWaypoint =>
            _activeCourse != null && _courseWaypointIdx < _activeCourse.waypoints.Length
                ? _activeCourse.waypoints[_courseWaypointIdx] : null;

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
                              instruction = Loc.Get("instr.climb_100") },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 100f,
                              hoverDuration = 5f, maxDeviationFt = 3f,
                              instruction   = Loc.Get("instr.hover_100") },
                new TaskDef { kind = TaskDef.Kind.Land,
                              instruction = Loc.Get("instr.land") },
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
                              instruction = Loc.Get("instr.climb_200") },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              hoverAnchorTransform = anchor1,
                              taskTimeLimit = 20f,
                              instruction   = Loc.Get("instr.hover_200") },
                new TaskDef { kind = TaskDef.Kind.FlyThrough, checkpoint = ring1,
                              taskTimeLimit = 45f,
                              instruction = Loc.Get("instr.checkpoint_1") },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              hoverAnchorTransform = anchor2,
                              taskTimeLimit = 20f,
                              instruction   = Loc.Get("instr.hover_200") },
                new TaskDef { kind = TaskDef.Kind.FlyThrough, checkpoint = ring2,
                              taskTimeLimit = 45f,
                              instruction = Loc.Get("instr.checkpoint_2") },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              hoverAnchorTransform = anchor3,
                              taskTimeLimit = 20f,
                              instruction   = Loc.Get("instr.hover_200") },
                new TaskDef { kind = TaskDef.Kind.Land,
                              taskTimeLimit = 30f,
                              instruction = Loc.Get("instr.land") },
            };
        }

        void BuildLevelCourse(CheckpointCourse course)
        {
            _environment  = new LevelEnvironment { windSpeed = 0f };
            timeLimit     = course.timeLimit;
            _activeCourse = course;

            int waypointCount = course.waypoints?.Length ?? 0;
            tasks = new TaskDef[]
            {
                new TaskDef { kind = TaskDef.Kind.CourseRun,
                              instruction = Loc.Get("instr.course", waypointCount) },
                new TaskDef { kind = TaskDef.Kind.Land,
                              instruction = Loc.Get("instr.land") },
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
                              instruction = Loc.Get("instr.climb_50") },
                new TaskDef { kind = TaskDef.Kind.NavigateTo, targetTransform = rescueZoneTransform,
                              radiusM = 100f, instruction = Loc.Get("instr.rescue_zone") },
                new TaskDef { kind = TaskDef.Kind.PickupNPCs,
                              instruction = Loc.Get("instr.land_pickup") },
                new TaskDef { kind = TaskDef.Kind.NavigateTo, targetTransform = baseTransform,
                              radiusM = 80f, instruction = Loc.Get("instr.return_base") },
                new TaskDef { kind = TaskDef.Kind.Land,
                              instruction = Loc.Get("instr.land") },
            };
        }

        // -------------------------------------------------------
        // Course run — sliding window
        // -------------------------------------------------------

        void StartCourseRun()
        {
            if (_activeCourse?.waypoints == null) return;

            UnsubscribeFromCurrentCourseWaypoint();
            _courseWaypointIdx = 0;
            _courseComplete    = false;
            IsHoverTask        = false;
            hoverTimer         = 0f;
            HoverProgress      = 0f;

            foreach (var wp in _activeCourse.waypoints)
            {
                wp.ring?.gameObject.SetActive(false);
                wp.hoverBox?.gameObject.SetActive(false);
            }

            SetupCurrentWaypoint();
            RefreshInstruction();
        }

        void SetupCurrentWaypoint()
        {
            var wp = CurrentWaypoint;
            if (wp == null) return;

            if (wp.kind == CourseWaypoint.Kind.Checkpoint)
            {
                ShowCheckpointWindow();
                SubscribeToCurrentCourseWaypoint();
            }
            else
            {
                wp.hoverBox?.gameObject.SetActive(true);
                hoverTimer    = 0f;
                HoverProgress = 0f;
            }
            UpdateNavigationTarget();
        }

        void ShowCheckpointWindow()
        {
            int shown = 0;
            for (int i = _courseWaypointIdx; i < _activeCourse.waypoints.Length && shown < _activeCourse.visibleAhead; i++)
            {
                var wp = _activeCourse.waypoints[i];
                if (wp.kind != CourseWaypoint.Kind.Checkpoint) break;
                if (wp.ring == null) continue;
                wp.ring.gameObject.SetActive(true);
                wp.ring.ResetRing();
                shown++;
            }
        }

        void SubscribeToCurrentCourseWaypoint()
        {
            var wp = CurrentWaypoint;
            if (wp?.kind == CourseWaypoint.Kind.Checkpoint && wp.ring != null)
                wp.ring.OnPassedThrough += AdvanceCourseWaypoint;
        }

        void UnsubscribeFromCurrentCourseWaypoint()
        {
            var wp = CurrentWaypoint;
            if (wp?.kind == CourseWaypoint.Kind.Checkpoint && wp.ring != null)
                wp.ring.OnPassedThrough -= AdvanceCourseWaypoint;
        }

        void AdvanceCourseWaypoint()
        {
            var wp = CurrentWaypoint;
            if (wp != null)
            {
                if (wp.kind == CourseWaypoint.Kind.Checkpoint)
                    wp.ring?.gameObject.SetActive(false);
                else
                    wp.hoverBox?.gameObject.SetActive(false);

                IsHoverTask   = false;
                hoverTimer    = 0f;
                HoverProgress = 0f;
            }

            _courseWaypointIdx++;
            OnCheckpointPassed?.Invoke();

            if (_courseWaypointIdx >= _activeCourse.waypoints.Length)
            {
                _courseComplete       = true;
                _completionPauseTimer = 2.5f;
                RefreshInstruction();
                return;
            }

            // Reveal next ring in sliding window for consecutive checkpoints
            if (CurrentWaypoint?.kind == CourseWaypoint.Kind.Checkpoint)
            {
                int tail = _courseWaypointIdx + _activeCourse.visibleAhead - 1;
                if (tail < _activeCourse.waypoints.Length)
                {
                    var tailWp = _activeCourse.waypoints[tail];
                    if (tailWp.kind == CourseWaypoint.Kind.Checkpoint && tailWp.ring != null)
                    {
                        tailWp.ring.gameObject.SetActive(true);
                        tailWp.ring.ResetRing();
                    }
                }
                SubscribeToCurrentCourseWaypoint();
            }
            else
            {
                SetupCurrentWaypoint();
            }

            RefreshInstruction();
            UpdateNavigationTarget();
        }

        void EvaluateCourseHover(Vector3 pos)
        {
            var wp = CurrentWaypoint;
            if (wp?.hoverBox == null) return;

            IsHoverTask = true;
            HoverInZone = wp.hoverBox.IsInside(pos);

            if (HoverInZone)
            {
                hoverTimer    += Time.deltaTime;
                HoverProgress  = Mathf.Clamp01(hoverTimer / wp.hoverBox.holdDuration);
                if (hoverTimer >= wp.hoverBox.holdDuration)
                {
                    OnHoverTaskComplete?.Invoke();
                    AdvanceCourseWaypoint();
                }
            }
            else
            {
                hoverTimer    = 0f;
                HoverProgress = 0f;
            }
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
            OnMissionStarted?.Invoke();
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
                        {
                            OnHoverTaskComplete?.Invoke();
                            AdvanceTask(pos);
                        }
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
                    else if (CurrentWaypoint?.kind == CourseWaypoint.Kind.HoverBox)
                    {
                        EvaluateCourseHover(pos);
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
                prev.checkpoint.OnPassedThrough -= OnFlyThroughPassed;

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
                next.checkpoint.OnPassedThrough += OnFlyThroughPassed;
            }

            if (next.kind == TaskDef.Kind.CourseRun)
                StartCourseRun();

            if (next.kind == TaskDef.Kind.PickupNPCs && level7NPCParent != null)
                level7NPCParent.SetActive(true);

            hoverTimer      = 0f;
            HoverProgress   = 0f;
            landingReceived = false;

            // If the player landed cleanly during the course-complete pause (before the task
            // switched to Land), count it — the helicopter is already on the ground and
            // OnCollisionEnter won't fire again.
            if (next.kind == TaskDef.Kind.Land &&
                landingChecker != null &&
                landingChecker.LastResult.success)
                landingReceived = true;

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
                    var cw = CurrentWaypoint;
                    NavigationTarget = cw == null ? null
                        : cw.kind == CourseWaypoint.Kind.Checkpoint && cw.ring != null
                            ? (Vector3?)cw.ring.transform.position
                            : cw.hoverBox != null ? (Vector3?)cw.hoverBox.transform.position : null;
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

        void OnFlyThroughPassed() => _checkpointPassed = true;

        void HandleCrashReset()
        {
            if (CurrentPhase != Phase.Running) return;

            UnsubscribeCurrentCheckpoint();
            UnsubscribeFromCurrentCourseWaypoint();

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
            OnMissionSuccess?.Invoke();
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
                t.checkpoint.OnPassedThrough -= OnFlyThroughPassed;
        }

        void RefreshInstruction()
        {
            if (CurrentPhase == Phase.Success) { CurrentInstruction = Loc.Get("result.complete"); return; }
            if (CurrentPhase == Phase.Failed)  { CurrentInstruction = Loc.Get("result.timesup");  return; }
            if (taskIdx >= tasks.Length)       { CurrentInstruction = "";                  return; }

            var t = tasks[taskIdx];
            if (t.kind == TaskDef.Kind.CourseRun && _activeCourse != null)
            {
                if (_courseComplete)
                    CurrentInstruction = Loc.Get("instr.course_complete");
                else if (CurrentWaypoint?.kind == CourseWaypoint.Kind.HoverBox)
                    CurrentInstruction = Loc.Get("instr.course_hover");
                else
                    CurrentInstruction = Loc.Get("instr.checkpoint_progress",
                        _courseWaypointIdx + 1, _activeCourse.waypoints.Length);
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
            UnsubscribeFromCurrentCourseWaypoint();
            if (landingChecker != null)
                landingChecker.OnLanding -= OnLanding;
            if (_crashHandler != null)
                _crashHandler.OnCrashReset -= HandleCrashReset;
        }
    }
}
