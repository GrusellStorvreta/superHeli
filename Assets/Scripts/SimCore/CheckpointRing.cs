using UnityEngine;

namespace SimCore
{
    public class CheckpointRing : MonoBehaviour
    {
        public float radius   = 12f;
        public int   segments = 48;

        public event System.Action OnPassedThrough;

        private bool         _passed;
        private LineRenderer _line;

        static readonly Color ColorActive = new Color(1f, 0.78f, 0.08f, 0.9f);  // amber
        static readonly Color ColorDone   = new Color(0.25f, 1f, 0.35f, 0.9f);  // green

        void Awake()
        {
            // Trigger collider — sphere slightly inside ring edge
            var sc       = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius    = radius * 0.8f;

            // Visual ring via LineRenderer
            _line                   = gameObject.AddComponent<LineRenderer>();
            _line.loop              = true;
            _line.positionCount     = segments;
            _line.startWidth        = 0.5f;
            _line.endWidth          = 0.5f;
            _line.useWorldSpace     = false;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var shader = Shader.Find("HDRP/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _line.material             = new Material(shader);
                _line.material.color       = ColorActive;
            }
            _line.startColor = ColorActive;
            _line.endColor   = ColorActive;

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        public void ResetRing()
        {
            _passed          = false;
            if (_line == null) return;
            _line.startColor = ColorActive;
            _line.endColor   = ColorActive;
            if (_line.material != null) _line.material.color = ColorActive;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_passed) return;
            if (other.GetComponent<Rigidbody>() == null) return;

            _passed          = true;
            if (_line != null)
            {
                _line.startColor = ColorDone;
                _line.endColor   = ColorDone;
                if (_line.material != null) _line.material.color = ColorDone;
            }
            OnPassedThrough?.Invoke();
        }

        void OnDestroy()
        {
            if (_line != null && _line.material != null)
                Destroy(_line.material);
        }
    }
}
