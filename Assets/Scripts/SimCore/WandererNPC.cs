using UnityEngine;

namespace SimCore
{
    public class WandererNPC : MonoBehaviour
    {
        [Header("Waypoints")]
        public Transform[] waypoints;
        public float waypointTolerance = 0.5f;

        [Header("Movement")]
        public float walkSpeed  = 1.4f;
        public float turnSpeed  = 5f;

        [Header("Timing")]
        public float   startClipDuration    = 0.6f;
        public float   stopClipDuration     = 0.5f;
        public float   backflipClipDuration = 1.2f;
        public Vector2 idleWaitRange        = new Vector2(3f, 8f);

        [Header("Backflip")]
        [Range(0f, 1f)]
        public float backflipChance = 0.4f;

        private Animator _anim;
        private int      _waypointIdx;
        private float    _stateTimer;

        private enum State { Idle, Starting, Walking, Stopping, Backflip }
        private State _state = State.Idle;


        void Start()
        {
            _anim       = GetComponent<Animator>();
            _stateTimer = Random.Range(idleWaitRange.x, idleWaitRange.y);
        }

        void Update()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            float dt = Time.deltaTime;

            switch (_state)
            {
                case State.Idle:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                        EnterStarting();
                    break;

                case State.Starting:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                        EnterWalking();
                    break;

                case State.Walking:
                    MoveTowardWaypoint(dt);
                    break;

                case State.Stopping:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                        DecideAfterStop();
                    break;

                case State.Backflip:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                        EnterIdle();
                    break;
            }
        }

        void MoveTowardWaypoint(float dt)
        {
            Transform target    = waypoints[_waypointIdx];
            Vector3   targetPos = new Vector3(target.position.x, transform.position.y, target.position.z);
            Vector3   dir       = targetPos - transform.position;

            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir.normalized),
                    turnSpeed * dt);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPos, walkSpeed * dt);

            if (Vector3.Distance(transform.position, targetPos) <= waypointTolerance)
            {
                _waypointIdx = (_waypointIdx + 1) % waypoints.Length;
                EnterStopping();
            }
        }

        void EnterStarting()
        {
            _state      = State.Starting;
            _stateTimer = startClipDuration;
            _anim?.SetBool("isWalking", true);
        }

        void EnterWalking()
        {
            _state = State.Walking;
        }

        void EnterStopping()
        {
            _state      = State.Stopping;
            _stateTimer = stopClipDuration;
            _anim?.SetBool("isWalking", false);
        }

        void DecideAfterStop()
        {
            if (Random.value < backflipChance)
                EnterBackflip();
            else
                EnterIdle();
        }

        void EnterBackflip()
        {
            _state      = State.Backflip;
            _stateTimer = backflipClipDuration;
            _anim?.SetTrigger("doBackflip");
        }

        void EnterIdle()
        {
            _state      = State.Idle;
            _stateTimer = Random.Range(idleWaitRange.x, idleWaitRange.y);
        }
    }
}
