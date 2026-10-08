using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Milestone 47: something wild to gather (garlic in spring, berries in summer, mushrooms in autumn,
    /// winterberries in winter). F gathers it; it grows back a few days later. Its little cluster is tinted like
    /// the season's find and hidden while it regrows. Saved per spot.
    /// </summary>
    public sealed class ForageSpot : Interactable, ISaveable
    {
        [Serializable]
        public struct Find
        {
            public Season Season;
            public ItemData Item;
            [Min(1)] public int Min;
            [Min(1)] public int Max;
        }

        [SerializeField] Find[] finds;
        [SerializeField, Min(1)] int regrowDays = 3;
        [SerializeField] float range = 1.7f;
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "forage.spot";
        [SerializeField, Tooltip("The cluster shown while there's something to gather.")] Renderer[] parts;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        int daysUntilRegrown;
        MaterialPropertyBlock block;

        [Serializable]
        sealed class State { public int days; }

        public string SaveId => saveId;
        public bool Ready => daysUntilRegrown <= 0 && TodaysFind(out _);

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

        /// <summary>What grows here this season (false if nothing does).</summary>
        public bool TodaysFind(out Find find)
        {
            var season = Calendar.Current;
            if (finds != null)
                foreach (var f in finds)
                    if (f.Season == season && f.Item != null)
                    {
                        find = f;
                        return true;
                    }
            find = default;
            return false;
        }

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Ready) return false; // nothing to see while it regrows
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;
            TodaysFind(out var find);
            prompt.Text = $"Gather {find.Item.DisplayName}";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor) => Gather(interactor.Inventory);

        public int Gather(Inventory inventory)
        {
            if (!Ready || !TodaysFind(out var find)) return 0;
            int count = UnityEngine.Random.Range(find.Min, Mathf.Max(find.Min, find.Max) + 1);
            int leftover = inventory != null ? inventory.Add(find.Item, count) : count;
            if (leftover > 0) ItemPickup.SpawnItem(transform.position + Vector3.up * 0.3f, find.Item, leftover, 0.4f);
            var progression = inventory != null ? inventory.GetComponent<PlayerProgression>() : null;
            if (progression != null) progression.AddXp(Discipline.Farming, 3 * count);
            var audio = GameAudio.Instance;
            if (audio != null && audio.Library != null) audio.Play(audio.Library.Harvest, transform.position);
            daysUntilRegrown = regrowDays;
            Refresh();
            return count;
        }

        void OnDayPassed(DayPassedEvent evt)
        {
            if (daysUntilRegrown > 0) daysUntilRegrown--;
            Refresh();
        }

        // Seasons change with days; a loaded save can jump seasons, so check now and then.
        float nextCheck;
        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            Refresh();
        }

        void Refresh()
        {
            bool show = Ready;
            Color color = TodaysFind(out var find) ? find.Item.PlaceholderColor : Color.white;
            block ??= new MaterialPropertyBlock();
            if (parts == null) return;
            foreach (var part in parts)
            {
                if (part == null) continue;
                part.enabled = show;
                part.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                part.SetPropertyBlock(block);
            }
        }

        public string CaptureState() => JsonUtility.ToJson(new State { days = daysUntilRegrown });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            daysUntilRegrown = state != null ? Mathf.Max(0, state.days) : 0;
            Refresh();
        }
    }
}
