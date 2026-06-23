using UnityEngine;

namespace SimCore
{
    // Simple smooth camera follower with configurable offset
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 5f, -12f);
        public float followSpeed = 6f;
        public float lookDamp = 6f;

        void LateUpdate()
        {
            if (target == null) return;
            Quaternion yawOnly = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            Vector3 desiredPos = target.position + yawOnly * offset;
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * followSpeed);

            Vector3 lookPoint = target.position + Vector3.up * 1.5f;
            Quaternion desiredRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * lookDamp);
        }
    }
}
