using UnityEngine;

namespace SimCore
{
    public class RescueNPC : MonoBehaviour
    {
        public float   pickupRadius    = 15f;
        public float   passengerMassKg = 150f;
        public Vector3 seatOffset      = new Vector3(0f, 0.5f, 0f);
        public bool    isInjured       = false;

        public bool IsBoarded => _boarded;

        private SimulatorDriver _driver;
        private bool            _boarded;
        private Transform       _origParent;
        private Vector3         _origLocalPos;
        private Quaternion      _origLocalRot;

        private const float AglBoardThreshFt = 2f;
        private const float MToFt            = 3.28084f;

        void Start()
        {
            _driver       = FindObjectOfType<SimulatorDriver>();
            _origParent   = transform.parent;
            _origLocalPos = transform.localPosition;
            _origLocalRot = transform.localRotation;
        }

        void Update()
        {
            if (_boarded || _driver == null) return;

            Vector3 heliPos = _driver.LastBodyPosition;

            float aglFt = (heliPos.y - TerrainUtils.GetGroundY(heliPos)) * MToFt;

            if (aglFt > AglBoardThreshFt) return;

            float horizDist = new Vector3(heliPos.x - transform.position.x, 0f,
                                          heliPos.z - transform.position.z).magnitude;
            if (horizDist > pickupRadius) return;

            Board();
        }

        void Board()
        {
            _boarded = true;
            var patrol = GetComponent<NPCPatrol>();
            if (patrol != null) patrol.enabled = false;
            transform.SetParent(_driver.transform);
            transform.localPosition = seatOffset;
            transform.localRotation = Quaternion.identity;
            _driver.AddPassengerMass(passengerMassKg);
        }

        public void Unboard()
        {
            if (!_boarded) return;
            _boarded = false;
            _driver?.ResetPassengerMass();
            transform.SetParent(_origParent);
            transform.localPosition = _origLocalPos;
            transform.localRotation = _origLocalRot;
            var patrol = GetComponent<NPCPatrol>();
            if (patrol != null) patrol.enabled = true;
        }
    }
}
