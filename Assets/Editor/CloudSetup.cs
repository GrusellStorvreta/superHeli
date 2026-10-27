using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Populates FirstIsland with flyable Local Volumetric Fog "clouds" and enables
// volumetric fog on the scene's existing global Fog override (it was off by
// default — the override itself already exists on Sky and Fog Settings Profile).
public static class CloudSetup
{
    const string ScenePath = "Assets/Levels/LevelOne/FirstIsland.unity";
    const string FogProfilePath = "Assets/Scenes/FirstLevel/Sky and Fog Settings Profile.asset";
    const string CloudsRootName = "Clouds";
    const string MaskDir = "Assets/Textures/CloudMasks";
    const int MaskSize = 32; // matches HDRP's default Local Volumetric Fog atlas resolution
    const int MaskVariantCount = 3;

    struct CloudSpec
    {
        public Vector3 position;
        public Vector3 size;
        public CloudSpec(Vector3 position, Vector3 size)
        {
            this.position = position;
            this.size = size;
        }
    }

    // Hand-placed around the home/spawn area (~749, 45, 199) and along the
    // Level 2/3 checkpoint corridor, at altitudes below the mission checkpoints
    // so flying to/from them naturally passes through a few.
    static readonly CloudSpec[] Clouds =
    {
        new CloudSpec(new Vector3(600, 140, 100),   new Vector3(100, 45, 90)),
        new CloudSpec(new Vector3(850, 160, 320),   new Vector3(120, 50, 110)),
        new CloudSpec(new Vector3(950, 130, 60),    new Vector3(90, 40, 80)),
        new CloudSpec(new Vector3(550, 180, 280),   new Vector3(110, 55, 100)),
        new CloudSpec(new Vector3(780, 150, -50),   new Vector3(85, 42, 95)),
        new CloudSpec(new Vector3(749, 130, 350),   new Vector3(90, 45, 90)),
        new CloudSpec(new Vector3(900, 200, 150),   new Vector3(130, 55, 120)),
        new CloudSpec(new Vector3(1080, 190, 60),   new Vector3(100, 45, 90)),
        new CloudSpec(new Vector3(1180, 210, -20),  new Vector3(110, 50, 100)),
        new CloudSpec(new Vector3(1000, 290, -80),  new Vector3(140, 60, 130)),
        new CloudSpec(new Vector3(1200, 310, -150), new Vector3(120, 55, 110)),
        new CloudSpec(new Vector3(750, 300, 250),   new Vector3(100, 50, 90)),
    };

    [MenuItem("Window/SuperHeli/Setup Clouds")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("SuperHeli Clouds", "Stop Play Mode first.", "OK");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        bool fogOk = EnableVolumetricFog();
        CreateCloudVolumes();

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[CloudSetup] Done — {Clouds.Length} cloud volumes placed, scene saved.");
        EditorUtility.DisplayDialog("SuperHeli Clouds",
            $"Setup complete!\n\n" +
            $"• {Clouds.Length} cloud volumes under a new '{CloudsRootName}' GameObject\n" +
            $"• Volumetric Fog on the global Fog override: {(fogOk ? "enabled, range extended to 2000m" : "FAILED — see Console")}\n\n" +
            "Fly around near the helipad or toward the Level 2/3 checkpoints to find them.",
            "OK");
    }

    static bool EnableVolumetricFog()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FogProfilePath);
        if (profile == null)
        {
            Debug.LogError($"[CloudSetup] Could not find volume profile at '{FogProfilePath}'.");
            return false;
        }

        if (!profile.TryGet(out Fog fog))
        {
            Debug.LogError("[CloudSetup] Profile has no Fog override to enable volumetrics on.");
            return false;
        }

        fog.enableVolumetricFog.value = true;
        fog.enableVolumetricFog.overrideState = true;

        // Default depthExtent (64m) only fills the volumetric buffer a short
        // distance from the camera — clouds placed hundreds of meters out
        // never got sampled until the helicopter was almost inside one.
        fog.depthExtent.value = 2000f;
        fog.depthExtent.overrideState = true;

        // Skew slice distribution further toward uniform coverage so the
        // much larger depth range doesn't starve the 200-700m band where
        // the clouds actually sit in favor of the first few meters.
        fog.sliceDistributionUniformity.value = 0.9f;
        fog.sliceDistributionUniformity.overrideState = true;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return true;
    }

    static void CreateCloudVolumes()
    {
        var masks = GetOrCreateCloudMasks();

        var root = GameObject.Find(CloudsRootName);
        if (root == null)
            root = new GameObject(CloudsRootName);

        // Idempotent: clear out any clouds from a previous run before recreating.
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        for (int i = 0; i < Clouds.Length; i++)
        {
            var spec = Clouds[i];
            var go = new GameObject($"Cloud_{i + 1}");
            go.transform.SetParent(root.transform);
            go.transform.position = spec.position;
            // Spread a handful of reused mask textures into more variety.
            go.transform.rotation = Quaternion.Euler(0f, (i * 47) % 360, 0f);

            var fog = go.AddComponent<LocalVolumetricFog>();
            fog.parameters.albedo = new Color(0.95f, 0.96f, 1f);
            fog.parameters.meanFreePath = 20f;
            fog.parameters.anisotropy = 0.3f;
            fog.parameters.size = spec.size;
            fog.parameters.falloffMode = LocalVolumetricFogFalloffMode.Exponential;
            fog.parameters.volumeMask = masks[i % masks.Length];

            // Small blend margin — the mask texture itself already tapers
            // density to 0 at its own edges, so this just softens the box
            // boundary rather than doing the heavy lifting of the cloud shape.
            float blend = Mathf.Min(spec.size.x, spec.size.y, spec.size.z) * 0.15f;
            var fade = new Vector3(blend / spec.size.x, blend / spec.size.y, blend / spec.size.z);
            fog.parameters.positiveFade = fade;
            fog.parameters.negativeFade = fade;
        }
    }

    // Builds (or loads, if already generated) a handful of cumulus-shaped
    // density masks: a dome-like base (round top, flatter bottom) eroded by
    // layered noise so the silhouette reads as puffy/tufted rather than a
    // smooth gradient blob.
    static Texture3D[] GetOrCreateCloudMasks()
    {
        System.IO.Directory.CreateDirectory(MaskDir);
        AssetDatabase.Refresh();

        var masks = new Texture3D[MaskVariantCount];
        for (int i = 0; i < MaskVariantCount; i++)
        {
            string path = $"{MaskDir}/CloudMask_{i}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
            if (existing != null)
            {
                masks[i] = existing;
                continue;
            }

            var tex = BuildCloudMaskTexture(seed: i * 1013);
            AssetDatabase.CreateAsset(tex, path);
            masks[i] = tex;
        }

        AssetDatabase.SaveAssets();
        return masks;
    }

    static Texture3D BuildCloudMaskTexture(int seed)
    {
        var tex = new Texture3D(MaskSize, MaskSize, MaskSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        // Random per-variant offset into noise space so each mask is distinct.
        var rng = new System.Random(seed);
        var offset = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble()) * 100f;

        var colors = new Color32[MaskSize * MaskSize * MaskSize];
        for (int z = 0; z < MaskSize; z++)
        {
            for (int y = 0; y < MaskSize; y++)
            {
                for (int x = 0; x < MaskSize; x++)
                {
                    // Cube coordinate in [-1, 1].
                    var p = new Vector3(x, y, z) / (MaskSize - 1) * 2f - Vector3.one;

                    // Cumulus base shape: squash the underside so the cloud
                    // has a flatter bottom and a rounder, fuller top.
                    float vertical = p.y > 0f ? p.y : p.y * 1.6f;
                    float dist = new Vector3(p.x, vertical, p.z).magnitude;
                    float baseShape = 1f - Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, dist));

                    // Erode the base shape with layered noise so edges look
                    // tufted/lumpy instead of a smooth gradient falloff.
                    float noise = Fbm3D(p * 2f + offset, octaves: 4);
                    float density = Mathf.Clamp01((baseShape * Mathf.Lerp(0.5f, 1.2f, noise) - 0.15f) / 0.5f);

                    byte v = (byte)(density * 255);
                    colors[x + y * MaskSize + z * MaskSize * MaskSize] = new Color32(v, v, v, v);
                }
            }
        }

        tex.SetPixels32(colors);
        tex.Apply();
        return tex;
    }

    // Cheap pseudo-3D fractal noise: each octave averages three 2D Perlin
    // lookups on orthogonal planes (Mathf.PerlinNoise has no 3D overload).
    static float Fbm3D(Vector3 p, int octaves)
    {
        float sum = 0f, amplitude = 0.5f, frequency = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            var s = p * frequency;
            float n = (Mathf.PerlinNoise(s.x, s.y) + Mathf.PerlinNoise(s.y, s.z) + Mathf.PerlinNoise(s.z, s.x)) / 3f;
            sum += n * amplitude;
            norm += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return sum / norm;
    }
}
