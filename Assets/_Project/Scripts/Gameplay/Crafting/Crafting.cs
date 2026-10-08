using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public readonly struct ItemCraftedEvent : IEvent
    {
        public readonly RecipeData Recipe;
        public ItemCraftedEvent(RecipeData recipe) { Recipe = recipe; }
    }

    public readonly struct WorkbenchOpenedEvent : IEvent
    {
        public readonly Workbench Bench;
        public WorkbenchOpenedEvent(Workbench bench) { Bench = bench; }
    }

    /// <summary>
    /// Crafting rules, shared by the workbench screen and tests. Ingredients come from the bag first, then from
    /// storage (the homestead chest, when the bench has one), then from equipped gear; gear made from equipped gear of
    /// the same slot replaces it in place (an upgrade stays equipped). Results go to the bag.
    /// </summary>
    public static class Crafting
    {
        /// <summary>How many of 'item' you could use: in the bag, in storage, plus worn or wielded.</summary>
        public static int Available(Inventory inventory, PlayerEquipment equipment, ItemData item, Inventory storage = null)
        {
            int count = inventory.CountOf(item);
            if (storage != null) count += storage.CountOf(item);
            if (equipment != null && item is EquipmentData gear) count += equipment.EquippedCount(gear);
            return count;
        }

        /// <summary>Null if it can be crafted now, otherwise why not.</summary>
        public static string Problem(RecipeData recipe, Inventory inventory, PlayerEquipment equipment, Inventory storage = null)
        {
            if (recipe == null || recipe.Output == null) return "Unknown recipe";
            foreach (var input in recipe.Inputs)
                if (input.Item != null && Available(inventory, equipment, input.Item, storage) < input.Count) return $"Need {input.Count}× {input.Item.DisplayName}";
            if (UpgradeSource(recipe, inventory, equipment, storage) == null && inventory.SpaceFor(recipe.Output) < recipe.OutputCount) return "Bag is full";
            return null;
        }

        /// <summary>Makes the recipe once. False (and nothing used up) if it can't be made.</summary>
        public static bool Craft(RecipeData recipe, Inventory inventory, PlayerEquipment equipment, Inventory storage = null)
        {
            if (Problem(recipe, inventory, equipment, storage) != null) return false;
            var upgrade = UpgradeSource(recipe, inventory, equipment, storage);

            foreach (var input in recipe.Inputs)
            {
                if (input.Item == null) continue;
                int owed = input.Count;
                owed -= Take(inventory, input.Item, owed);
                if (storage != null) owed -= Take(storage, input.Item, owed);
                // Gear still owed comes off your body; the piece being upgraded is swapped in place below instead.
                for (int i = 0; i < owed; i++)
                    if (input.Item != upgrade) equipment.RemoveEquipped((EquipmentData)input.Item);
            }

            if (upgrade != null) equipment.ReplaceEquipped(upgrade, (EquipmentData)recipe.Output);
            else inventory.Add(recipe.Output, recipe.OutputCount + ExtraServings(recipe));

            EventBus<ItemCraftedEvent>.Raise(new ItemCraftedEvent(recipe));
            return true;
        }

        /// <summary>The Copper Still (Milestone 49): alchemy and cooking make one more.</summary>
        public static int ExtraServings(RecipeData recipe) =>
            recipe != null && recipe.Kind is CraftKind.Alchemy or CraftKind.Cooking &&
            Services.TryGet(out HomesteadUpgrades upgrades) && upgrades.Has(HomesteadUpgradeKind.CopperStill) ? 1 : 0;

        static int Take(Inventory from, ItemData item, int wanted)
        {
            int taken = Mathf.Min(wanted, from.CountOf(item));
            if (taken > 0) from.Remove(item, taken);
            return taken;
        }

        /// <summary>
        /// The equipped piece this recipe upgrades in place: an input that's gear for the same slot as the output,
        /// that isn't in the bag or storage (so it has to come off your body). Null otherwise.
        /// </summary>
        static EquipmentData UpgradeSource(RecipeData recipe, Inventory inventory, PlayerEquipment equipment, Inventory storage)
        {
            if (equipment == null || recipe.Output is not EquipmentData result) return null;
            foreach (var input in recipe.Inputs)
                if (input.Item is EquipmentData gear && gear.Slot == result.Slot &&
                    inventory.CountOf(gear) + (storage != null ? storage.CountOf(gear) : 0) < input.Count &&
                    equipment.EquippedCount(gear) > 0)
                    return gear;
            return null;
        }
    }
}
