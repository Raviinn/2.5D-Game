using System.Collections.Generic;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.EditorTools
{
    /// <summary>
    /// One-click Milestone 1 setup: folders, GameConfig, GameDatabase, Bootstrap + greybox test scene,
    /// Build Settings, and fast Enter Play Mode. Safe to run again; existing scenes/assets are kept.
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project";
        const string BootstrapScenePath = Root + "/Scenes/Bootstrap.unity";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string InputActionsPath = Root + "/Settings/GameControls.inputactions";
        const string ConfigPath = Root + "/Resources/GameConfig.asset";
        const string DatabasePath = Root + "/Data/GameDatabase.asset";
        const string MaterialsFolder = Root + "/Art/Materials";

        static readonly string[] Folders =
        {
            "Art/Characters", "Art/Environment", "Art/Materials", "Art/UI", "Art/VFX",
            "Audio", "Data", "Prefabs", "Resources", "Scenes", "Settings",
        };

        [MenuItem("Beast/Setup/Run Milestone 1 Setup", priority = 0)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (string folder in Folders) EnsureFolder($"{Root}/{folder}");

            var database = LoadOrCreate<GameDatabase>(DatabasePath);
            GameDatabaseBuilder.Rebuild(database);

            var config = LoadOrCreate<GameConfig>(ConfigPath);
            config.Database = database;
            config.InputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (config.InputActions == null)
                Debug.LogError($"[Setup] Input actions not found at {InputActionsPath}.");
            EditorUtility.SetDirty(config);

            CreateSceneIfMissing(BootstrapScenePath, BuildBootstrapScene);
            CreateSceneIfMissing(TestWorldScenePath, BuildTestWorldScene);
            SetBuildScenes(BootstrapScenePath, TestWorldScenePath);

            // Fast Play Mode: skip domain reload. Core code resets its statics to support this.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(TestWorldScenePath);
            Debug.Log("[Setup] Milestone 1 setup complete. Press Play in World_Test (or Bootstrap).");
        }

        // ---------- Scenes ----------

        static void BuildBootstrapScene()
        {
            // Only a camera, so the screen is black (not "No cameras rendering") during the first load.
            var cameraObject = new GameObject("Boot Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
        }

        static void BuildTestWorldScene()
        {
            var groundMat = LoadOrCreateMaterial("Greybox_Ground", new Color(0.36f, 0.42f, 0.30f));
            var blockMat = LoadOrCreateMaterial("Greybox_Block", new Color(0.62f, 0.58f, 0.52f));
            var accentMat = LoadOrCreateMaterial("Greybox_Accent", new Color(0.60f, 0.34f, 0.24f));
            var playerMat = LoadOrCreateMaterial("Greybox_Player", new Color(0.20f, 0.45f, 0.85f));

            var sun = new GameObject("Directional Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var environment = new GameObject("Environment").transform;
            Primitive(PrimitiveType.Plane, "Ground", environment, Vector3.zero, new Vector3(10f, 1f, 10f), Quaternion.identity, groundMat);
            Primitive(PrimitiveType.Cube, "House_A", environment, new Vector3(9f, 1.5f, 6f), new Vector3(4f, 3f, 5f), Quaternion.identity, blockMat);
            Primitive(PrimitiveType.Cube, "House_B", environment, new Vector3(-9f, 2f, 5f), new Vector3(5f, 4f, 5f), Quaternion.Euler(0f, 20f, 0f), blockMat);
            Primitive(PrimitiveType.Cube, "House_C", environment, new Vector3(4f, 1.25f, -11f), new Vector3(6f, 2.5f, 4f), Quaternion.identity, blockMat);
            Primitive(PrimitiveType.Cube, "Tower", environment, new Vector3(-7f, 4f, -9f), new Vector3(2.5f, 8f, 2.5f), Quaternion.identity, accentMat);
            Primitive(PrimitiveType.Cube, "Ramp", environment, new Vector3(0f, 1f, 14f), new Vector3(3f, 0.3f, 8f), Quaternion.Euler(-15f, 0f, 0f), accentMat);
            for (int i = 0; i < 5; i++)
            {
                float height = 0.25f * (i + 1);
                Primitive(PrimitiveType.Cube, $"Step_{i}", environment,
                    new Vector3(-14f + i * 1.2f, height * 0.5f, -2f), new Vector3(1.2f, height, 3f), Quaternion.identity, accentMat);
            }

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 1.1f, 0f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.GetComponent<MeshRenderer>().sharedMaterial = playerMat;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = Vector3.zero;

            // Facing indicator so rotation is visible on the capsule.
            var nose = Primitive(PrimitiveType.Cube, "FacingIndicator", player.transform, Vector3.zero, new Vector3(0.2f, 0.2f, 0.4f), Quaternion.identity, accentMat);
            Object.DestroyImmediate(nose.GetComponent<BoxCollider>());
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);

            var motor = player.AddComponent<PlayerMotor>();
            var saveable = player.AddComponent<SaveableTransform>();
            SetString(saveable, "saveId", "player");

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, -5f), Quaternion.Euler(15f, 0f, 0f));
            var orbit = cameraObject.AddComponent<ThirdPersonCamera>();
            SetReference(orbit, "target", player.transform);
            SetReference(motor, "cameraTransform", cameraObject.transform);

            new GameObject("EventProbe").AddComponent<EventProbe>();
        }

        // ---------- Helpers ----------

        static void CreateSceneIfMissing(string path, System.Action build)
        {
            if (File.Exists(path))
            {
                Debug.Log($"[Setup] {path} already exists; kept as-is.");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            build();
            EditorSceneManager.SaveScene(scene, path);
        }

        static void SetBuildScenes(params string[] requiredFirst)
        {
            var scenes = requiredFirst.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !requiredFirst.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = parent != null && parent.name == "Environment";
            return go;
        }

        static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
