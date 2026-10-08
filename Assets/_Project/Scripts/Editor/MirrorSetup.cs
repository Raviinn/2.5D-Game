using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 28 setup (names &amp; the mirror): a standing mirror at the homestead for changing your look and name,
    /// and a recompile of the Ink story (it gains player_name()). Name entry in the creator needs no setup.
    /// Safe to re-run: an existing mirror is kept.
    /// </summary>
    public static class MirrorSetup
    {
        const string TestWorldScenePath = "Assets/_Project/Scenes/World_Test.unity";
        const string GlassPath = "Assets/_Project/Art/Materials/Mirror_Glass.mat";
        const string MirrorName = "Mirror";

        [MenuItem("Beast/Setup/Run Milestone 28 Setup (Names & Mirror)", priority = 24)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            bool ink = DialogueQuestSetup.CompileInk();
            var materialPaths = EnvironmentSetup.EnsureMaterials(EnvironmentArtGenerator.EnsureTextures());
            EnsureGlass();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            var wood = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Wood"]);
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);

            string report;
            var mirror = GameObject.Find(MirrorName);
            if (mirror != null)
            {
                report = "mirror kept";
            }
            else
            {
                var bed = Object.FindFirstObjectByType<SleepSpot>();
                if (bed == null)
                {
                    Debug.LogError("[Setup] No bed (Sleep Spot) in World_Test. Run the Milestone 5 setup first.");
                    return;
                }
                mirror = BuildMirror(FindSpot(bed.transform), bed.transform, wood, glass);
                report = $"mirror placed near the bed at {mirror.transform.position:F1}";
            }
            if (!mirror.TryGetComponent(out Mirror _)) mirror.AddComponent<Mirror>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 28 names & mirror setup complete: {report}; Ink story {(ink ? "recompiled (player_name())" : "NOT recompiled — see the [Ink] errors")}.");
        }

        static void EnsureGlass()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(GlassPath) != null) return;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.72f, 0.80f, 0.86f));
            material.SetFloat("_Smoothness", 0.95f);
            material.SetFloat("_Metallic", 0.6f);
            AssetDatabase.CreateAsset(material, GlassPath);
        }

        /// <summary>A clear spot near the bed, out of the way of the workbench and chest.</summary>
        static Vector3 FindSpot(Transform bed)
        {
            Physics.SyncTransforms();
            var directions = new[] { -bed.forward, bed.forward, -bed.right, bed.right };
            foreach (float distance in new[] { 2f, 2.8f, 3.6f, 4.5f })
                foreach (var direction in directions)
                {
                    var flat = new Vector3(direction.x, 0f, direction.z).normalized;
                    var spot = bed.position + flat * distance;
                    var from = new Vector3(spot.x, bed.position.y + 1.5f, spot.z);
                    if (!Physics.Raycast(from, Vector3.down, out var ground, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.CheckBox(ground.point + Vector3.up * 1.1f, new Vector3(0.7f, 0.9f, 0.7f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return ground.point;
                }
            return bed.position - bed.forward * 2f;
        }

        static GameObject BuildMirror(Vector3 groundPoint, Transform bed, Material wood, Material glass)
        {
            var mirror = new GameObject(MirrorName);
            mirror.transform.position = groundPoint;
            var toBed = bed.position - groundPoint;
            toBed.y = 0f;
            if (toBed.sqrMagnitude > 0.01f) mirror.transform.rotation = Quaternion.LookRotation(toBed); // faces the homestead
            mirror.isStatic = true;
            var collider = mirror.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.95f, 0f);
            collider.size = new Vector3(0.9f, 1.9f, 0.4f);

            Part("Foot", mirror.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 0.4f), wood);
            foreach (var x in new[] { -0.4f, 0.4f })
                Part("Post", mirror.transform, new Vector3(x, 0.95f, 0f), new Vector3(0.08f, 1.8f, 0.08f), wood);
            Part("Frame", mirror.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.72f, 1.2f, 0.06f), wood);
            Part("Glass", mirror.transform, new Vector3(0f, 1.15f, 0.035f), new Vector3(0.6f, 1.08f, 0.02f), glass);
            Part("Crest", mirror.transform, new Vector3(0f, 1.82f, 0f), new Vector3(0.5f, 0.1f, 0.08f), wood);
            return mirror;
        }

        static void Part(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            go.isStatic = true;
        }
    }
}
