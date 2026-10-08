using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 44 setup (durability &amp; repair): gives weapons and armour a Durability (uses before they wear out;
    /// accessories never wear), adds Gear Condition to the player, and recompiles the Ink story (Brenna mends gear).
    /// Safe to re-run: a Durability you've already set is kept.
    /// </summary>
    public static class DurabilitySetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";

        /// <summary>Blows landed (weapons) or hits taken (armour) before each wears out. Iron lasts about twice as long.</summary>
        static readonly Dictionary<string, int> Durabilities = new()
        {
            { "Weapon_RustedSwordShield", 120 },
            { "Weapon_RustedGreatsword", 110 },
            { "Weapon_IronSwordShield", 260 },
            { "Weapon_IronGreatsword", 240 },
            { "Armor_PaddedCap", 120 },
            { "Armor_Gambeson", 140 },
            { "Armor_LeatherLeggings", 140 },
            { "Armor_FurLeggings", 180 },
            { "Armor_IronHelm", 260 },
            { "Armor_ChainHauberk", 300 },
        };

        [MenuItem("Beast/Setup/Run Milestone 44 Setup (Durability & Repair)", priority = 36)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }

            int set = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EquipmentData", new[] { Root + "/Data/Items" }))
            {
                var item = AssetDatabase.LoadAssetAtPath<EquipmentData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.Durability > 0 || item.Slot == EquipSlot.Accessory) continue;
                item.Durability = Durabilities.TryGetValue(item.name, out int uses) ? uses : (item.Slot == EquipSlot.Weapon ? 150 : 160);
                EditorUtility.SetDirty(item);
                set++;
            }
            AssetDatabase.SaveAssets();
            if (!DialogueQuestSetup.CompileInk()) return;

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }
            bool added = !player.TryGetComponent(out GearCondition _);
            if (added) player.AddComponent<GearCondition>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 44 durability setup complete: durability set on {set} item(s); Gear Condition {(added ? "added to" : "already on")} the player. " +
                      "Weapons wear as they land blows, armour as you take hits. Brenna mends gear for gold; the workbench mends it with Iron Scrap (Smithing).");
        }
    }
}
