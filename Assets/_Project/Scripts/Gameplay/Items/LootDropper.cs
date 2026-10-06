using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Rolls a loot table when this combatant dies and scatters pickups around the body.
    /// Enemies killed while emboldened (at night) roll extra times and drop more gold.
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public sealed class LootDropper : MonoBehaviour
    {
        static readonly List<ItemStack> rollBuffer = new();

        [SerializeField, Tooltip("Optional. Defaults to the EnemyData's loot table.")] LootTable lootTable;
        [SerializeField] float scatterRadius = 1.2f;

        Combatant combatant;

        void Awake()
        {
            combatant = GetComponent<Combatant>();
            if (lootTable == null && TryGetComponent(out EnemyController enemy) && enemy.Data != null)
                lootTable = enemy.Data.Loot;
        }

        void OnEnable() => combatant.Died += Drop;
        void OnDisable() => combatant.Died -= Drop;

        void Drop()
        {
            if (lootTable == null) return;

            rollBuffer.Clear();
            lootTable.Roll(rollBuffer, out int gold);
            if (TryGetComponent(out EnemyController enemy) && enemy.Emboldened && enemy.Data != null)
            {
                for (int i = 0; i < enemy.Data.NightExtraLootRolls; i++)
                {
                    lootTable.Roll(rollBuffer, out int extraGold);
                    gold += extraGold;
                }
                gold = Mathf.RoundToInt(gold * (1f + enemy.Data.NightGoldBonus));
            }

            Vector3 origin = transform.position;
            foreach (var stack in rollBuffer)
                ItemPickup.SpawnItem(origin, stack.Item, stack.Count, scatterRadius);
            if (gold > 0) ItemPickup.SpawnGold(origin, gold, scatterRadius);
        }
    }
}
