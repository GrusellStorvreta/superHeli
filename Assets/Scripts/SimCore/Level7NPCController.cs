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
        public float     walkSpeed     = 1.4f;
        public float     boardRadius   = 3f;

        [Header("Landningsvillkor")]
        public float landingRadiusM = 15f;
        public float landingAglFt   = 6f;

        public bool IsComplete { get; private set; }

        private SimulatorDriver _driver;
        private float           _lyingTimer;

        private enum State { Active, Walking, Complete }
        private State _state = State.Active;

        const float MToFt = 3.28084f;

        void OnEnable()
        {
            _driver     = FindObjectOfType<SimulatorDriver>();
            _lyingTimer = lyingCycleInterval;
            _state      = State.Active;
            IsComplete  = false;
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

            var terrain = Terrain.activeTerrain;
            float groundY = terrain != null
                ? terrain.SampleHeight(heliPos) + terrain.transform.position.y
                : 0f;
            float aglFt = (heliPos.y - groundY) * MToFt;

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
                Complete();
        }

        void Complete()
        {
            _state     = State.Complete;
            IsComplete = true;
        }
    }
}
