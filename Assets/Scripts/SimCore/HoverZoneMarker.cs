using UnityEngine;

namespace SimCore
{
    public class HoverZoneMarker : MonoBehaviour
    {
        public float radiusM   = 3f;
        public int   segments  = 64;
        public float lineWidth = 0.4f;

        static readonly Color ColorIdle = new Color(1f, 0.78f, 0.08f, 0.8f);
        static readonly Color ColorIn   = new Color(0.25f, 0.95f, 0.35f, 0.9f);

        private LineRenderer _line;

        void Awake()
        {
            _line                   = gameObject.AddComponent<LineRenderer>();
            _line.loop              = true;
            _line.positionCount     = segments;
            _line.startWidth        = lineWidth;
            _line.endWidth          = lineWidth;
            _line.useWorldSpace     = false;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var shader = Shader.Find("HDRP/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _line.material       = new Material(shader);
                _line.material.color = ColorIdle;
            }
            _line.startColor = ColorIdle;
            _line.endColor   = ColorIdle;

            BuildRing();
        }

        public void Place(Vector3 anchorXZ, float targetAglFt, float deviationFt)
        {
            float groundY = TerrainUtils.GetGroundY(anchorXZ);
            float worldY  = groundY + targetAglFt / 3.28084f;
            transform.position = new Vector3(anchorXZ.x, worldY, anchorXZ.z);

            radiusM = deviationFt / 3.28084f;
            BuildRing();
            SetInZone(false);
        }

        public void SetInZone(bool inZone)
        {
            if (_line == null) return;
            Color c = inZone ? ColorIn : ColorIdle;
            _line.startColor = c;
            _line.endColor   = c;
            if (_line.material != null) _line.material.color = c;
        }

        void BuildRing()
        {
            if (_line == null) return;
            _line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(Mathf.Cos(a) * radiusM, 0f, Mathf.Sin(a) * radiusM));
            }
        }

        void OnDestroy()
        {
            if (_line != null && _line.material != null)
                Destroy(_line.material);
        }
    }
}
