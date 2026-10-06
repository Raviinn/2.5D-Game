using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 3 setup: placeholder sprite sheets, the shared sprite material, and a sprite Visual
    /// on the player and every enemy in World_Test (replacing the capsule meshes). Safe to re-run.
    /// </summary>
    public static class SpriteVisualsSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string SpriteFolder = Root + "/Art/Characters/Placeholder";
        const string SheetFolder = Root + "/Data/Visuals";
        const string MaterialPath = Root + "/Art/Materials/Sprite_Lit.mat";

        internal static readonly PlaceholderSpriteGenerator.Palette KnightPalette = new()
        {
            Body = new Color32(70, 95, 140, 255), Trim = new Color32(200, 170, 80, 255), Skin = new Color32(232, 192, 160, 255),
            Hair = new Color32(150, 156, 168, 255), Weapon = new Color32(215, 220, 230, 255), Outline = new Color32(24, 20, 30, 255),
            HasWeapon = true, HasShield = true,
        };

        [MenuItem("Beast/Setup/Run Milestone 3 Setup (Sprites)", priority = 2)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test scene not found. Run Milestone 1 and 2 setups first.");
                return;
            }

            EnsureFolder(SpriteFolder);
            EnsureFolder(SheetFolder);

            string knightPath = CreateSheet("Knight", KnightPalette);
            string banditPath = CreateSheet("Bandit", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(135, 55, 45, 255), Trim = new Color32(64, 48, 36, 255), Skin = new Color32(212, 160, 120, 255),
                Hair = new Color32(52, 36, 30, 255), Weapon = new Color32(185, 185, 175, 255), Outline = new Color32(24, 16, 14, 255),
                HasWeapon = true,
            });
            string dummyPath = CreateSheet("TrainingDummy", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(200, 170, 90, 255), Trim = new Color32(120, 90, 50, 255), Skin = new Color32(222, 196, 122, 255),
                Hair = new Color32(172, 140, 70, 255), Outline = new Color32(50, 36, 20, 255),
            });
            CreateSpriteMaterial();

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);

            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            var knight = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(knightPath);
            var bandit = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(banditPath);
            var dummy = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(dummyPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            var player = GameObject.FindWithTag("Player");
            if (player != null) AddVisual(player, knight, material);
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AddVisual(enemy.gameObject, enemy.name.Contains("Dummy") ? dummy : bandit, material);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Setup] Milestone 3 sprite setup complete. Characters render as sprites in Play Mode " +
                      "(the Scene view shows cyan boxes for them).");
        }

        internal static string CreateSheet(string characterName, PlaceholderSpriteGenerator.Palette palette)
        {
            string sheetPath = $"{SheetFolder}/Sheet_{characterName}.asset";
            var texture = PlaceholderSpriteGenerator.CreateSheetTexture($"{SpriteFolder}/{characterName}_Placeholder.png", palette);

            var sheet = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(sheetPath);
            if (sheet != null)
            {
                if (sheet.Texture == null)
                {
                    sheet.Texture = texture;
                    EditorUtility.SetDirty(sheet);
                }
                return sheetPath;
            }

            sheet = ScriptableObject.CreateInstance<DirectionalSpriteSheet>();
            sheet.Texture = texture;
            sheet.CellSize = new Vector2Int(PlaceholderSpriteGenerator.CellWidth, PlaceholderSpriteGenerator.CellHeight);
            sheet.PixelsPerUnit = PlaceholderSpriteGenerator.PixelsPerUnit;
            sheet.Pivot = new Vector2(0.5f, PlaceholderSpriteGenerator.PivotY);
            sheet.MirrorLeft = true;

            var layout = PlaceholderSpriteGenerator.Layout;
            sheet.Clips = new DirectionalSpriteSheet.Clip[layout.Length];
            for (int i = 0; i < layout.Length; i++)
            {
                sheet.Clips[i] = new DirectionalSpriteSheet.Clip
                {
                    Anim = layout[i].Anim,
                    StartColumn = layout[i].Start,
                    FrameCount = layout[i].Count,
                    FramesPerSecond = layout[i].Fps,
                    Loop = layout[i].Loop,
                    StartupFrames = layout[i].StartupFrames,
                    ActiveFrames = layout[i].ActiveFrames,
                };
            }

            AssetDatabase.CreateAsset(sheet, sheetPath);
            return sheetPath;
        }

        /// <summary>
        /// Brings an older placeholder sheet up to date with PlaceholderSpriteGenerator.Layout (e.g. the climbing clips):
        /// redraws the PNG if it still has the original placeholder size, then adds any missing clips to the sheet asset.
        /// Sheets with your own art (any other size) are left alone. Returns a short report.
        /// </summary>
        internal static string UpgradeSheet(string characterName, PlaceholderSpriteGenerator.Palette palette)
        {
            string pngPath = $"{SpriteFolder}/{characterName}_Placeholder.png";
            string sheetPath = $"{SheetFolder}/Sheet_{characterName}.asset";
            var sheet = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(sheetPath);
            if (sheet == null) return $"{characterName}: no sheet (run the Milestone 3 setup)";

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            string report;
            if (texture != null && texture.width == PlaceholderSpriteGenerator.LegacyColumns * PlaceholderSpriteGenerator.CellWidth)
            {
                texture = PlaceholderSpriteGenerator.CreateSheetTexture(pngPath, palette, regenerate: true);
                report = $"{characterName}: placeholder redrawn with climbing frames";
            }
            else report = $"{characterName}: sheet already up to date";

            var sheetTexture = sheet.Texture != null ? sheet.Texture : texture;
            int columns = sheetTexture != null ? sheetTexture.width / Mathf.Max(1, sheet.CellSize.x) : 0;
            var clips = new System.Collections.Generic.List<DirectionalSpriteSheet.Clip>(sheet.Clips ?? new DirectionalSpriteSheet.Clip[0]);
            int added = 0;
            foreach (var layout in PlaceholderSpriteGenerator.Layout)
            {
                if (clips.Exists(c => c.Anim == layout.Anim) || layout.Start + layout.Count > columns) continue;
                clips.Add(new DirectionalSpriteSheet.Clip
                {
                    Anim = layout.Anim, StartColumn = layout.Start, FrameCount = layout.Count, FramesPerSecond = layout.Fps,
                    Loop = layout.Loop, StartupFrames = layout.StartupFrames, ActiveFrames = layout.ActiveFrames,
                });
                added++;
            }
            if (added > 0)
            {
                sheet.Clips = clips.ToArray();
                EditorUtility.SetDirty(sheet);
                report += $", {added} clip(s) added";
            }
            return report;
        }

        /// <summary>URP/Lit, alpha-clipped, double-sided (for the shadow quad), matte, with emission for hit flashes.</summary>
        static void CreateSpriteMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            // URP/Lit decides shadow receiving per material (Renderer.receiveShadows isn't enough). Sprites must not
            // receive: each character's shadow quad would shade its own body. DirectionalSpriteRenderer shades instead.
            material.SetFloat("_ReceiveShadows", 0f);
            material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            // Near-black (not black) keeps URP from stripping _EMISSION; the flash sets the real colour per character.
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.001f, 0.001f, 0.001f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.renderQueue = (int)RenderQueue.AlphaTest;
            // Prefer the sprite shader that receives real shadows (falls back to the URP/Lit set-up above if it's missing).
            SpriteShadowSetup.Convert(material, SpriteShadowSetup.CharacterOffset);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        internal static void AddVisual(GameObject character, DirectionalSpriteSheet sheet, Material material)
        {
            var existing = character.transform.Find("Visual");
            if (existing != null)
            {
                // Re-run: fill in anything missing, keep the rest.
                if (existing.TryGetComponent(out DirectionalSpriteRenderer existingRenderer))
                {
                    var serialized = new SerializedObject(existingRenderer);
                    var sheetProperty = serialized.FindProperty("sheet");
                    if (sheetProperty.objectReferenceValue == null)
                    {
                        sheetProperty.objectReferenceValue = sheet;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                return;
            }

            // Remove the greybox capsule visuals (the CharacterController stays).
            if (character.TryGetComponent(out MeshRenderer capsuleRenderer)) Object.DestroyImmediate(capsuleRenderer);
            if (character.TryGetComponent(out MeshFilter capsuleFilter)) Object.DestroyImmediate(capsuleFilter);
            var indicator = character.transform.Find("FacingIndicator");
            if (indicator != null) Object.DestroyImmediate(indicator.gameObject);

            float feetY = -1f;
            if (character.TryGetComponent(out CharacterController controller))
                feetY = controller.center.y - controller.height * 0.5f;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(character.transform, false);
            visual.transform.localPosition = new Vector3(0f, feetY, 0f);

            bool perPixelShadows = material != null && material.shader.name == SpriteShadowSetup.ShaderName;
            var body = CreateQuad("Body", visual.transform, material, ShadowCastingMode.Off, receiveShadows: perPixelShadows);
            var shadow = CreateQuad("Shadow", visual.transform, material, ShadowCastingMode.ShadowsOnly, receiveShadows: false);

            var renderer = visual.AddComponent<DirectionalSpriteRenderer>();
            var so = new SerializedObject(renderer);
            so.FindProperty("sheet").objectReferenceValue = sheet;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = character.layer;
        }

        static MeshRenderer CreateQuad(string name, Transform parent, Material material, ShadowCastingMode shadows, bool receiveShadows)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
