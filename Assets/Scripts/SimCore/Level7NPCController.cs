using System.Collections;
using UnityEngine;

namespace SimCore
{
    public class Level7NPCController : MonoBehaviour, ILevelResetHandler
    {
        [Header("Räddningszonen — aktiva vid upphämtning")]
        public GameObject nilsRescue;
        public Animator   nilsAnimator;
        public int        lyingVariantCount  = 2;
        public float      lyingCycleInterval = 4f;
        public GameObject svenRescue;
        public Animator   svenAnimator;
        public float      walkSpeed    = 1.4f;
        public float      boardRadius  = 3f;

        [Header("Helikoptern — förinställda, inaktiva vid start")]
        public GameObject nilsSeated;
        public GameObject svenSeated;

        [Header("Boarding")]
        public SceneFader fader;
        public float      passengerWeightKg = 170f;

        [Header("Landningsvillkor")]
        public float landingRadiusM    = 15f;
        public float landingAglFt      = 6f;
        public float landingMaxSpeedMs = 0.5f;

        public bool IsComplete => _state == State.Complete;

        private SimulatorDriver _driver;
        private float           _lyingTimer;
        private int             _lyingVariant;

        private enum State { Active, Walking, Boarding, Complete }
        private State _state = State.Active;

        const float MToFt = 3.28084f;

        void OnEnable()
        {
            _driver     = FindObjectOfType<SimulatorDriver>();
            _lyingTimer = lyingCycleInterval;
            ResetBoarding();
        }

        void Update()
        {
            if (_driver == null) { _driver = FindObjectOfType<SimulatorDriver>(); return; }

            // CheckLanding()/WalkSvenToHelicopter() read the helicopter's live
            // position/velocity — skip a frame rather than act on a position that
            // hasn't caught up yet after a reset was requested (crash, retry, ...).
            if (_driver.IsResetPending) return;

            CycleNilsAnimation();

            switch (_state)
            {
                case State.Active:
                    FaceSven(_driver.LastBodyPosition);
                    CheckLanding();
                    break;

                case State.Walking:
                    WalkSvenToHelicopter();
                    break;
            }
        }

        void CycleNilsAnimation()
        {
            if (nilsAnimator == null || lyingVariantCount <= 1) return;
            _lyingTimer -= Time.deltaTime;
            if (_lyingTimer <= 0f)
            {
                // Offset by 1..count-1 so the same variant never plays twice in a row
                _lyingVariant = (_lyingVariant + Random.Range(1, lyingVariantCount)) % lyingVariantCount;
                nilsAnimator.SetInteger("lyingVariant", _lyingVariant);
                _lyingTimer = lyingCycleInterval;
            }
        }

        void FaceSven(Vector3 heliPos)
        {
            if (svenRescue == null) return;
            Vector3 dir = heliPos - svenRescue.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                svenRescue.transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        void CheckLanding()
        {
            // Hovering low doesn't count — the helicopter must be standing still
            if (_driver.LastBodyVelocity.magnitude > landingMaxSpeedMs) return;

            Vector3 heliPos = _driver.LastBodyPosition;
            float   groundY = TerrainUtils.GetGroundY(heliPos);
            float   aglFt   = (heliPos.y - groundY) * MToFt;

            Vector3 heliFlat = new Vector3(heliPos.x, 0f, heliPos.z);
            Vector3 nilsFlat = nilsRescue != null
                ? new Vector3(nilsRescue.transform.position.x, 0f, nilsRescue.transform.position.z)
                : Vector3.zero;

            if (aglFt <= landingAglFt && Vector3.Distance(heliFlat, nilsFlat) <= landingRadiusM)
                EnterWalking();
        }

        void EnterWalking()
        {
            _state = State.Walking;
            svenAnimator?.SetBool("startWalking", true);
        }

        void WalkSvenToHelicopter()
        {
            if (svenRescue == null) return;

            Vector3 heliPos = _driver.LastBodyPosition;

            // Player took off again — Sven stops and waits for the next landing
            float aglFt = (heliPos.y - TerrainUtils.GetGroundY(heliPos)) * MToFt;
            if (aglFt > landingAglFt)
            {
                svenAnimator?.SetBool("startWalking", false);
                _state = State.Active;
                return;
            }

            Vector3 target = new Vector3(heliPos.x, svenRescue.transform.position.y, heliPos.z);

            FaceSven(heliPos);
            svenRescue.transform.position = Vector3.MoveTowards(
                svenRescue.transform.position, target, walkSpeed * Time.deltaTime);

            if (Vector3.Distance(svenRescue.transform.position, target) <= boardRadius)
            {
                _state = State.Boarding;
                StartCoroutine(BoardingSequence());
            }
        }

        IEnumerator BoardingSequence()
        {
            if (fader != null)
                yield return StartCoroutine(fader.FadeToBlack());

            nilsRescue?.SetActive(false);
            svenRescue?.SetActive(false);
            nilsSeated?.SetActive(true);
            svenSeated?.SetActive(true);

            _driver?.AddPassengerMass(passengerWeightKg);

            yield return new WaitForSeconds(0.5f);

            if (fader != null)
                yield return StartCoroutine(fader.FadeFromBlack());

            _state = State.Complete;
        }

        public void OnLevelReset() => ResetBoarding();

        public void ResetBoarding()
        {
            StopAllCoroutines();
            fader?.Clear();

            nilsRescue?.SetActive(true);
            svenRescue?.SetActive(true);
            nilsSeated?.SetActive(false);
            svenSeated?.SetActive(false);

            svenAnimator?.SetBool("startWalking", false);

            // Boarding added the passenger weight, so resetting it belongs here too
            if (_driver == null) _driver = FindObjectOfType<SimulatorDriver>();
            _driver?.ResetPassengerMass();

            _state = State.Active;
        }

        [ContextMenu("Log NPC Heights (Editor)")]
        void LogHeights()
        {
            LogHeight("SvenRescue", svenRescue);
            LogHeight("NilsRescue", nilsRescue);
        }

        void LogHeight(string label, GameObject go)
        {
            if (go == null) { Debug.Log($"{label}: ej tilldelad"); return; }
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.Log($"{label}: inga Renderers"); return; }
            Vector3 savedScale = go.transform.localScale;
            go.transform.localScale = Vector3.one;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            go.transform.localScale = savedScale;
            Debug.Log($"{label}: höjd vid scale 1 = {b.size.y:F3} m  (nuvarande scale: {savedScale})");
        }

        [ContextMenu("Test Boarding (Editor)")]
        void TestBoarding()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Level7] Test Boarding kräver Play mode.");
                return;
            }
            _state = State.Boarding;
            StartCoroutine(BoardingSequence());
        }

        [ContextMenu("Reset Boarding (Editor)")]
        void TestReset() => ResetBoarding();
    }
}
