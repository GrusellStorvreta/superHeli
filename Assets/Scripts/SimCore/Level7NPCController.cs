using System.Collections;
using UnityEngine;

namespace SimCore
{
    public class Level7NPCController : MonoBehaviour
    {
        [Header("Nils (skadad, ligger kvar)")]
        public Transform nilsTransform;
        public Animator  nilsAnimator;
        public int       lyingVariantCount  = 2;
        public float     lyingCycleInterval = 4f;

        [Header("Sven (oskadad, vinkar och går)")]
        public Transform svenTransform;
        public Animator  svenAnimator;
        public float     walkSpeed   = 1.4f;
        public float     boardRadius = 3f;

        [Header("Boarding")]
        public SceneFader fader;
        public Transform  seatNils;
        public Transform  seatSven;
        public float      passengerWeightKg = 170f;

        [Header("Landningsvillkor")]
        public float landingRadiusM = 15f;
        public float landingAglFt   = 6f;

        public bool IsComplete { get; private set; }

        private SimulatorDriver _driver;
        private float           _lyingTimer;

        private Transform  _nilsOriginalParent;
        private Vector3    _nilsOriginalLocalPos;
        private Quaternion _nilsOriginalLocalRot;
        private Transform  _svenOriginalParent;
        private Vector3    _svenOriginalLocalPos;
        private Quaternion _svenOriginalLocalRot;

        private enum State { Active, Walking, Boarding, Complete }
        private State _state = State.Active;

        const float MToFt = 3.28084f;

        void OnEnable()
        {
            _driver     = FindObjectOfType<SimulatorDriver>();
            _lyingTimer = lyingCycleInterval;
            _state      = State.Active;
            IsComplete  = false;

            if (nilsTransform != null)
            {
                _nilsOriginalParent   = nilsTransform.parent;
                _nilsOriginalLocalPos = nilsTransform.localPosition;
                _nilsOriginalLocalRot = nilsTransform.localRotation;
            }
            if (svenTransform != null)
            {
                _svenOriginalParent   = svenTransform.parent;
                _svenOriginalLocalPos = svenTransform.localPosition;
                _svenOriginalLocalRot = svenTransform.localRotation;
            }
        }

        void Update()
        {
            if (_driver == null) { _driver = FindObjectOfType<SimulatorDriver>(); return; }

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
                nilsAnimator.SetInteger("lyingVariant", Random.Range(0, lyingVariantCount));
                _lyingTimer = lyingCycleInterval;
            }
        }

        void FaceSven(Vector3 heliPos)
        {
            if (svenTransform == null) return;
            Vector3 dir = heliPos - svenTransform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                svenTransform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        void CheckLanding()
        {
            Vector3 heliPos = _driver.LastBodyPosition;
            float   groundY = TerrainUtils.GetGroundY(heliPos);
            float   aglFt   = (heliPos.y - groundY) * MToFt;

            Vector3 heliFlat = new Vector3(heliPos.x, 0f, heliPos.z);
            Vector3 nilsFlat = nilsTransform != null
                ? new Vector3(nilsTransform.position.x, 0f, nilsTransform.position.z)
                : Vector3.zero;

            if (aglFt <= landingAglFt && Vector3.Distance(heliFlat, nilsFlat) <= landingRadiusM)
                EnterWalking();
        }

        void EnterWalking()
        {
            _state = State.Walking;
            svenAnimator?.SetTrigger("startWalking");
        }

        void WalkSvenToHelicopter()
        {
            if (svenTransform == null) return;

            Vector3 heliPos = _driver.LastBodyPosition;
            Vector3 target  = new Vector3(heliPos.x, svenTransform.position.y, heliPos.z);

            FaceSven(heliPos);
            svenTransform.position = Vector3.MoveTowards(
                svenTransform.position, target, walkSpeed * Time.deltaTime);

            if (Vector3.Distance(svenTransform.position, target) <= boardRadius)
                StartBoarding();
        }

        void StartBoarding()
        {
            _state = State.Boarding;
            StartCoroutine(BoardingSequence());
        }

        IEnumerator BoardingSequence()
        {
            if (fader != null)
                yield return StartCoroutine(fader.FadeToBlack());

            // Reparent passengers to helicopter seats while screen is black
            if (seatSven != null && svenTransform != null)
            {
                svenTransform.SetParent(seatSven, false);
                svenTransform.localPosition = Vector3.zero;
                svenTransform.localRotation = Quaternion.identity;
            }
            if (seatNils != null && nilsTransform != null)
            {
                nilsTransform.SetParent(seatNils, false);
                nilsTransform.localPosition = Vector3.zero;
                nilsTransform.localRotation = Quaternion.identity;
            }

            if (svenAnimator != null) svenAnimator.enabled = false;
            if (nilsAnimator != null) nilsAnimator.enabled = false;

            if (_driver != null) _driver.AddPassengerMass(passengerWeightKg);

            yield return new WaitForSeconds(0.5f);

            if (fader != null)
                yield return StartCoroutine(fader.FadeFromBlack());

            _state     = State.Complete;
            IsComplete = true;
        }

        [ContextMenu("Test Boarding (Editor)")]
        void TestBoarding()
        {
            if (seatSven != null && svenTransform != null)
            {
                svenTransform.SetParent(seatSven, false);
                svenTransform.localPosition = Vector3.zero;
                svenTransform.localRotation = Quaternion.identity;
            }
            if (seatNils != null && nilsTransform != null)
            {
                nilsTransform.SetParent(seatNils, false);
                nilsTransform.localPosition = Vector3.zero;
                nilsTransform.localRotation = Quaternion.identity;
            }
        }

        [ContextMenu("Reset Boarding (Editor)")]
        void TestReset() => ResetBoarding();

        public void ResetBoarding()
        {
            StopAllCoroutines();
            fader?.Clear();

            if (nilsTransform != null && _nilsOriginalParent != null)
            {
                nilsTransform.SetParent(_nilsOriginalParent, false);
                nilsTransform.localPosition = _nilsOriginalLocalPos;
                nilsTransform.localRotation = _nilsOriginalLocalRot;
            }
            if (svenTransform != null && _svenOriginalParent != null)
            {
                svenTransform.SetParent(_svenOriginalParent, false);
                svenTransform.localPosition = _svenOriginalLocalPos;
                svenTransform.localRotation = _svenOriginalLocalRot;
            }

            if (nilsAnimator != null) nilsAnimator.enabled = true;
            if (svenAnimator != null) svenAnimator.enabled = true;

            _state     = State.Active;
            IsComplete = false;
        }
    }
}
