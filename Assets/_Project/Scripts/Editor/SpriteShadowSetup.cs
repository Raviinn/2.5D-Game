using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 12 setup (per-pixel sprite shadows): moves the character sprite material and the environment
    /// billboard materials (trees, bushes, grass, flowers) onto the "Beast/Sprite Lit" shader, which receives real
    /// shadows without being shaded by each sprite's own shadow quad, and lets those renderers receive shadows.
    /// Safe to re-run; the materials keep their textures and colours.
    /// </summary>
    public static class SpriteShadowSetup
    {
        public const string ShaderName = "Beast/Sprite Lit";
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string CharacterMaterialPath = Root + "/Art/Materials/Sprite_Lit.mat";
        const string EnvironmentMaterialFolder = EnvironmentArtGenerator.ArtRoot + "/Materials";

        /// <summary>How far toward the sun each kind of sprite looks past itself: a bit more than half its width.</summary>
        static readonly (string material, float offset)[] EnvironmentMaterials =
        {
            ("Env_Oak", 3f), ("Env_Pine", 2.2f), ("Env_DeadTree", 2.6f), ("Env_Bush", 1.2f), ("Env_GrassTuft", 0.3f), ("Env_Flowers", 0.3f),
        };

        public const float CharacterOffset = 1.2f;

        [MenuItem("Beast/Setup/Run Milestone 12 Setup (Sprite Shadows)", priority = 11)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (Shader.Find(ShaderName) == null)
            {
                Debug.LogError($"[Setup] Shader '{ShaderName}' not found (Art/Shaders/SpriteLit.shader). Let Unity finish importing, then try again.");
                return;
            }

            int converted = 0;
            var character = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialPath);
            if (character != null && Convert(character, CharacterOffset)) converted++;
            foreach (var (name, offset) in EnvironmentMaterials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{EnvironmentMaterialFolder}/{name}.mat");
                if (material != null && Convert(material, offset)) converted++;
            }
            AssetDatabase.SaveAssets();

            int renderers = 0;
            if (File.Exists(TestWorldScenePath))
            {
                var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
                foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    // Bodies only: shadow-only quads never draw, so receiving makes no difference to them.
                    if (renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || renderer.receiveShadows) continue;
                    if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != ShaderName) continue;
                    renderer.receiveShadows = true;
                    renderers++;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[Setup] Milestone 12 sprite shadow setup complete: {converted} material(s) on {ShaderName}, " +
                      $"{renderers} sprite(s) now receive shadows. Characters darken per pixel in the shade of buildings, trees and each other.");
        }

        /// <summary>Puts a sprite material on Beast/Sprite Lit (keeping its texture and tint). False if it already was.</summary>
        public static bool Convert(Material material, float selfShadowOffset)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) return false;
            bool changed = material.shader != shader;
            if (changed)
            {
                var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                var scale = material.HasProperty("_BaseMap") ? material.GetTextureScale("_BaseMap") : Vector2.one;
                var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                material.shader = shader;
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Cutoff", 0.5f);
                material.SetColor("_EmissionColor", Color.black);
                material.shaderKeywords = new string[0];
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (!Mathf.Approximately(material.GetFloat("_SelfShadowOffset"), selfShadowOffset))
            {
                material.SetFloat("_SelfShadowOffset", selfShadowOffset);
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(material);
            return changed;
        }
    }
}
