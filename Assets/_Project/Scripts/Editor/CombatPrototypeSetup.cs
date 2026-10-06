using System;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 2 setup: Player/Enemy layers, Knight class with two styles, enemy data,
    /// and combat components + enemies in World_Test. Safe to re-run: existing assets keep your tuning.
    /// </summary>
    public static class CombatPrototypeSetup
    {
        const string Root = "Assets/_Project";
        const string DataRoot = Root + "/Data/Combat";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";

        [MenuItem("Beast/Setup/Run Milestone 2 Setup (Combat)", priority = 1)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test scene not found. Run Milestone 1 Setup first.");
                return;
            }

            int playerLayer = EnsureLayer(CombatLayers.PlayerLayer);
            int enemyLayer = EnsureLayer(CombatLayers.EnemyLayer);
            if (playerLayer < 0 || enemyLayer < 0) return;

            foreach (var folder in new[] { "Attacks", "Movesets", "Classes", "Enemies" })
                EnsureFolder($"{DataRoot}/{folder}");

            string knightPath = AssetDatabase.GetAssetPath(CreateKnight());
            string banditPath = AssetDatabase.GetAssetPath(CreateBandit());
            string dummyPath = AssetDatabase.GetAssetPath(CreateDummy());

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);

            // Opening a scene unloads assets, invalidating references held from before it.
            // Reload them from disk so the scene gets valid references.
            var knight = AssetDatabase.LoadAssetAtPath<ClassData>(knightPath);
            var bandit = AssetDatabase.LoadAssetAtPath<EnemyData>(banditPath);
            var dummy = AssetDatabase.LoadAssetAtPath<EnemyData>(dummyPath);
            SetUpPlayer(knight, playerLayer);
            SetUpCamera(playerLayer, enemyLayer);
            SpawnEnemies(bandit, dummy, enemyLayer);
            if (Object.FindFirstObjectByType<CombatDebugHUD>() == null)
                new GameObject("CombatHUD").AddComponent<CombatDebugHUD>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Milestone 2 combat setup complete. Press Play. Tip: turn on Gizmos in the Game view to see hitboxes.");
        }

        // ---------- Data ----------

        static ClassData CreateKnight()
        {
            var swordShield = Asset<MovesetData>("Movesets/Knight_SwordShield", m =>
            {
                m.DisplayName = "Sword & Shield";
                m.LightCombo = new[]
                {
                    Attack("Knight_SS_Light1", 12, 10, 2.5f, 0.10f, 0.08f, 0.22f, new Vector3(1.8f, 1.2f, 1.4f), 0.6f, 0.05f, 0.10f),
                    Attack("Knight_SS_Light2", 12, 10, 2.5f, 0.10f, 0.08f, 0.24f, new Vector3(1.8f, 1.2f, 1.4f), 0.6f, 0.05f, 0.10f),
                    Attack("Knight_SS_Light3", 18, 20, 5.0f, 0.14f, 0.10f, 0.30f, new Vector3(2.2f, 1.2f, 1.6f), 1.0f, 0.08f, 0.20f),
                    Attack("Knight_SS_Light4", 26, 35, 7.0f, 0.20f, 0.12f, 0.40f, new Vector3(2.6f, 1.4f, 2.0f), 1.4f, 0.10f, 0.30f),
                };
                m.Heavy = Attack("Knight_SS_ShieldBash", 20, 45, 6.0f, 0.30f, 0.12f, 0.40f, new Vector3(1.6f, 1.4f, 1.6f), 1.5f, 0.10f, 0.30f, dodgeCancelAfter: 0.3f);
            });

            var greatsword = Asset<MovesetData>("Movesets/Knight_Greatsword", m =>
            {
                m.DisplayName = "Greatsword";
                m.LightCombo = new[]
                {
                    Attack("Knight_GS_Light1", 22, 20, 4f, 0.18f, 0.12f, 0.32f, new Vector3(3.0f, 1.4f, 2.2f), 0.8f, 0.07f, 0.18f, centerZ: 1.5f),
                    Attack("Knight_GS_Light2", 24, 22, 4f, 0.18f, 0.12f, 0.34f, new Vector3(3.0f, 1.4f, 2.2f), 0.8f, 0.07f, 0.18f, centerZ: 1.5f),
                    Attack("Knight_GS_Light3", 40, 50, 8f, 0.30f, 0.16f, 0.50f, new Vector3(3.4f, 1.6f, 2.6f), 1.6f, 0.12f, 0.35f, centerZ: 1.5f),
                };
                m.Heavy = Attack("Knight_GS_Overhead", 45, 70, 9f, 0.45f, 0.16f, 0.55f, new Vector3(2.4f, 2.0f, 3.2f), 1.0f, 0.14f, 0.45f, centerZ: 1.7f, dodgeCancelAfter: 0.4f);
                m.DodgeDistance = 4f;
                m.DodgeDuration = 0.36f;
                m.IFrameStart = 0.03f;
                m.IFrameEnd = 0.26f;
                m.DodgeStaminaCost = 25f;
                m.BlockDamageReduction = 0.7f;
                m.ParryWindow = 0.12f;
                m.BlockMoveSpeedMultiplier = 0.35f;
                m.BlockStaminaPerDamage = 1.5f;
                m.ParryStaggerDuration = 1.8f;
                m.GuardBreakStaggerDuration = 1.2f;
            });

            return Asset<ClassData>("Classes/Class_Knight", c =>
            {
                c.DisplayName = "Knight";
                c.Description = "Heavy infantry. Unbreakable wall with sword & shield, or a devastating greatsword.";
                c.MaxHealth = 150f;
                c.MaxPoise = 60f;
                c.MaxStamina = 100f;
                c.StaminaRegen = 35f;
                c.StaminaRegenDelay = 0.7f;
                c.Movesets = new[] { swordShield, greatsword };
            });
        }

        static EnemyData CreateBandit() => Asset<EnemyData>("Enemies/Enemy_Bandit", e =>
        {
            e.DisplayName = "Bandit";
            e.MaxHealth = 70f;
            e.MaxPoise = 30f;
            e.MoveSpeed = 3.4f;
            e.AggroRange = 12f;
            e.AttackRange = 2f;
            e.AttackCooldown = 1.1f;
            e.RespawnDelay = 6f;
            e.Attacks = new[]
            {
                // Long, readable wind-ups: the orange flash is the cue to dodge or parry.
                Attack("Bandit_Slash", 12, 25, 4f, 0.45f, 0.12f, 0.45f, new Vector3(1.6f, 1.2f, 1.4f), 0.8f, 0f, 0f, centerZ: 1.0f),
                Attack("Bandit_Overhead", 20, 40, 6f, 0.75f, 0.12f, 0.60f, new Vector3(1.2f, 1.2f, 1.8f), 1.2f, 0f, 0f, centerZ: 1.1f),
            };
        });

        static EnemyData CreateDummy() => Asset<EnemyData>("Enemies/Enemy_TrainingDummy", e =>
        {
            e.DisplayName = "Training Dummy";
            e.IsTrainingDummy = true;
            e.MaxHealth = 200f;
            e.MaxPoise = 50f;
            e.RespawnDelay = 3f;
            e.Attacks = Array.Empty<AttackData>();
        });

        static AttackData Attack(string name, float damage, float poise, float knockback,
            float startup, float active, float recovery, Vector3 hitboxSize, float lunge, float hitStop, float shake,
            float centerZ = 1.1f, float dodgeCancelAfter = 0f)
        {
            return Asset<AttackData>($"Attacks/{name}", a =>
            {
                a.Damage = damage;
                a.PoiseDamage = poise;
                a.Knockback = knockback;
                a.Startup = startup;
                a.Active = active;
                a.Recovery = recovery;
                a.DodgeCancelAfter = dodgeCancelAfter;
                a.HitboxCenter = new Vector3(0f, 0f, centerZ);
                a.HitboxSize = hitboxSize;
                a.Lunge = lunge;
                a.HitStop = hitStop;
                a.CameraShake = shake;
            });
        }

        /// <summary>Loads the asset, or creates it and applies the defaults. Existing assets keep your tuning.</summary>
        static T Asset<T>(string relativePath, Action<T> configureNew) where T : ScriptableObject
        {
            string path = $"{DataRoot}/{relativePath}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            configureNew(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------- Scene ----------

        static void SetUpPlayer(ClassData knight, int playerLayer)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }

            SetLayerRecursively(player, playerLayer);
            var combatant = GetOrAdd<Combatant>(player);
            SetEnum(combatant, "team", (int)Team.Player);
            GetOrAdd<Stamina>(player);
            GetOrAdd<LockOnController>(player);
            var combat = GetOrAdd<PlayerCombat>(player);
            SetReference(combat, "classData", knight);
            GetOrAdd<CombatantFlash>(player);
        }

        static void SetUpCamera(int playerLayer, int enemyLayer)
        {
            var orbit = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (orbit == null) return;

            var serialized = new SerializedObject(orbit);
            serialized.FindProperty("collisionMask").intValue = ~((1 << playerLayer) | (1 << enemyLayer));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SpawnEnemies(EnemyData bandit, EnemyData dummy, int enemyLayer)
        {
            if (GameObject.Find("Enemies") != null)
            {
                RepairEnemyData(bandit, dummy);
                return;
            }

            var parent = new GameObject("Enemies").transform;
            var banditMat = LoadOrCreateMaterial("Greybox_Enemy", new Color(0.75f, 0.2f, 0.2f));
            var dummyMat = LoadOrCreateMaterial("Greybox_Dummy", new Color(0.8f, 0.7f, 0.35f));

            CreateEnemy("TrainingDummy", dummy, new Vector3(-3f, 1.1f, 4f), 180f, dummyMat, parent, enemyLayer);
            CreateEnemy("Bandit_A", bandit, new Vector3(15f, 1.1f, 16f), 225f, banditMat, parent, enemyLayer);
            CreateEnemy("Bandit_B", bandit, new Vector3(19f, 1.1f, 12f), 225f, banditMat, parent, enemyLayer);
        }

        /// <summary>Fills in EnemyData on existing enemies that lost it; leaves assigned ones alone.</summary>
        static void RepairEnemyData(EnemyData bandit, EnemyData dummy)
        {
            int repaired = 0;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(enemy);
                var dataProperty = serialized.FindProperty("data");
                if (dataProperty.objectReferenceValue != null) continue;

                dataProperty.objectReferenceValue = enemy.name.Contains("Dummy") ? dummy : bandit;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                repaired++;
            }
            Debug.Log($"[Setup] Enemies already exist in World_Test; repaired {repaired} missing EnemyData reference(s).");
        }

        static void CreateEnemy(string name, EnemyData data, Vector3 position, float yaw, Material material, Transform parent, int layer)
        {
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = name;
            enemy.transform.SetParent(parent, false);
            enemy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Object.DestroyImmediate(enemy.GetComponent<CapsuleCollider>());
            enemy.GetComponent<MeshRenderer>().sharedMaterial = material;

            var controller = enemy.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = Vector3.zero;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingIndicator";
            Object.DestroyImmediate(nose.GetComponent<BoxCollider>());
            nose.transform.SetParent(enemy.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);
            nose.transform.localScale = new Vector3(0.2f, 0.2f, 0.4f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = material;

            var combatant = enemy.AddComponent<Combatant>();
            SetEnum(combatant, "team", (int)Team.Enemy);
            var ai = enemy.AddComponent<EnemyController>();
            SetReference(ai, "data", data);
            enemy.AddComponent<CombatantFlash>();

            SetLayerRecursively(enemy, layer);
        }

        // ---------- Helpers ----------

        internal static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++) // 0-7 are reserved by Unity
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[Setup] Added layer '{layerName}' at index {i}.");
                return i;
            }

            Debug.LogError($"[Setup] No free layer slot for '{layerName}'.");
            return -1;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = $"{Root}/Art/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();

        static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetEnum(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
