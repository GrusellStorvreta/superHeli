using System.Collections;
using UnityEngine;

namespace SimCore
{
    public class MissionManager : MonoBehaviour
    {
        public SimulatorDriver driver;

        [Header("Level")]
        public int              levelNumber        = 1;
        public CheckpointRing[] level2Checkpoints;   // assign Ring1, Ring2 in Inspector

        public enum Phase { Idle, Running, Success, Failed }
        public Phase    CurrentPhase       { get; private set; } = Phase.Idle;
        public float    TimeRemaining      { get; private set; }
        public float    HoverProgress      { get; private set; }
        public bool     HoverInZone        { get; private set; }
        public bool     IsHoverTask        { get; private set; }
        public string   CurrentInstruction { get; private set; } = "";
        public float    FinalTime          { get; private set; }

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
            public enum Kind { ClimbToAGL, HoverAtAGL, FlyThrough, Land }
            public Kind           kind;
            public float          targetAglFt;
            public float          hoverDuration;
            public float          maxDeviationFt;
            public CheckpointRing checkpoint;
            public string         instruction;
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
            BuildLevel();
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            landingChecker = FindObjectOfType<LandingChecker>();
            if (landingChecker != null) landingChecker.OnLanding += OnLanding;
            StartMission();
        }

        void BuildLevel()
        {
            if (levelNumber == 2) BuildLevel2();
            else                  BuildLevel1();
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
            }
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

            hoverTimer      = 0f;
            HoverProgress   = 0f;
            landingReceived = false;
            RefreshInstruction();
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
                RefreshInstruction();
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
            RefreshInstruction();

            // Unlock next level
            if (GameSettings.CurrentLevel >= GameSettings.UnlockedLevels)
                GameSettings.UnlockedLevels = GameSettings.CurrentLevel + 1;

            StartCoroutine(ReturnToMenuAfterDelay(4f));
        }

        void Fail()
        {
            CurrentPhase = Phase.Failed;
            IsHoverTask  = false;
            RefreshInstruction();
            StartCoroutine(RestartAfterDelay(3f));
        }

        IEnumerator ReturnToMenuAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CurrentPhase = Phase.Idle;
            var drv = driver != null ? driver : FindObjectOfType<SimulatorDriver>();
            drv?.ResetToSpawnPoint();
            FindObjectOfType<MainMenuManager>()?.ShowMenu();
        }

        IEnumerator RestartAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            var drv = driver != null ? driver : FindObjectOfType<SimulatorDriver>();
            drv?.ResetToSpawnPoint();
            BuildLevel();
            StartMission();
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

        float GetAGL(Vector3 pos)
        {
            var terrain = Terrain.activeTerrain;
            float groundY = terrain != null
                ? terrain.SampleHeight(pos) + terrain.transform.position.y
                : 0f;
            return (pos.y - groundY) * MToFt;
        }

        void OnDestroy()
        {
            UnsubscribeCurrentCheckpoint();
            if (landingChecker != null)
                landingChecker.OnLanding -= OnLanding;
        }
    }
}
