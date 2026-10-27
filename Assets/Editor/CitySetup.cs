using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Procedurally builds a stylized city in FirstIsland on a flat plain at the
// foot of the mountains. The site is found automatically by scanning the
// terrain (flat footprint above sea level, high ground close by) — or place an
// empty GameObject named "CitySite" in the scene to pick the spot yourself.
// Deterministic for a given Seed, and idempotent: re-running replaces the city.
public static class CitySetup
{
    const string ScenePath = "Assets/Levels/LevelOne/FirstIsland.unity";
    const string CityRootName = "City";
    const string SiteMarkerName = "CitySite";
    const string MaterialDir = "Assets/Materials/City";
    const int Seed = 1337;

    // Layout: GridBlocks × GridBlocks blocks, each split into 2×2 lots.
    const int GridBlocks = 7;
    const float BlockSize = 56f;
    const float StreetWidth = 14f;
    const float Pitch = BlockSize + StreetWidth;
    const float CitySpan = GridBlocks * Pitch + StreetWidth;
    const float FloorHeight = 3.5f;

    // Site search. The terrain has no naturally flat area big enough, so the footprint is always
    // flattened afterwards — the search just picks the least hilly spot next to the mountains.
    const float SearchStep = 40f;
    const float MountainRing = 350f;   // how far beyond the city edge we look for mountains
    const float MinMountainRise = 40f; // high ground must rise at least this much above the plain
    const float MinDistFromSpawn = 400f;

    // Terrain flattening: flat out to FlattenMargin past the city edge, then blends back
    // to the original terrain over FlattenBlend metres. Original heights are backed up so
    // Remove City (or regenerating) restores them.
    const float FlattenMargin = 15f;
    const float FlattenBlend = 150f;
    const string BackupPath = MaterialDir + "/TerrainBackup.bytes";

    const int HelipadCount = 4;

    [MenuItem("Window/SuperHeli/Generate City")]
    public static void Generate()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("SuperHeli City", "Stop Play Mode first.", "OK");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (Terrain.activeTerrains.Length == 0)
        {
            EditorUtility.DisplayDialog("SuperHeli City", "No terrain found in the scene.", "OK");
            return;
        }

        // Start from a clean slate: drop any previous city and search the original
        // terrain, not one already flattened by a previous run.
        var root = GameObject.Find(CityRootName);
        if (root != null)
            Object.DestroyImmediate(root);
        RestoreTerrain();

        if (!FindSite(out Vector3 center, out string how))
        {
            EditorUtility.DisplayDialog("SuperHeli City",
                "Couldn't find a plain next to the mountains (see Console for details).\n\n" +
                $"Place an empty GameObject named '{SiteMarkerName}' where you want the city and run again.",
                "OK");
            return;
        }

        var mats = GetOrCreateMaterials();
        FlattenTerrain(center);
        root = new GameObject(CityRootName);
        root.transform.position = center;

        var rng = new System.Random(Seed);
        BuildGround(root.transform, center, mats);
        var buildings = BuildBlocks(root.transform, center, mats, rng);
        int pads = BuildHelipads(root.transform, buildings, mats);

        foreach (var t in root.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[CitySetup] City at {center} ({how}) — {buildings.Count} buildings, {pads} helipads, scene saved.");
        EditorUtility.DisplayDialog("SuperHeli City",
            $"City generated!\n\n" +
            $"• Location: {center.x:F0}, {center.y:F0}, {center.z:F0} ({how})\n" +
            $"• {buildings.Count} buildings, {pads} rooftop helipads\n" +
            $"• Size: {CitySpan:F0} × {CitySpan:F0} m\n\n" +
            "Select the 'City' GameObject and press F in the Scene view to find it.",
            "OK");
    }

    [MenuItem("Window/SuperHeli/Remove City")]
    public static void Remove()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("SuperHeli City", "Stop Play Mode first.", "OK");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var root = GameObject.Find(CityRootName);
        if (root != null)
            Object.DestroyImmediate(root);
        bool restored = RestoreTerrain();

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("SuperHeli City",
            $"City {(root != null ? "removed" : "not found")}, terrain {(restored ? "restored" : "unchanged")}. Scene saved.", "OK");
    }

    // ---------------------------------------------------------------- site

    static bool FindSite(out Vector3 center, out string how)
    {
        var marker = GameObject.Find(SiteMarkerName);
        if (marker != null)
        {
            var p = marker.transform.position;
            center = new Vector3(p.x, GroundHeight(p.x, p.z), p.z);
            how = $"from '{SiteMarkerName}' marker";
            return true;
        }

        float seaLevel = EstimateSeaLevel();
        Vector3? spawn = FindSpawn();
        float half = CitySpan * 0.5f;

        float bestScore = float.MinValue, bestRelief = 0f;
        center = Vector3.zero;

        // Scan the combined bounds of all terrain tiles — the city may straddle tile seams.
        var bounds = new Bounds(Terrain.activeTerrains[0].transform.position, Vector3.zero);
        foreach (var t in Terrain.activeTerrains)
        {
            bounds.Encapsulate(t.transform.position);
            bounds.Encapsulate(t.transform.position + t.terrainData.size);
        }

        int tested = 0, nearSpawn = 0, inWater = 0, noMountains = 0;
        for (float x = bounds.min.x + half; x <= bounds.max.x - half; x += SearchStep)
        {
            for (float z = bounds.min.z + half; z <= bounds.max.z - half; z += SearchStep)
            {
                tested++;
                if (spawn.HasValue && new Vector2(x - spawn.Value.x, z - spawn.Value.z).magnitude < MinDistFromSpawn + half)
                {
                    nearSpawn++;
                    continue;
                }

                // Flatness across the whole footprint.
                float min = float.MaxValue, max = float.MinValue, sum = 0f;
                int n = 0;
                for (int i = 0; i <= 6; i++)
                {
                    for (int j = 0; j <= 6; j++)
                    {
                        float h = GroundHeight(x - half + CitySpan * i / 6f, z - half + CitySpan * j / 6f);
                        min = Mathf.Min(min, h);
                        max = Mathf.Max(max, h);
                        sum += h;
                        n++;
                    }
                }
                float relief = max - min;
                float mean = sum / n;
                if (min < seaLevel + 3f)
                {
                    inWater++;
                    continue;
                }

                // "Where the mountains begin": highest ground in a ring just outside the city.
                float peak = float.MinValue;
                for (int a = 0; a < 24; a++)
                {
                    float ang = a * Mathf.PI * 2f / 24f;
                    for (float r = half + 60f; r <= half + MountainRing; r += 60f)
                        peak = Mathf.Max(peak, GroundHeight(x + Mathf.Cos(ang) * r, z + Mathf.Sin(ang) * r));
                }
                float rise = peak - mean;
                if (rise < MinMountainRise)
                {
                    noMountains++;
                    continue;
                }

                // Mountains just need to be clearly there; beyond that, least flattening wins.
                float score = Mathf.Min(rise, 150f) - relief * 4f - (mean - seaLevel) * 0.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRelief = relief;
                    center = new Vector3(x, mean, z);
                }
            }
        }

        Debug.Log($"[CitySetup] Site search: {Terrain.activeTerrains.Length} terrain(s), bounds {bounds.min}–{bounds.max}, " +
                  $"sea level {seaLevel:F1}. Tested {tested}: {nearSpawn} near spawn, {inWater} in water, " +
                  $"{noMountains} no mountains within {MountainRing} m (<{MinMountainRise} m rise). " +
                  $"Chosen site needs {bestRelief:F0} m of terrain flattened.");

        how = $"by the mountains, {bestRelief:F0} m of terrain flattened";
        return bestScore > float.MinValue;
    }

    // ---------------------------------------------------------------- terrain

    static void FlattenTerrain(Vector3 center)
    {
        float inner = CitySpan * 0.5f + FlattenMargin;
        float outer = inner + FlattenBlend;

        // World rect → heightmap sample range on each tile it overlaps.
        var touched = new List<(Terrain terrain, RectInt range)>();
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData;
            var o = t.transform.position;
            int res = td.heightmapResolution;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - outer - o.x) / td.size.x * (res - 1)), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + outer - o.x) / td.size.x * (res - 1)), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - outer - o.z) / td.size.z * (res - 1)), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + outer - o.z) / td.size.z * (res - 1)), 0, res - 1);
            if (x1 > x0 && z1 > z0)
                touched.Add((t, new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1)));
        }

        using (var w = new System.IO.BinaryWriter(System.IO.File.Create(BackupPath)))
        {
            w.Write(touched.Count);
            foreach (var (t, range) in touched)
            {
                var td = t.terrainData;
                var o = t.transform.position;
                int res = td.heightmapResolution;
                int x0 = range.x, z0 = range.y, cols = range.width, rows = range.height;

                var heights = td.GetHeights(x0, z0, cols, rows); // [row(z), col(x)], normalized 0..1

                // Tiles are identified by world position: FirstIsland's TerrainData is embedded
                // in the scene, so it has no asset GUID to key on.
                w.Write(o.x); w.Write(o.z);
                w.Write(x0); w.Write(z0); w.Write(cols); w.Write(rows);
                foreach (float h in heights)
                    w.Write(h);

                float target = (center.y - o.y) / td.size.y;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        float wx = o.x + (x0 + c) / (float)(res - 1) * td.size.x;
                        float wz = o.z + (z0 + r) / (float)(res - 1) * td.size.z;
                        // Square city → square flat area (Chebyshev distance), smooth blend outside it.
                        float d = Mathf.Max(Mathf.Abs(wx - center.x), Mathf.Abs(wz - center.z));
                        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d));
                        heights[r, c] = Mathf.Lerp(target, heights[r, c], blend);
                    }
                }

                td.SetHeights(x0, z0, heights);
                EditorUtility.SetDirty(td);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(BackupPath);
    }

    static bool RestoreTerrain()
    {
        if (!System.IO.File.Exists(BackupPath))
            return false;

        int failed = 0;
        using (var r = new System.IO.BinaryReader(System.IO.File.OpenRead(BackupPath)))
        {
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                float px = r.ReadSingle(), pz = r.ReadSingle();
                int x0 = r.ReadInt32(), z0 = r.ReadInt32(), cols = r.ReadInt32(), rows = r.ReadInt32();
                var heights = new float[rows, cols];
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < cols; col++)
                        heights[row, col] = r.ReadSingle();

                var terrain = System.Array.Find(Terrain.activeTerrains, t =>
                    Mathf.Approximately(t.transform.position.x, px) && Mathf.Approximately(t.transform.position.z, pz));
                if (terrain == null)
                {
                    Debug.LogError($"[CitySetup] No terrain tile at ({px}, {pz}) from the backup — can't restore it.");
                    failed++;
                    continue;
                }
                terrain.terrainData.SetHeights(x0, z0, heights);
                EditorUtility.SetDirty(terrain.terrainData);
            }
        }

        // Keep the backup if anything couldn't be restored, so nothing is lost.
        if (failed > 0)
            return false;

        AssetDatabase.DeleteAsset(BackupPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[CitySetup] Terrain restored from backup.");
        return true;
    }

    static float GroundHeight(float x, float z)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            var o = t.transform.position;
            var s = t.terrainData.size;
            if (x >= o.x && x <= o.x + s.x && z >= o.z && z <= o.z + s.z)
                return t.SampleHeight(new Vector3(x, 0f, z)) + o.y;
        }
        return float.MinValue; // off-terrain counts as unusable (fails the sea-level check)
    }

    static float EstimateSeaLevel()
    {
        // A large water/ocean surface, if the scene has one.
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            string name = r.gameObject.name.ToLowerInvariant();
            if ((name.Contains("ocean") || name.Contains("water")) && r.bounds.size.x > 500f)
                return r.bounds.center.y;
        }

        float lowest = float.MaxValue;
        foreach (var t in Terrain.activeTerrains)
            lowest = Mathf.Min(lowest, t.transform.position.y);
        return lowest;
    }

    static Vector3? FindSpawn()
    {
        // Matched by name so this editor assembly doesn't need a reference to SimCore.
        foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>())
            if (mb.GetType().Name == "SimulatorDriver")
                return mb.transform.position;
        return null;
    }

    // ---------------------------------------------------------------- build

    struct Building
    {
        public Vector3 roofCenter;
        public Vector2 footprint;
    }

    static void BuildGround(Transform root, Vector3 center, Materials mats)
    {
        // One asphalt slab for the streets; extends well below ground to cover small terrain dips.
        const float depth = 10f;
        CreateBox("Streets", root, center + Vector3.down * (depth * 0.5f - 0.15f),
            new Vector3(CitySpan, depth, CitySpan), mats.asphalt, mats.asphalt, 0f);
    }

    static List<Building> BuildBlocks(Transform root, Vector3 center, Materials mats, System.Random rng)
    {
        var buildings = new List<Building>();
        float half = CitySpan * 0.5f;
        float maxDist = half * 1.1f;

        for (int bx = 0; bx < GridBlocks; bx++)
        {
            for (int bz = 0; bz < GridBlocks; bz++)
            {
                var blockCenter = center + new Vector3(
                    -half + StreetWidth + bx * Pitch + BlockSize * 0.5f, 0f,
                    -half + StreetWidth + bz * Pitch + BlockSize * 0.5f);

                var block = new GameObject($"Block_{bx}_{bz}").transform;
                block.SetParent(root);
                block.position = blockCenter;

                // Sidewalk plinth
                CreateBox("Sidewalk", block, blockCenter + Vector3.up * 0.1f,
                    new Vector3(BlockSize, 0.5f, BlockSize), mats.concrete, mats.concrete, 0f);

                // Downtown in the middle: taller and denser toward the city center.
                float dist = new Vector2(blockCenter.x - center.x, blockCenter.z - center.z).magnitude;
                float centrality = 1f - Mathf.Clamp01(dist / maxDist);

                // Some downtown blocks get one big tower instead of four lots.
                if (centrality > 0.7f && rng.NextDouble() < 0.5)
                {
                    float floors = Mathf.Lerp(20, 38, (float)rng.NextDouble());
                    buildings.Add(AddBuilding(block, blockCenter, new Vector2(BlockSize - 10f, BlockSize - 10f), floors * FloorHeight, mats, rng));
                    continue;
                }

                const float lotInset = 3f;
                float lot = BlockSize * 0.5f;
                for (int lx = 0; lx < 2; lx++)
                {
                    for (int lz = 0; lz < 2; lz++)
                    {
                        // Suburban edges have the odd empty lot (parking/park).
                        if (centrality < 0.3f && rng.NextDouble() < 0.25)
                            continue;

                        var lotCenter = blockCenter + new Vector3((lx - 0.5f) * lot, 0f, (lz - 0.5f) * lot);
                        float w = lot - lotInset * 2f - (float)rng.NextDouble() * 6f;
                        float d = lot - lotInset * 2f - (float)rng.NextDouble() * 6f;

                        float maxFloors = Mathf.Lerp(4f, 22f, centrality * centrality);
                        float floors = Mathf.Max(2f, Mathf.Round(maxFloors * Mathf.Lerp(0.4f, 1f, (float)rng.NextDouble())));
                        buildings.Add(AddBuilding(block, lotCenter, new Vector2(w, d), floors * FloorHeight, mats, rng));
                    }
                }
            }
        }

        return buildings;
    }

    static Building AddBuilding(Transform parent, Vector3 lotCenter, Vector2 footprint, float height, Materials mats, System.Random rng)
    {
        // Sit the base on the lowest terrain point under the footprint and sink it a little,
        // so no building floats on gentle slopes.
        float baseY = float.MaxValue;
        for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
                baseY = Mathf.Min(baseY, GroundHeight(lotCenter.x + i * footprint.x * 0.5f, lotCenter.z + j * footprint.y * 0.5f));
        baseY = Mathf.Min(baseY, lotCenter.y) - 2f;

        float topY = lotCenter.y + height;
        var facade = mats.facades[rng.Next(mats.facades.Length)];
        var size = new Vector3(footprint.x, topY - baseY, footprint.y);
        CreateBox("Building", parent, new Vector3(lotCenter.x, (baseY + topY) * 0.5f, lotCenter.z), size, facade, mats.roof, FloorHeight);

        return new Building { roofCenter = new Vector3(lotCenter.x, topY, lotCenter.z), footprint = footprint };
    }

    static int BuildHelipads(Transform root, List<Building> buildings, Materials mats)
    {
        // Tallest buildings with roofs big enough for a 16 m pad.
        var candidates = buildings.FindAll(b => Mathf.Min(b.footprint.x, b.footprint.y) >= 20f);
        candidates.Sort((a, b) => b.roofCenter.y.CompareTo(a.roofCenter.y));

        int count = Mathf.Min(HelipadCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            var top = candidates[i].roofCenter;
            var pad = new GameObject($"Helipad_{i + 1}").transform;
            pad.SetParent(root);
            pad.position = top;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Pad";
            disc.transform.SetParent(pad);
            disc.transform.position = top + Vector3.up * 0.15f;
            disc.transform.localScale = new Vector3(16f, 0.15f, 16f);
            disc.GetComponent<Renderer>().sharedMaterial = mats.pad;

            // The "H": two uprights and a crossbar.
            CreateBox("H_Left", pad, top + new Vector3(-2.5f, 0.32f, 0f), new Vector3(1.2f, 0.05f, 7f), mats.marking, mats.marking, 0f);
            CreateBox("H_Right", pad, top + new Vector3(2.5f, 0.32f, 0f), new Vector3(1.2f, 0.05f, 7f), mats.marking, mats.marking, 0f);
            CreateBox("H_Bar", pad, top + new Vector3(0f, 0.32f, 0f), new Vector3(4f, 0.05f, 1.2f), mats.marking, mats.marking, 0f);
        }
        return count;
    }

    // Box with its own mesh so wall UVs are in world units (window texture tiles once per
    // floor regardless of building size). Submesh 0 = walls, submesh 1 = top/bottom.
    static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 size, Material walls, Material top, float tile)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;

        go.AddComponent<MeshFilter>().sharedMesh = BuildBoxMesh(size, tile);
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { walls, top };
        go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    static Mesh BuildBoxMesh(Vector3 size, float tile)
    {
        var h = size * 0.5f;
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var wallTris = new List<int>();
        var capTris = new List<int>();

        // Each face: normal, then the two in-plane axes (u along width, v up the face).
        void Face(Vector3 n, Vector3 u, Vector3 v, float uLen, float vLen, List<int> tris)
        {
            int start = verts.Count;
            var c = Vector3.Scale(n, h);
            var hu = u * (uLen * 0.5f);
            var hv = v * (vLen * 0.5f);
            verts.Add(c - hu - hv); verts.Add(c + hu - hv); verts.Add(c + hu + hv); verts.Add(c - hu + hv);
            for (int i = 0; i < 4; i++) normals.Add(n);

            float su = tile > 0f ? uLen / tile : 1f;
            float sv = tile > 0f ? vLen / tile : 1f;
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(su, 0)); uvs.Add(new Vector2(su, sv)); uvs.Add(new Vector2(0, sv));

            tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
            tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
        }

        Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y, wallTris);
        Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y, wallTris);
        Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y, wallTris);
        Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y, wallTris);
        Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z, capTris);
        Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z, capTris);

        var mesh = new Mesh { name = "CityBox" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(wallTris, 0);
        mesh.SetTriangles(capTris, 1);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- materials

    class Materials
    {
        public Material[] facades;
        public Material roof, asphalt, concrete, pad, marking;
    }

    static readonly Color[] FacadeColors =
    {
        new Color(0.78f, 0.74f, 0.68f), // sandstone
        new Color(0.62f, 0.64f, 0.67f), // grey concrete
        new Color(0.55f, 0.36f, 0.30f), // brick
        new Color(0.85f, 0.84f, 0.80f), // off-white
        new Color(0.45f, 0.52f, 0.58f), // blue-grey glass
    };

    static Materials GetOrCreateMaterials()
    {
        System.IO.Directory.CreateDirectory(MaterialDir);
        AssetDatabase.Refresh();

        var windows = GetOrCreateWindowTexture();
        var m = new Materials { facades = new Material[FacadeColors.Length] };
        for (int i = 0; i < FacadeColors.Length; i++)
            m.facades[i] = GetOrCreateMaterial($"Facade_{i}", FacadeColors[i], windows, 0.35f);

        m.roof = GetOrCreateMaterial("Roof", new Color(0.25f, 0.25f, 0.26f), null, 0.2f);
        m.asphalt = GetOrCreateMaterial("Asphalt", new Color(0.13f, 0.13f, 0.14f), null, 0.15f);
        m.concrete = GetOrCreateMaterial("Sidewalk", new Color(0.55f, 0.55f, 0.53f), null, 0.2f);
        m.pad = GetOrCreateMaterial("Helipad", new Color(0.2f, 0.32f, 0.22f), null, 0.2f);
        m.marking = GetOrCreateMaterial("Marking", Color.white, null, 0.3f);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Material GetOrCreateMaterial(string name, Color color, Texture2D map, float smoothness)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseColorMap", map);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // One floor × one window bay: light wall frame around a dark window pane.
    // Tinted per facade via _BaseColor, so the panes stay dark on every color.
    static Texture2D GetOrCreateWindowTexture()
    {
        string path = $"{MaterialDir}/Windows.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            return existing;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        var wall = new Color(1f, 1f, 1f);
        var glass = new Color(0.22f, 0.27f, 0.33f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool pane = x > 12 && x < 52 && y > 14 && y < 54;
                tex.SetPixel(x, y, pane ? glass : wall);
            }
        }
        tex.Apply();
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }
}
