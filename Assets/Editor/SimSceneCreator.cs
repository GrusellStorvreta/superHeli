#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

// Editor utility to programmatically create and save a sample scene that hosts the SimulatorDriver.
// Place this script under Assets/Editor so it is compiled into the Editor assembly.

public static class SimSceneCreator
{
    [MenuItem("Tools/Create Sample Scene")]
    public static void CreateSampleScene()
    {
        // Cannot create scenes during play mode
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Error", "Cannot create scene during play mode. Please stop playing first.", "OK");
            return;
        }

        // Ensure Scenes folder exists inside Assets
        string scenesDir = Path.Combine(Application.dataPath, "Scenes");
        if (!Directory.Exists(scenesDir))
        {
            Directory.CreateDirectory(scenesDir);
        }

        // Create a new scene (single, default game objects)
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Add a GameObject named "Simulator" with the SimulatorDriver component
        GameObject simGO = new GameObject("Simulator");
        simGO.AddComponent<SimCore.SimulatorDriver>();

        // Add HUD to display cyclic, pedals, collective
        GameObject hudGO = new GameObject("SimulatorHUD");
        hudGO.AddComponent<SimCore.SimulatorHUD>();

        // Try to instantiate the MD-500 model from Assets/Models/MD500/MD-500.fbx as the player helicopter
        string heliPath = "Assets/Models/MD500/MD-500.fbx";
        var heliAsset = AssetDatabase.LoadAssetAtPath<GameObject>(heliPath);
        GameObject heliInstance = null;
        if (heliAsset != null)
        {
            heliInstance = (GameObject)PrefabUtility.InstantiatePrefab(heliAsset);
            if (heliInstance != null)
            {
                heliInstance.name = "PlayerHelicopter";
                heliInstance.transform.position = Vector3.zero;
                // Add simple HelicopterPlayer component to drive rotors/visuals from simulator input
                var player = heliInstance.AddComponent<SimCore.HelicopterPlayer>();

                // Try to auto-wire a rotor child if present
                Transform rotor = heliInstance.transform.Find("MainRotor") ?? heliInstance.transform.Find("Rotor") ?? heliInstance.transform.Find("main_rotor") ?? heliInstance.transform.Find("rotor");
                if (rotor != null)
                {
                    player.rotorTransform = rotor;
                }
                else
                {
                    Debug.Log("PlayerHelicopter instantiated but no rotor child found (expected names: MainRotor/Rotor). You can assign rotor in the HelicopterPlayer inspector.");
                }

                Debug.Log("PlayerHelicopter instantiated from " + heliPath);
            }
            else
            {
                Debug.LogWarning("Failed to instantiate heli prefab from: " + heliPath);
            }
        }
        else
        {
            Debug.LogWarning("MD-500 model not found at: " + heliPath + " — place the FBX at that path to auto-instantiate.");
        }

        // Camera: create or position Main Camera behind and above the helicopter
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }

        // Add CameraFollow component and set offset
        if (heliInstance != null && mainCam != null)
        {
            var follow = mainCam.GetComponent<SimCore.CameraFollow>();
            if (follow == null) follow = mainCam.gameObject.AddComponent<SimCore.CameraFollow>();
            follow.target = heliInstance.transform;
            follow.offset = new Vector3(0f, 5f, -12f);
            mainCam.transform.position = heliInstance.transform.position + follow.offset;
            mainCam.transform.LookAt(heliInstance.transform.position + Vector3.up * 1.5f);
        }

        // Optionally set scene settings here (lighting, camera tags, etc.)

        // Save the scene asset under Assets/Scenes/SampleScene.unity
        string scenePath = "Assets/Scenes/SampleScene.unity";
        bool saved = EditorSceneManager.SaveScene(scene, scenePath);
        if (saved)
        {
            Debug.Log("Sample scene created and saved to: " + scenePath);
            AssetDatabase.Refresh();
        }
        else
        {
            Debug.LogError("Failed to save sample scene to: " + scenePath);
        }
    }
}
#endif
