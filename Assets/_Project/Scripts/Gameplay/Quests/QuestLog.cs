using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum QuestStatus
    {
        Inactive,
        Active,
        /// <summary>All objectives done; waiting to be turned in.</summary>
        Ready,
        Completed,
    }

    /// <summary>
    /// The player's quests: starting, objective progress (kills, harvests, items held, dialogue flags),
    /// turn-in with rewards, and daily cooldowns for repeatable contracts.
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(Combatant))]
    public sealed class QuestLog : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "player.quests";

        /// <summary>Progress for Kill / Harvest / Custom objectives (Collect is counted live from the bag).</summary>
        readonly Dictionary<QuestData, int[]> active = new();
        readonly HashSet<QuestData> completed = new();
        readonly Dictionary<QuestData, int> completedOnDay = new();
        readonly HashSet<QuestData> announcedReady = new();
        readonly List<QuestData> scratch = new();
        readonly List<QuestData> sortedBuffer = new();
        QuestData focused;

        Inventory inventory;
        Combatant self;
        PlayerProgression progression;
        Reputation reputation;

        [Serializable]
        sealed class State
        {
            public string[] activeIds;
            public int[] progressLengths;  // objective count per active quest
            public int[] progressFlat;     // every active quest's objective counts, in order
            public string[] completedIds;
            public int[] completedDays;
            public string focusedId;
        }

        public string SaveId => saveId;

        /// <summary>Active quests in start order (story quests first in the UI).</summary>
        public IEnumerable<QuestData> ActiveQuests => active.Keys;
        public IEnumerable<QuestData> CompletedQuests => completed;

        void Awake()
        {
            inventory = GetComponent<Inventory>();
            self = GetComponent<Combatant>();
            progression = GetComponent<PlayerProgression>();
            reputation = GetComponent<Reputation>();
        }

        void OnEnable()
        {
            EventBus<DamageDealtEvent>.Subscribe(OnDamageDealt);
            EventBus<CropHarvestedEvent>.Subscribe(OnCropHarvested);
            inventory.Changed += OnInventoryChanged;
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            EventBus<DamageDealtEvent>.Unsubscribe(OnDamageDealt);
            EventBus<CropHarvestedEvent>.Unsubscribe(OnCropHarvested);
            inventory.Changed -= OnInventoryChanged;
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        // ---------- Status ----------

        public QuestStatus StatusOf(QuestData quest)
        {
            if (quest == null) return QuestStatus.Inactive;
            if (active.ContainsKey(quest)) return IsReady(quest) ? QuestStatus.Ready : QuestStatus.Active;
            if (completed.Contains(quest) && !(quest.IsRepeatable && !CompletedToday(quest))) return QuestStatus.Completed;
            return QuestStatus.Inactive;
        }

        public bool CanStart(QuestData quest) =>
            quest != null && StatusOf(quest) == QuestStatus.Inactive && LockReason(quest) == null;

        /// <summary>Why an inactive quest can't be started yet (missing prerequisite or standing), or null.</summary>
        public string LockReason(QuestData quest)
        {
            if (quest == null) return null;
            if (quest.Prerequisites != null)
                foreach (var required in quest.Prerequisites)
                    if (required != null && !completed.Contains(required)) return $"Finish \"{required.Title}\" first";
            if (quest.RequiredTier > 0 && reputation != null && !reputation.MeetsTier(quest.RequiredTier))
                return $"Requires {reputation.NameOfTier(quest.RequiredTier)} standing";
            return null;
        }

        /// <summary>Repeatable contracts can be done once per in-game day.</summary>
        public bool CompletedToday(QuestData quest) =>
            completedOnDay.TryGetValue(quest, out int day) && Services.TryGet(out WorldClock clock) && clock.Day == day;

        public int ObjectiveProgress(QuestData quest, int index)
        {
            var objective = quest.Objectives[index];
            if (objective.Type == ObjectiveType.Collect)
                return objective.Item != null ? Mathf.Min(objective.Count, inventory.CountOf(objective.Item)) : 0;
            return active.TryGetValue(quest, out var counts) ? Mathf.Min(objective.Count, counts[index]) : 0;
        }

        public bool IsObjectiveDone(QuestData quest, int index) => ObjectiveProgress(quest, index) >= quest.Objectives[index].Count;

        public bool IsReady(QuestData quest)
        {
            if (quest == null || !active.ContainsKey(quest)) return false;
            if (quest.Objectives == null) return true;
            for (int i = 0; i < quest.Objectives.Length; i++)
                if (!IsObjectiveDone(quest, i)) return false;
            return true;
        }

        // ---------- Lifecycle ----------

        public bool StartQuest(QuestData quest)
        {
            if (!CanStart(quest)) return false;

            active[quest] = new int[quest.Objectives?.Length ?? 0];
            announcedReady.Remove(quest);
            EventBus<QuestStartedEvent>.Raise(new QuestStartedEvent(quest));
            if (focused == null || !active.ContainsKey(focused)) SetFocus(quest); // nothing tracked yet: track the new one
            CheckReady(quest); // e.g. collect quests you already hold the items for
            return true;
        }

        /// <summary>Drops a side quest or contract (progress is lost). Story quests can't be abandoned.</summary>
        public bool Abandon(QuestData quest)
        {
            if (quest == null || quest.Type == QuestType.Story || !active.Remove(quest)) return false;
            announcedReady.Remove(quest);
            EventBus<QuestAbandonedEvent>.Raise(new QuestAbandonedEvent(quest));
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"Abandoned: {quest.Title}"));
            if (focused == quest) FocusBestRemaining();
            return true;
        }

        // ---------- Focus (the one tracked quest) ----------

        /// <summary>The quest shown on the HUD, minimap and waypoint. Null when nothing is active.</summary>
        public QuestData FocusedQuest => focused != null && active.ContainsKey(focused) ? focused : null;

        public void SetFocus(QuestData quest)
        {
            if (quest != null && !active.ContainsKey(quest)) return;
            if (focused == quest) return;
            focused = quest;
            EventBus<QuestFocusChangedEvent>.Raise(new QuestFocusChangedEvent(quest));
        }

        /// <summary>Tracks the next active quest (story → side → contracts, then wraps).</summary>
        public void CycleFocus()
        {
            var ordered = SortedActive();
            if (ordered.Count == 0) return;
            int current = focused != null ? ordered.IndexOf(focused) : -1;
            SetFocus(ordered[(current + 1) % ordered.Count]);
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"Tracking: {focused.Title}"));
        }

        /// <summary>Active quests ordered story → side → contracts, keeping start order within each type.</summary>
        public List<QuestData> SortedActive()
        {
            sortedBuffer.Clear();
            foreach (QuestType type in Enum.GetValues(typeof(QuestType)))
                foreach (var quest in active.Keys)
                    if (quest.Type == type) sortedBuffer.Add(quest);
            return sortedBuffer;
        }

        void FocusBestRemaining()
        {
            var ordered = SortedActive();
            SetFocus(ordered.Count > 0 ? ordered[0] : null);
        }

        public void CompleteObjective(QuestData quest, int index)
        {
            if (!active.TryGetValue(quest, out var counts) || index < 0 || index >= counts.Length) return;
            counts[index] = quest.Objectives[index].Count;
            OnProgress(quest);
        }

        public bool TurnIn(QuestData quest)
        {
            if (!IsReady(quest)) return false;

            if (quest.TakeItemsOnTurnIn && quest.Objectives != null)
                foreach (var objective in quest.Objectives)
                    if (objective.Type == ObjectiveType.Collect && objective.Item != null)
                        inventory.Remove(objective.Item, objective.Count);

            active.Remove(quest);
            completed.Add(quest);
            if (Services.TryGet(out WorldClock clock)) completedOnDay[quest] = clock.Day;

            GrantRewards(quest);
            EventBus<QuestCompletedEvent>.Raise(new QuestCompletedEvent(quest));
            if (focused == quest) FocusBestRemaining();
            return true;
        }

        void GrantRewards(QuestData quest)
        {
            if (quest.GoldReward > 0) inventory.AddGold(quest.GoldReward);
            if (quest.ItemRewards != null)
            {
                foreach (var reward in quest.ItemRewards)
                {
                    if (reward.Item == null) continue;
                    int leftover = inventory.Add(reward.Item, reward.Count);
                    if (leftover > 0) ItemPickup.SpawnItem(transform.position, reward.Item, leftover, 1f); // bag full: drop it
                }
            }
            if (progression != null)
            {
                progression.AddXp(Discipline.Combat, quest.CombatXp);
                progression.AddXp(Discipline.Farming, quest.FarmingXp);
            }
        }

        // ---------- Progress ----------

        void OnDamageDealt(DamageDealtEvent evt)
        {
            if (evt.Result != HitResult.Killed || evt.Attacker != self) return;
            evt.Target.TryGetComponent(out EnemyController enemy);
            var killed = enemy != null ? enemy.Data : null;
            AdvanceMatching(o => o.Type == ObjectiveType.Kill && (o.Enemy == null || o.Enemy == killed), 1);
        }

        void OnCropHarvested(CropHarvestedEvent evt) =>
            AdvanceMatching(o => o.Type == ObjectiveType.Harvest && (o.Crop == null || o.Crop == evt.Crop), evt.Count);

        void AdvanceMatching(Func<QuestData.Objective, bool> matches, int amount)
        {
            scratch.Clear();
            scratch.AddRange(active.Keys);
            foreach (var quest in scratch)
            {
                bool changed = false;
                var counts = active[quest];
                if (quest.Objectives == null) continue;
                for (int i = 0; i < quest.Objectives.Length; i++)
                {
                    if (!matches(quest.Objectives[i]) || counts[i] >= quest.Objectives[i].Count) continue;
                    counts[i] = Mathf.Min(quest.Objectives[i].Count, counts[i] + amount);
                    changed = true;
                }
                if (changed) OnProgress(quest);
            }
        }

        void OnInventoryChanged()
        {
            // Collect objectives are live counts; just re-check readiness for notifications.
            scratch.Clear();
            scratch.AddRange(active.Keys);
            foreach (var quest in scratch) CheckReady(quest);
        }

        void OnProgress(QuestData quest)
        {
            EventBus<QuestProgressEvent>.Raise(new QuestProgressEvent(quest));
            CheckReady(quest);
        }

        void CheckReady(QuestData quest)
        {
            if (!IsReady(quest))
            {
                announcedReady.Remove(quest);
                return;
            }
            if (quest.AutoComplete)
            {
                TurnIn(quest);
                return;
            }
            if (announcedReady.Add(quest))
                EventBus<QuestReadyEvent>.Raise(new QuestReadyEvent(quest));
        }

        // ---------- Save ----------

        public string CaptureState()
        {
            var state = new State
            {
                activeIds = new string[active.Count],
                progressLengths = new int[active.Count],
                completedIds = new string[completed.Count],
                completedDays = new int[completed.Count],
            };
            var progress = new List<int>();
            int n = 0;
            foreach (var pair in active)
            {
                state.activeIds[n] = pair.Key.Id;
                state.progressLengths[n] = pair.Value.Length;
                progress.AddRange(pair.Value);
                n++;
            }
            state.progressFlat = progress.ToArray();

            n = 0;
            foreach (var quest in completed)
            {
                state.completedIds[n] = quest.Id;
                state.completedDays[n] = completedOnDay.TryGetValue(quest, out int day) ? day : 0;
                n++;
            }
            state.focusedId = FocusedQuest != null ? FocusedQuest.Id : string.Empty;
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            active.Clear();
            completed.Clear();
            completedOnDay.Clear();
            announcedReady.Clear();
            if (!Services.TryGet(out GameDatabase database)) return;

            int cursor = 0;
            if (state.activeIds != null)
            {
                for (int q = 0; q < state.activeIds.Length; q++)
                {
                    int savedLength = state.progressLengths != null && q < state.progressLengths.Length ? state.progressLengths[q] : 0;
                    int start = cursor;
                    cursor += savedLength; // always consume this quest's slice, even if it's skipped

                    if (!database.TryGet(state.activeIds[q], out QuestData quest))
                    {
                        Debug.LogWarning($"[Quests] Saved quest '{state.activeIds[q]}' no longer exists; dropped.");
                        continue;
                    }
                    // Objectives may have been added/removed since saving: copy what overlaps.
                    var counts = new int[quest.Objectives?.Length ?? 0];
                    for (int i = 0; i < counts.Length && i < savedLength && state.progressFlat != null && start + i < state.progressFlat.Length; i++)
                        counts[i] = state.progressFlat[start + i];
                    active[quest] = counts;
                }
            }

            if (state.completedIds != null)
            {
                for (int i = 0; i < state.completedIds.Length; i++)
                {
                    if (!database.TryGet(state.completedIds[i], out QuestData quest)) continue;
                    completed.Add(quest);
                    completedOnDay[quest] = state.completedDays != null && i < state.completedDays.Length ? state.completedDays[i] : 0;
                }
            }

            foreach (var quest in active.Keys)
                if (IsReady(quest)) announcedReady.Add(quest);

            focused = null;
            if (!string.IsNullOrEmpty(state.focusedId) && database.TryGet(state.focusedId, out QuestData saved) && active.ContainsKey(saved))
                SetFocus(saved);
            else
                FocusBestRemaining();
        }
    }
}
