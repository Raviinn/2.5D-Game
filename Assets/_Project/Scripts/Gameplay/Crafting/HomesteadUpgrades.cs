using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum HomesteadUpgradeKind
    {
        /// <summary>A second, smaller field (the kitchen garden).</summary>
        KitchenGarden,
        /// <summary>The storage chest holds more.</summary>
        LargerChest,
        /// <summary>Alchemy and cooking make one extra serving.</summary>
        CopperStill,
        /// <summary>One fill of the trough feeds the animals for three days.</summary>
        HayLoft,
    }

    /// <summary>
    /// Milestone 49: the homestead plans (a signpost by the bed). F opens the plans: spend gold and materials (from the
    /// bag, then the chest) on lasting improvements. Each is built once and saved; effects come back on load.
    /// Other systems ask Has(kind): Crafting (Copper Still), FeedTrough (Hay Loft).
    /// </summary>
    public sealed class HomesteadUpgrades : Interactable, ISaveable
    {
        [Serializable]
        public struct Cost
        {
            public ItemData Item;
            [Min(1)] public int Count;
        }

        [Serializable]
        public struct Upgrade
        {
            public HomesteadUpgradeKind Kind;
            public string Title;
            [TextArea] public string Description;
            [Min(0)] public int Gold;
            public Cost[] Materials;
        }

        [SerializeField] Upgrade[] upgrades;
        [SerializeField, Tooltip("Shown once the Kitchen Garden is built (a second field, inactive until then).")] GameObject kitchenGarden;
        [SerializeField] StorageChest chest;
        [SerializeField, Min(1)] int largerChestSlots = 100;
        [SerializeField] float range = 2f;
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "homestead.upgrades";

        readonly HashSet<HomesteadUpgradeKind> built = new();

        [Serializable]
        sealed class State { public string[] built; }

        public string SaveId => saveId;
        public IReadOnlyList<Upgrade> All => upgrades;
        public bool Has(HomesteadUpgradeKind kind) => built.Contains(kind);
        public Inventory Storage => chest != null ? chest.Contents : null;

        /// <summary>Raised after something is built (or restored from a save).</summary>
        public event Action Changed;

        void Awake() => Services.Register(this);

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void OnDestroy() => Services.Unregister(this);

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;
            prompt.Text = "Look over the homestead plans";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor) => Open();

        public void Open()
        {
            Services.Get<GameStateService>().SetState(GameState.InGameMenu);
            EventBus<HomesteadPlansOpenedEvent>.Raise(new HomesteadPlansOpenedEvent(this));
        }

        /// <summary>Why it can't be built now (null if it can).</summary>
        public string Problem(Upgrade upgrade, Inventory bag)
        {
            if (Has(upgrade.Kind)) return "Already built";
            if (bag == null) return "No bag";
            if (bag.Gold < upgrade.Gold) return $"Needs {upgrade.Gold} gold";
            if (upgrade.Materials != null)
                foreach (var cost in upgrade.Materials)
                    if (cost.Item != null && Available(bag, cost.Item) < cost.Count) return $"Needs {cost.Count} {cost.Item.DisplayName}";
            return null;
        }

        public int Available(Inventory bag, ItemData item) => (bag != null ? bag.CountOf(item) : 0) + (Storage != null ? Storage.CountOf(item) : 0);

        /// <summary>Pays and builds. False (nothing spent) if it can't be built.</summary>
        public bool Build(HomesteadUpgradeKind kind, Inventory bag)
        {
            int index = Array.FindIndex(upgrades ?? new Upgrade[0], u => u.Kind == kind);
            if (index < 0) return false;
            var upgrade = upgrades[index];
            if (Problem(upgrade, bag) != null) return false;
            if (!bag.TrySpendGold(upgrade.Gold)) return false;
            if (upgrade.Materials != null)
                foreach (var cost in upgrade.Materials)
                {
                    if (cost.Item == null) continue;
                    int fromBag = Mathf.Min(bag.CountOf(cost.Item), cost.Count);
                    if (fromBag > 0) bag.Remove(cost.Item, fromBag);
                    if (cost.Count - fromBag > 0) Storage.Remove(cost.Item, cost.Count - fromBag);
                }
            built.Add(kind);
            Apply(kind);
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"Built: {upgrade.Title}"));
            Changed?.Invoke();
            return true;
        }

        void Apply(HomesteadUpgradeKind kind)
        {
            switch (kind)
            {
                case HomesteadUpgradeKind.KitchenGarden:
                    if (kitchenGarden != null) kitchenGarden.SetActive(true);
                    break;
                case HomesteadUpgradeKind.LargerChest:
                    if (Storage != null) Storage.Grow(largerChestSlots);
                    break;
            }
        }

        public string CaptureState()
        {
            var names = new List<string>();
            foreach (var kind in built) names.Add(kind.ToString());
            return JsonUtility.ToJson(new State { built = names.ToArray() });
        }

        public void RestoreState(string json)
        {
            built.Clear();
            var state = JsonUtility.FromJson<State>(json);
            if (state?.built != null)
                foreach (var name in state.built)
                    if (Enum.TryParse(name, out HomesteadUpgradeKind kind)) built.Add(kind);
            // Loading an older save (or one from before a build) puts the garden away again.
            if (kitchenGarden != null) kitchenGarden.SetActive(built.Contains(HomesteadUpgradeKind.KitchenGarden));
            foreach (var kind in built) Apply(kind);
            Changed?.Invoke();
        }
    }

    public readonly struct HomesteadPlansOpenedEvent : IEvent
    {
        public readonly HomesteadUpgrades Plans;
        public HomesteadPlansOpenedEvent(HomesteadUpgrades plans) { Plans = plans; }
    }
}
