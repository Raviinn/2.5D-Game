using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 14 setup (day/night &amp; weather): rain streak texture and material, an animated procedural sky,
    /// a "[Sky]" object with the DayNightCycle and WeatherSystem, lanterns beside every house door and at the
    /// contracts board, and a campfire at the bandit camp. Safe to re-run: lanterns and the campfire are rebuilt.
    /// </summary>
    public static class DayNightSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string MaterialFolder = EnvironmentArtGenerator.ArtRoot + "/Materials";
        const string RainTexturePath = EnvironmentArtGenerator.TextureFolder + "/Rain_Streak.png";
        const string RainMaterialPath = MaterialFolder + "/Weather_Rain.mat";
        const string SkyMaterialPath = MaterialFolder + "/Sky_Procedural.mat";
        const string GlowMaterialPath = MaterialFolder + "/Env_LanternGlow.mat";
        const string LightsName = "[Night Lights]";

        [MenuItem("Beast/Setup/Run Milestone 14 Setup (Day, Night & Weather)", priority = 13)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            if (Shader.Find("Beast/Rain") == null)
            {
                Debug.LogError("[Setup] Shader 'Beast/Rain' not found (Art/Shaders/Rain.shader). Let Unity finish importing, then try again.");
                return;
            }

            EnvironmentArtGenerator.EnsureFolder(MaterialFolder);
            var envMaterials = EnvironmentSetup.EnsureMaterials(EnvironmentArtGenerator.EnsureTextures());
            EnsureRainTexture();
            EnsureMaterial(RainMaterialPath, () =>
            {
                var m = new Material(Shader.Find("Beast/Rain"));
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RainTexturePath));
                return m;
            });
            EnsureMaterial(SkyMaterialPath, () =>
            {
                var m = new Material(Shader.Find("Skybox/Procedural"));
                m.SetFloat("_SunDisk", 2f); // high quality
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_SunSizeConvergence", 6f);
                m.SetFloat("_AtmosphereThickness", 1f);
                m.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
                m.SetColor("_GroundColor", new Color(0.33f, 0.32f, 0.3f));
                m.SetFloat("_Exposure", 1.3f);
                return m;
            });
            EnsureMaterial(GlowMaterialPath, () =>
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.SetColor("_BaseColor", new Color(1f, 0.62f, 0.25f));
                return m;
            });
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            var rain = AssetDatabase.LoadAssetAtPath<Material>(RainMaterialPath);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            var iron = AssetDatabase.LoadAssetAtPath<Material>(envMaterials["Env_Iron"]);
            var wood = AssetDatabase.LoadAssetAtPath<Material>(envMaterials["Env_Wood"]);
            var rock = AssetDatabase.LoadAssetAtPath<Material>(envMaterials["Env_Rock"]);

            Light sun = RenderSettings.sun;
            if (sun == null)
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (sun == null)
            {
                Debug.LogError("[Setup] No directional light (sun) in World_Test.");
                return;
            }
            RenderSettings.sun = sun;
            RenderSettings.skybox = sky;

            var skyObject = GameObject.Find("[Sky]") ?? new GameObject("[Sky]");
            if (!skyObject.TryGetComponent(out DayNightCycle cycle)) cycle = skyObject.AddComponent<DayNightCycle>();
            var so = new SerializedObject(cycle);
            so.FindProperty("sun").objectReferenceValue = sun;
            so.FindProperty("skybox").objectReferenceValue = sky;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!skyObject.TryGetComponent(out WeatherSystem weather)) weather = skyObject.AddComponent<WeatherSystem>();
            so = new SerializedObject(weather);
            so.FindProperty("rainMaterial").objectReferenceValue = rain;
            so.ApplyModifiedPropertiesWithoutUndo();

            var old = GameObject.Find(LightsName);
            if (old != null) Object.DestroyImmediate(old);
            var lights = new GameObject(LightsName).transform;

            int lanterns = 0;
            foreach (var door in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (door.name != "Door" || !UnderHouse(door)) continue;
                // A door decal's visible side faces -forward (see EnvironmentSetup.Decal).
                Vector3 outward = -door.forward;
                outward.y = 0f;
                outward.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, outward);
                Vector3 at = door.position + outward * 0.22f + side * 0.75f + Vector3.up * 0.55f;
                Lantern(lights, at, outward, iron, glow);
                lanterns++;
            }

            var board = Object.FindFirstObjectByType<ContractBoard>();
            if (board != null)
            {
                Vector3 at = board.transform.position + board.transform.right * 1.3f;
                at.y = 0f;
                Box("Lantern_Post", lights, at + Vector3.up * 1.1f, new Vector3(0.12f, 2.2f, 0.12f), wood, collider: true);
                Lantern(lights, at + Vector3.up * 2.05f + board.transform.forward * -0.2f, -board.transform.forward, iron, glow);
                lanterns++;
            }

            var camp = CampCenter();
            string campReport = "no bandit camp found";
            if (camp.HasValue)
            {
                Campfire(lights, camp.Value, rock, glow);
                campReport = "campfire at the bandit camp";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 14 day/night & weather setup complete: {lanterns} lantern(s), {campReport}. " +
                      "Press F1 to skip a day, F2 an hour, F4 to change the weather.");
        }

        static bool UnderHouse(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name.StartsWith("House_")) return true;
            return false;
        }

        static Vector3? CampCenter()
        {
            var sum = Vector3.zero;
            int count = 0;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy.Data == null || enemy.Data.IsTrainingDummy) continue;
                sum += enemy.transform.position;
                count++;
            }
            if (count == 0) return null;
            var center = sum / count;
            center.y = 0f;
            return center;
        }

        static void Lantern(Transform parent, Vector3 at, Vector3 outward, Material iron, Material glow)
        {
            var root = new GameObject("Lantern").transform;
            root.SetParent(parent, false);
            root.position = at;
            root.rotation = Quaternion.LookRotation(outward);
            Box("Cap", root, at + Vector3.up * 0.14f, new Vector3(0.24f, 0.05f, 0.24f), iron, collider: false);
            var glass = Box("Glass", root, at, new Vector3(0.16f, 0.22f, 0.16f), glow, collider: false);
            Box("Bracket", root, at - outward * 0.12f + Vector3.up * 0.14f, new Vector3(0.04f, 0.04f, 0.24f), iron, collider: false);

            var light = root.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.color = new Color(1f, 0.68f, 0.38f);
            light.shadows = LightShadows.None;
            light.intensity = 0f;
            var lantern = root.gameObject.AddComponent<LanternLight>();
            var so = new SerializedObject(lantern);
            so.FindProperty("glow").objectReferenceValue = glass.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Campfire(Transform parent, Vector3 at, Material rock, Material glow)
        {
            var root = new GameObject("Campfire").transform;
            root.SetParent(parent, false);
            root.position = at + Vector3.up * 0.8f; // the light sits above the embers
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                Box("Stone", root, at + new Vector3(Mathf.Cos(a), 0.09f, Mathf.Sin(a)) * 0.55f, new Vector3(0.24f, 0.18f, 0.2f), rock, collider: false)
                    .transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            }
            var embers = Box("Embers", root, at + Vector3.up * 0.08f, new Vector3(0.6f, 0.12f, 0.6f), glow, collider: false);

            var light = root.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.color = new Color(1f, 0.5f, 0.22f);
            light.shadows = LightShadows.None;
            light.intensity = 0f;
            var fire = root.gameObject.AddComponent<LanternLight>();
            var so = new SerializedObject(fire);
            so.FindProperty("glow").objectReferenceValue = embers.GetComponent<Renderer>();
            so.FindProperty("intensity").floatValue = 3.2f;
            so.FindProperty("flicker").floatValue = 0.3f;
            so.FindProperty("onAtDarkness").floatValue = 0.1f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Box(string name, Transform parent, Vector3 worldPosition, Vector3 size, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = worldPosition;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static void EnsureRainTexture()
        {
            if (File.Exists(RainTexturePath)) return;
            // A thin streak: bright core, soft edges, fading toward the tail.
            const int w = 4, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float across = x is 1 or 2 ? 1f : 0.35f;
                    float along = Mathf.Sin((y + 0.5f) / h * Mathf.PI);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, across * along));
                }
            File.WriteAllBytes(RainTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RainTexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RainTexturePath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void EnsureMaterial(string path, System.Func<Material> create)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return; // keep your tuning
            AssetDatabase.CreateAsset(create(), path);
        }
    }
}
