using UnityEngine;

namespace SimCore
{
    public static class TerrainUtils
    {
        public static Terrain GetTerrainAt(Vector3 worldPos)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                Vector3 tp = t.transform.position;
                Vector3 ts = t.terrainData.size;
                if (worldPos.x >= tp.x && worldPos.x <= tp.x + ts.x &&
                    worldPos.z >= tp.z && worldPos.z <= tp.z + ts.z)
                    return t;
            }
            return null;
        }

        public static float GetGroundY(Vector3 worldPos)
        {
            var t = GetTerrainAt(worldPos);
            return t != null ? t.SampleHeight(worldPos) + t.transform.position.y : 0f;
        }
    }
}
