using UnityEngine;

namespace SimCore
{
    public class HoverBoxMarker : MonoBehaviour
    {
        public Vector3  size         = new Vector3(20f, 15f, 20f);
        public float    holdDuration = 5f;
        [Range(0.05f, 1f)]
        public float    lineWidth    = 0.25f;
        public Color    color        = new Color(0.2f, 0.85f, 1f, 1f);
        public Material lineMaterial;

        public bool IsInside(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            return Mathf.Abs(local.x) < size.x * 0.5f &&
                   Mathf.Abs(local.y) < size.y * 0.5f &&
                   Mathf.Abs(local.z) < size.z * 0.5f;
        }

        void Awake() => BuildWireframe();

        void BuildWireframe()
        {
            // Clear any stale children from a previous build
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            float hw = size.x * 0.5f, hh = size.y * 0.5f, hd = size.z * 0.5f;

            var lbf = new Vector3(-hw, -hh, -hd); var rbf = new Vector3(hw, -hh, -hd);
            var rtf = new Vector3( hw,  hh, -hd); var ltf = new Vector3(-hw,  hh, -hd);
            var lbk = new Vector3(-hw, -hh,  hd); var rbk = new Vector3(hw, -hh,  hd);
            var rtk = new Vector3( hw,  hh,  hd); var ltk = new Vector3(-hw,  hh,  hd);

            MakeLine(lbf, rbf, rtf, ltf, lbf); // front face
            MakeLine(lbk, rbk, rtk, ltk, lbk); // back face
            MakeLine(lbf, lbk);                 // connecting edges
            MakeLine(rbf, rbk);
            MakeLine(rtf, rtk);
            MakeLine(ltf, ltk);
        }

        void MakeLine(params Vector3[] points)
        {
            var go = new GameObject("_edge") { hideFlags = HideFlags.HideInHierarchy };
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.startWidth = lr.endWidth = lineWidth;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows   = false;
            if (lineMaterial != null) lr.material = lineMaterial;
        }

        void OnDrawGizmos()
        {
            Gizmos.color  = new Color(color.r, color.g, color.b, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
