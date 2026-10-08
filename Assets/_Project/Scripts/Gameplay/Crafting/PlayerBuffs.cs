using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Timed stat bonuses from food and potions (ConsumableData.Buff). One per item: eating another of the same
    /// restarts it; different foods stack. Counts down in game time only (not while paused). Not saved: buffs end
    /// when you load a game.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerBuffs : MonoBehaviour, IStatSource
    {
        public struct Active
        {
            public ConsumableData Source;
            public float Remaining;
        }

        readonly List<Active> active = new();
        PlayerStats stats;

        public IReadOnlyList<Active> Buffs => active;

        void Awake() => stats = GetComponent<PlayerStats>();

        void OnEnable() => EventBus<GameLoadedEvent>.Subscribe(OnGameLoaded);
        void OnDisable() => EventBus<GameLoadedEvent>.Unsubscribe(OnGameLoaded);
        void OnGameLoaded(GameLoadedEvent evt) => Clear();

        public void Apply(ConsumableData item)
        {
            if (item == null || !item.HasBuff) return;
            active.RemoveAll(b => b.Source == item);
            active.Add(new Active { Source = item, Remaining = item.BuffSeconds });
            stats.MarkDirty();
        }

        public void Clear()
        {
            if (active.Count == 0) return;
            active.Clear();
            stats.MarkDirty();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || active.Count == 0) return;
            bool expired = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var buff = active[i];
                buff.Remaining -= dt;
                if (buff.Remaining <= 0f)
                {
                    active.RemoveAt(i);
                    expired = true;
                }
                else active[i] = buff;
            }
            if (expired) stats.MarkDirty();
        }

        public void CollectModifiers(List<StatModifier> into)
        {
            foreach (var buff in active) into.Add(buff.Source.Buff);
        }
    }
}
