using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Milestone 45: the pen's feed trough. Filling it (once a day) feeds every animal for the day: one Animal Feed
    /// (Oswin sells it), or failing that two turnips from your field. It empties every morning; with the Hay Loft
    /// (Milestone 49) one fill lasts three days.
    /// </summary>
    public sealed class FeedTrough : Interactable, ISaveable
    {
        [SerializeField] ItemData feed;
        [SerializeField, Tooltip("Used when you have no feed: this many of it fill the trough.")] ItemData fallbackFeed;
        [SerializeField] int fallbackCount = 2;
        [SerializeField] float range = 2f;
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "animal.trough";
        [SerializeField, Tooltip("Shown when full (the feed inside).")] GameObject fillVisual;

        int daysLeft; // days the current fill still feeds them (today included)

        [Serializable]
        sealed class State { public bool filled; public int daysLeft; }

        public string SaveId => saveId;
        public bool Filled => daysLeft > 0;
        public int DaysLeft => daysLeft;
        bool filled => daysLeft > 0;

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus<DayPassedEvent>.Subscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus<DayPassedEvent>.Unsubscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start() => Refresh();

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;
            prompt.Distance = distance;
            var inventory = interactor.Inventory;
            if (filled)
            {
                prompt.Text = daysLeft > 1 ? $"The trough is full: the animals are fed for {daysLeft} days" : "The trough is full: the animals are fed today";
            }
            else if (feed != null && inventory != null && inventory.CountOf(feed) > 0)
            {
                prompt.Text = $"Fill the trough ({feed.DisplayName})";
                prompt.CanInteract = true;
            }
            else if (fallbackFeed != null && inventory != null && inventory.CountOf(fallbackFeed) >= fallbackCount)
            {
                prompt.Text = $"Fill the trough ({fallbackCount} {fallbackFeed.DisplayName}s)";
                prompt.CanInteract = true;
            }
            else
            {
                prompt.Text = $"The trough is empty: needs {(feed != null ? feed.DisplayName : "feed")} (Oswin sells it)";
            }
            return true;
        }

        public override void Interact(PlayerInteractor interactor) => Fill(interactor.Inventory);

        /// <summary>Uses feed from the bag to feed every animal today. False if it's already full or there's no feed.</summary>
        public bool Fill(Inventory inventory)
        {
            if (filled || inventory == null) return false;
            bool paid = feed != null && inventory.Remove(feed, 1);
            if (!paid && fallbackFeed != null && inventory.CountOf(fallbackFeed) >= fallbackCount) paid = inventory.Remove(fallbackFeed, fallbackCount);
            if (!paid) return false;
            daysLeft = Services.TryGet(out HomesteadUpgrades upgrades) && upgrades.Has(HomesteadUpgradeKind.HayLoft) ? 3 : 1;
            foreach (var animal in FarmAnimal.All) animal.Feed();
            var audio = GameAudio.Instance;
            if (audio != null && audio.Library != null) audio.Play(audio.Library.Plant, transform.position);
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent("The animals crowd round the trough."));
            Refresh();
            return true;
        }

        void OnDayPassed(DayPassedEvent evt)
        {
            if (daysLeft > 0) daysLeft--;
            Refresh();
        }

        // A fill that lasts several days feeds them again each morning (after their own day has turned over).
        void Update()
        {
            if (daysLeft <= 0) return;
            foreach (var animal in FarmAnimal.All) if (!animal.Fed) animal.Feed();
        }

        void Refresh()
        {
            if (fillVisual != null) fillVisual.SetActive(filled);
        }

        public string CaptureState() => JsonUtility.ToJson(new State { filled = filled, daysLeft = daysLeft });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            daysLeft = state == null ? 0 : state.daysLeft > 0 ? state.daysLeft : state.filled ? 1 : 0;
            // Animals save their own "fed" flag, so nothing else to restore here.
            Refresh();
        }
    }
}
