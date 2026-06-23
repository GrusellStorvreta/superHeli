using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TerrainColorMap : MonoBehaviour
{
    [System.Serializable]
    public struct ColorLayer
    {
        public Color color;
        public TerrainLayer terrainLayer;
    }

    public Texture2D colorMap;
    public ColorLayer[] layers;

    [ContextMenu("Apply Color Map to Terrain")]
    public void Apply()
    {
        if (colorMap == null)
        {
            Debug.LogError("Assign a color map texture in the Inspector.");
            return;
        }
        if (layers == null || layers.Length == 0)
        {
            Debug.LogError("Add at least one Color → Layer mapping in the Inspector.");
            return;
        }

#if UNITY_EDITOR
        EnsureReadable();

        Terrain terrain = GetComponent<Terrain>();
        TerrainData td = terrain.terrainData;

        // Push layer references onto the terrain
        var terrainLayers = new TerrainLayer[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].terrainLayer == null)
            {
                Debug.LogError($"Layer {i} has no TerrainLayer assigned.");
                return;
            }
            terrainLayers[i] = layers[i].terrainLayer;
        }
        td.terrainLayers = terrainLayers;

        int aw = td.alphamapWidth;
        int ah = td.alphamapHeight;
        int lc = layers.Length;
        float[,,] alphas = new float[ah, aw, lc];

        for (int y = 0; y < ah; y++)
        {
            for (int x = 0; x < aw; x++)
            {
                float u = (float)x / (aw - 1);
                float v = (float)y / (ah - 1);
                Color sample = colorMap.GetPixelBilinear(u, v);
                int best = FindClosestLayer(sample);

                for (int l = 0; l < lc; l++)
                    alphas[y, x, l] = l == best ? 1f : 0f;
            }
        }

        td.SetAlphamaps(0, 0, alphas);
        Debug.Log($"Color map applied: {aw}×{ah} alphamap, {lc} layers.");
#else
        Debug.LogWarning("TerrainColorMap only runs in the Editor.");
#endif
    }

    private int FindClosestLayer(Color c)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < layers.Length; i++)
        {
            float dist = ColorDistSq(c, layers[i].color);
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return best;
    }

    private static float ColorDistSq(Color a, Color b)
    {
        float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
        return dr * dr + dg * dg + db * db;
    }

#if UNITY_EDITOR
    private void EnsureReadable()
    {
        if (colorMap.isReadable) return;
        string path = AssetDatabase.GetAssetPath(colorMap);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.isReadable = true; AssetDatabase.ImportAsset(path); }
    }
#endif
}
