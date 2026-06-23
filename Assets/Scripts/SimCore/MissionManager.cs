using System.Collections;
using UnityEngine;

namespace SimCore
{
    public class MissionManager : MonoBehaviour
    {
        public SimulatorDriver driver;

        public enum Phase { Idle, Running, Success, Failed }
        public Phase    CurrentPhase    { get; private set; } = Phase.Idle;
        public float    TimeRemaining   { get; private set; }
        public float    HoverProgress   { get; private set; }
        public bool     HoverInZone     { get; private set; }
        public bool     IsHoverTask     { get; private set; }
        public string   CurrentInstruction { get; private set; } = "";
        public float    FinalTime       { get; private set; }

        private const float MToFt = 3.28084f;

        // --- Task / Level data ---

        struct LevelEnvironment
        {
            public float windSpeed;           // m/s — written to WindZone.windMain
            public float windDirectionDeg;    // 0 = world +Z (Unity forward)
            public float windTurbulence;
            public float windPulseMagnitude;
            public float windPulseFrequency;
        }

        class TaskDef
        {
            public enum Kind { ClimbToAGL, HoverAtAGL, Land }
            public Kind   kind;
            public float  targetAglFt;
            public float  hoverDuration;
            public float  maxDeviationFt;
            public string instruction;
        }

        private TaskDef[] tasks;
        private float     timeLimit;

        // --- Runtime state ---

        private LevelEnvironment _environment;
        private int     taskIdx;
        private float   hoverTimer;
        private Vector3 hoverAnchor;
        private bool    landingReceived;
        private LandingChecker landingChecker;

        // -------------------------------------------------------

        void Start()
        {
            BuildLevel1();
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();
            landingChecker = FindObjectOfType<LandingChecker>();
            if (landingChecker != null) landingChecker.OnLanding += OnLanding;
            StartMission();
        }

        void BuildLevel1()
        {
            _environment = new LevelEnvironment
            {
                windSpeed          = 0f,
                windDirectionDeg   = 0f,
                windTurbulence     = 0f,
                windPulseMagnitude = 0f,
                windPulseFrequency = 0f,
            };

            timeLimit = 30f;
            tasks = new TaskDef[]
            {
                new TaskDef
                {
                    kind        = TaskDef.Kind.ClimbToAGL,
                    targetAglFt = 100f,
                    instruction = "Climb to 100 ft AGL"
                },
                new TaskDef
                {
                    kind           = TaskDef.Kind.HoverAtAGL,
                    targetAglFt    = 100f,
                    hoverDuration  = 5f,
                    maxDeviationFt = 3f,
                    instruction    = "Hover at 100 ft  (±3 ft)  for 5 sec"
                },
                new TaskDef
                {
                    kind        = TaskDef.Kind.Land,
                    instruction = "Land the helicopter"
                },
            };
        }

        void StartMission()
        {
            ApplyEnvironment(_environment);
            taskIdx         = 0;
            TimeRemaining   = timeLimit;
            hoverTimer      = 0f;
            HoverProgress   = 0f;
            HoverInZone     = false;
            IsHoverTask     = false;
            landingReceived = false;
            CurrentPhase    = Phase.Running;
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

                case TaskDef.Kind.Land:
                    if (landingReceived)
                        Succeed();
                    break;
            }
        }

        void AdvanceTask(Vector3 pos)
        {
            taskIdx++;
            if (taskIdx >= tasks.Length) { Succeed(); return; }

            if (tasks[taskIdx].kind == TaskDef.Kind.HoverAtAGL)
                hoverAnchor = pos;

            hoverTimer      = 0f;
            HoverProgress   = 0f;
            landingReceived = false;
            RefreshInstruction();
        }

        void OnLanding(LandingResult result)
        {
            if (CurrentPhase != Phase.Running) return;

            if (!result.success)
            {
                // Crash — restart task sequence, timer keeps running
                taskIdx         = 0;
                hoverTimer      = 0f;
                HoverProgress   = 0f;
                HoverInZone     = false;
                IsHoverTask     = false;
                landingReceived = false;
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
        }

        void Fail()
        {
            CurrentPhase = Phase.Failed;
            IsHoverTask  = false;
            RefreshInstruction();
            StartCoroutine(RestartAfterDelay(3f));
        }

        IEnumerator RestartAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            var driver = this.driver != null ? this.driver : FindObjectOfType<SimulatorDriver>();
            driver?.ResetToSpawnPoint();
            BuildLevel1();
            StartMission();
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

            // Refresh the cached WindZone reference in WindForce if present.
            var windForce = FindObjectOfType<WindForce>();
            windForce?.RefreshWindZone();
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
            if (landingChecker != null)
                landingChecker.OnLanding -= OnLanding;
        }
    }
}
