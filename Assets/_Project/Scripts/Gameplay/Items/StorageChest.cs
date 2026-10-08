using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public readonly struct StorageOpenedEvent : IEvent
    {
        public readonly StorageChest Chest;
        public StorageOpenedEvent(StorageChest chest) { Chest = chest; }
    }

    /// <summary>
    /// A storage chest (the homestead's): walk up and press E to move things between your bag and the chest.
    /// Its contents are an Inventory on the same object (saved under its own ID). Moving items between the two is
    /// done here, not in the UI.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public sealed class StorageChest : Interactable
    {
        [SerializeField] string displayName = "Storage chest";
        [SerializeField] float range = 2.2f;

        Inventory contents;

        public Inventory Contents => contents ??= GetComponent<Inventory>();
        public string DisplayName => displayName;

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;
            prompt.Text = $"Open {displayName.ToLowerInvariant()}";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor) => Open();

        public void Open()
        {
            Services.Get<GameStateService>().SetState(GameState.InGameMenu);
            EventBus<StorageOpenedEvent>.Raise(new StorageOpenedEvent(this));
        }

        /// <summary>Moves up to 'count' of an item from one inventory to the other. Returns how many moved.</summary>
        public static int Move(Inventory from, Inventory to, ItemData item, int count)
        {
            if (from == null || to == null || item == null) return 0;
            count = Mathf.Min(count, from.CountOf(item), to.SpaceFor(item));
            if (count <= 0) return 0;
            from.Remove(item, count);
            to.AddQuietly(item, count);
            return count;
        }

        /// <summary>Stores every crop, material and seed in the bag (food, gear and quest items stay with you).</summary>
        public int StoreGoods(Inventory bag)
        {
            int moved = 0;
            for (int i = 0; i < bag.SlotCount; i++)
            {
                var stack = bag[i];
                if (stack.IsEmpty || !IsGood(stack.Item)) continue;
                moved += Move(bag, Contents, stack.Item, stack.Count);
            }
            return moved;
        }

        public static bool IsGood(ItemData item) =>
            item.Category is ItemCategory.Crop or ItemCategory.Material or ItemCategory.Seed;
    }
}
