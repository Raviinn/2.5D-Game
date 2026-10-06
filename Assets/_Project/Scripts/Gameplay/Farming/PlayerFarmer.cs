using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>Tracks which seed the player plants. Cycle Seed (V / D-pad ←) switches between seed types in the bag.</summary>
    [RequireComponent(typeof(Inventory))]
    public sealed class PlayerFarmer : MonoBehaviour
    {
        Inventory inventory;
        InputAction cycleAction;
        ItemData selectedSeed;

        /// <summary>The seed that will be planted next, or null if the bag has no plantable seeds.</summary>
        public ItemData SelectedSeed
        {
            get
            {
                if (selectedSeed == null || inventory.CountOf(selectedSeed) == 0) selectedSeed = NextSeedAfter(null);
                return selectedSeed;
            }
        }

        /// <summary>True when more than one kind of seed is available (worth showing the switch hint).</summary>
        public bool HasSeedChoice => SelectedSeed != null && NextSeedAfter(SelectedSeed) != SelectedSeed;

        void Awake() => inventory = GetComponent<Inventory>();

        void Start() => cycleAction = Services.Get<InputService>().CycleSeed;

        void Update()
        {
            if (!cycleAction.WasPressedThisFrame() || SelectedSeed == null) return;
            selectedSeed = NextSeedAfter(selectedSeed);
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"Seeds: {selectedSeed.DisplayName}"));
        }

        /// <summary>Next distinct plantable seed in bag order after 'current' (wraps). Null current = first.</summary>
        ItemData NextSeedAfter(ItemData current)
        {
            ItemData first = null;
            bool passedCurrent = current == null;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var stack = inventory[i];
                if (stack.IsEmpty || !CropCatalog.IsPlantable(stack.Item)) continue;

                first ??= stack.Item;
                if (passedCurrent && stack.Item != current) return stack.Item;
                if (stack.Item == current) passedCurrent = true;
            }
            return first;
        }
    }
}
