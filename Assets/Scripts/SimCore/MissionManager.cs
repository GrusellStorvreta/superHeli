using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimCore
{
    public class MissionManager : MonoBehaviour
    {
        public SimulatorDriver driver;

        [Header("Level")]
        public int              levelNumber        = 1;
        public CheckpointRing[] level2Checkpoints;   // assign Ring1, Ring2 in Inspector

        [Header("Level 7")]
        public string                returnSceneName     = "";
        public Transform             rescueZoneTransform;
        public Transform             baseTransform;
        public RescueNPC[]           rescueNPCs;
        public GameObject            level7NPCParent;
        public Level7NPCController   level7NPCs;

        [Header("Result Screen")]
        public MissionResultScreen resultScreen;

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
            public enum Kind { ClimbToAGL, HoverAtAGL, FlyThrough, Land, NavigateTo, PickupNPCs }
            public Kind           kind;
            public float          targetAglFt;
            public float          hoverDuration;
            public float          maxDeviationFt;
            public CheckpointRing checkpoint;
            public string         instruction;
            public float          radiusM;
            public Transform      targetTransform;
        }

        private TaskDef[] tasks;
        private float     timeLimit;

        // --- Runtime state ---

        private LevelEnvironment _environment;
        private int     taskIdx;
        private float   hoverTimer;
        private Vector3 hoverAnchor;
        private bool    landingReceived;
        private bool    _checkpointPassed;
        private LandingChecker landingChecker;

        // -------------------------------------------------------

        void Start()
        {
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            landingChecker = FindObjectOfType<LandingChecker>();
            if (landingChecker != null) landingChecker.OnLanding += OnLanding;
            BuildLevel();
            StartMission();
        }

        public void Initialize(int level)
        {
            levelNumber = level;
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            if (landingChecker == null)
            {
                landingChecker = FindObjectOfType<LandingChecker>();
                if (landingChecker != null) landingChecker.OnLanding += OnLanding;
            }
            BuildLevel();
            StartMission();
        }

        void BuildLevel()
        {
            if      (levelNumber == 2) BuildLevel2();
            else if (levelNumber == 7) BuildLevel7();
            else                       BuildLevel1();
        }

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

            var ring1 = level2Checkpoints != null && level2Checkpoints.Length > 0 ? level2Checkpoints[0] : null;
            var ring2 = level2Checkpoints != null && level2Checkpoints.Length > 1 ? level2Checkpoints[1] : null;

            tasks = new TaskDef[]
            {
                new TaskDef { kind = TaskDef.Kind.ClimbToAGL, targetAglFt = 200f,
                              instruction = "Climb to 200 ft AGL" },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              instruction   = "Hover at 200 ft  (±5 ft)  for 3 sec" },
                new TaskDef { kind = TaskDef.Kind.FlyThrough, checkpoint = ring1,
                              instruction = "Fly through Checkpoint 1" },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              instruction   = "Hover at 200 ft  (±5 ft)  for 3 sec" },
                new TaskDef { kind = TaskDef.Kind.FlyThrough, checkpoint = ring2,
                              instruction = "Fly through Checkpoint 2  (turn around!)" },
                new TaskDef { kind = TaskDef.Kind.HoverAtAGL, targetAglFt = 200f,
                              hoverDuration = 3f, maxDeviationFt = 5f,
                              instruction   = "Hover at 200 ft  (±5 ft)  for 3 sec" },
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

        void StartMission()
        {
            ApplyEnvironment(_environment);

            // Hide all checkpoints — shown one at a time as tasks advance
            if (level2Checkpoints != null)
                foreach (var r in level2Checkpoints)
                    if (r != null) r.gameObject.SetActive(false);

            taskIdx           = 0;
            TimeRemaining     = timeLimit;
            hoverTimer        = 0f;
            HoverProgress     = 0f;
            HoverInZone       = false;
            IsHoverTask       = false;
            landingReceived   = false;
            _checkpointPassed = false;
            CurrentPhase      = Phase.Running;
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
            // Unsubscribe from outgoing checkpoint
            var prev = tasks[taskIdx];
            if (prev.kind == TaskDef.Kind.FlyThrough && prev.checkpoint != null)
                prev.checkpoint.OnPassedThrough -= OnCheckpointPassed;

            taskIdx++;
            if (taskIdx >= tasks.Length) { Succeed(); return; }

            var next = tasks[taskIdx];

            if (next.kind == TaskDef.Kind.HoverAtAGL)
                hoverAnchor = pos;

            if (next.kind == TaskDef.Kind.FlyThrough && next.checkpoint != null)
            {
                next.checkpoint.gameObject.SetActive(true);
                next.checkpoint.ResetRing();
                _checkpointPassed = false;
                next.checkpoint.OnPassedThrough += OnCheckpointPassed;
            }

            if (next.kind == TaskDef.Kind.PickupNPCs && level7NPCParent != null)
                level7NPCParent.SetActive(true);

            hoverTimer      = 0f;
            HoverProgress   = 0f;
            landingReceived = false;
            RefreshInstruction();
            UpdateNavigationTarget();
        }

        void UpdateNavigationTarget()
        {
            if (taskIdx >= tasks.Length) { NavigationTarget = null; return; }
            var t = tasks[taskIdx];
            NavigationTarget = (t.kind == TaskDef.Kind.NavigateTo && t.targetTransform != null)
                ? (Vector3?)t.targetTransform.position
                : null;
        }

        void OnCheckpointPassed() => _checkpointPassed = true;

        void OnLanding(LandingResult result)
        {
            if (CurrentPhase != Phase.Running) return;

            if (!result.success)
            {
                // Crash — unsubscribe any active checkpoint, restart tasks
                UnsubscribeCurrentCheckpoint();
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
                RefreshInstruction();
                UpdateNavigationTarget();
                return;
            }

            if (tasks[taskIdx].kind == TaskDef.Kind.Land)
                landingReceived = true;
        }

        void Succeed()
        {
            FinalTime    = timeLimit - TimeRemaining;
            CurrentPhase = Phase.Success;
            IsHoverTask  = false;

            if (levelNumber == 2)
                GameSettings.UnlockedLevels = Mathf.Max(GameSettings.UnlockedLevels, 7);
            else if (levelNumber == 7)
                GameSettings.UnlockedLevels = Mathf.Max(GameSettings.UnlockedLevels, 8);
            else if (GameSettings.CurrentLevel >= GameSettings.UnlockedLevels)
                GameSettings.UnlockedLevels = GameSettings.CurrentLevel + 1;

            resultScreen?.ShowSuccess(levelNumber);
        }

        void Fail()
        {
            string reason = $"Time ran out\n{tasks[taskIdx].instruction}";
            CurrentPhase  = Phase.Failed;
            IsHoverTask   = false;
            resultScreen?.ShowFailure(reason, levelNumber);
        }

        void UnsubscribeCurrentCheckpoint()
        {
            if (tasks == null || taskIdx >= tasks.Length) return;
            {
                var t = tasks[taskIdx];
                if (t.kind == TaskDef.Kind.FlyThrough && t.checkpoint != null)
                    t.checkpoint.OnPassedThrough -= OnCheckpointPassed;
            }
        }

        void RefreshInstruction()
        {
            if (CurrentPhase == Phase.Success) { CurrentInstruction = "MISSION COMPLETE"; return; }
            if (CurrentPhase == Phase.Failed)  { CurrentInstruction = "TIME'S UP";        return; }
            CurrentInstruction = taskIdx < tasks.Length ? tasks[taskIdx].instruction : "";
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
            if (landingChecker != null)
                landingChecker.OnLanding -= OnLanding;
        }
    }
}
