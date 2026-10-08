using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 25 setup (archers &amp; jump links): the Bandit Archer enemy (data, placeholder sprites with a bow)
    /// and one archer at the bandit camp. Jump links need no setup: the navigation mesh finds them when the scene
    /// loads. Safe to re-run: existing assets and the archer are kept (the archer's data and sprites are re-linked).
    /// </summary>
    public static class ArcherSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string BanditDataPath = Root + "/Data/Combat/Enemies/Enemy_Bandit.asset";
        const string ArcherDataPath = Root + "/Data/Combat/Enemies/Enemy_BanditArcher.asset";
        const string ArcherName = "Bandit_Archer";
        static readonly Vector3 ArcherPosition = new(24f, 1.1f, 19f);

        internal static readonly PlaceholderSpriteGenerator.Palette ArcherPalette = new()
        {
            Body = new Color32(70, 92, 52, 255), Trim = new Color32(92, 64, 40, 255), Skin = new Color32(212, 160, 120, 255),
            Hair = new Color32(52, 36, 30, 255), Weapon = new Color32(225, 215, 190, 255), Outline = new Color32(24, 16, 14, 255),
            HasWeapon = true, Bow = true,
        };

        [MenuItem("Beast/Setup/Run Milestone 25 Setup (Archers & Jump Links)", priority = 21)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }

            string sheetPath = SpriteVisualsSetup.CreateSheet("BanditArcher", ArcherPalette);
            var data = EnsureArcherData(out bool createdData);
            if (data == null) return;
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            data = AssetDatabase.LoadAssetAtPath<EnemyData>(ArcherDataPath);
            var sheet = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(sheetPath);

            var archer = GameObject.Find(ArcherName);
            bool createdArcher = false;
            if (archer == null)
            {
                var template = GameObject.Find("Bandit_A");
                if (template == null)
                {
                    Debug.LogError("[Setup] Bandit_A not found in World_Test. Run the Milestone 2 setup first.");
                    return;
                }
                archer = Object.Instantiate(template, template.transform.parent);
                archer.name = ArcherName;
                archer.transform.SetPositionAndRotation(ArcherPosition, Quaternion.Euler(0f, 225f, 0f));
                createdArcher = true;
            }
            SetReference(archer.GetComponent<EnemyController>(), "data", data);
            var visual = archer.GetComponentInChildren<DirectionalSpriteRenderer>();
            if (visual != null) SetReference(visual, "sheet", sheet);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 25 setup complete: Bandit Archer data {(createdData ? "created" : "kept")}, " +
                      $"{ArcherName} {(createdArcher ? "added to" : "already at")} the bandit camp. Enemies now vault up low edges " +
                      $"(up to {WorldNavigation.MaxVault} m) and jump down drops (up to {WorldNavigation.MaxDrop} m) to reach you.");
        }

        /// <summary>A copy of the Bandit's data (loot, night settings) tuned as an archer. Kept if it already exists.</summary>
        static EnemyData EnsureArcherData(out bool created)
        {
            created = false;
            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(ArcherDataPath);
            if (existing != null) return existing;

            var bandit = AssetDatabase.LoadAssetAtPath<EnemyData>(BanditDataPath);
            if (bandit == null)
            {
                Debug.LogError("[Setup] Enemy_Bandit not found. Run the Milestone 2 setup first.");
                return null;
            }
            var data = ScriptableObject.CreateInstance<EnemyData>();
            EditorUtility.CopySerialized(bandit, data);
            data.name = "Enemy_BanditArcher";
            var so = new SerializedObject(data);
            so.FindProperty("id").stringValue = ""; // a fresh ID from the database rebuild
            so.ApplyModifiedPropertiesWithoutUndo();

            data.DisplayName = "Bandit Archer";
            data.MaxHealth = 45f;
            data.MaxPoise = 20f;
            data.MoveSpeed = 3.2f;
            data.AggroRange = 16f;
            data.Ranged = true;
            data.ShootRange = 16f;
            data.PreferredDistance = 9f;
            data.DrawTime = 0.9f;
            data.ShotCooldown = 2.6f;
            data.ArrowSpeed = 22f;
            data.ArrowDamage = 14f;
            data.ArrowPoiseDamage = 12f;
            data.GiveUpAfter = 6f;
            data.LeashRange = 24f;
            data.XpReward = Mathf.RoundToInt(bandit.XpReward * 1.2f);
            AssetDatabase.CreateAsset(data, ArcherDataPath);
            created = true;
            return data;
        }

        static void SetReference(Object target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
