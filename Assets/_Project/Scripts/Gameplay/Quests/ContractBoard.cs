using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A notice board of repeatable contracts (each once per day). Opens the contracts screen.
    /// Milestone 48: the board posts a few contracts a day from its pool, a different draw each morning. Contracts
    /// you've taken stay posted until handed in; contracts above your standing appear only as a locked teaser,
    /// one tier up at most. The draw is fixed per day, so reloading doesn't reshuffle it.
    /// </summary>
    public sealed class ContractBoard : Interactable
    {
        [SerializeField] string boardName = "Contracts Board";
        [SerializeField, Tooltip("Every contract this board can post.")] QuestData[] contracts;
        [SerializeField, Tooltip("How many fresh contracts are posted each day (taken ones stay on top of these).")]
        int postedPerDay = 4;
        [SerializeField] float range = 2.5f;

        readonly List<QuestData> posted = new();
        int postedDay = -1;
        int postedTier = -1;
        int postedTaken = -1;
        QuestLog log;
        Reputation reputation;

        public string BoardName => boardName;
        /// <summary>Every contract this board can post (the rotation draws from these).</summary>
        public QuestData[] Pool => contracts;
        public int PostedPerDay => postedPerDay;

        /// <summary>Today's postings: what you've taken, then today's draw.</summary>
        public QuestData[] Contracts
        {
            get
            {
                Refresh();
                return posted.ToArray();
            }
        }

        public bool Posts(QuestData quest) => contracts != null && System.Array.IndexOf(contracts, quest) >= 0;

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;

            prompt.Text = $"Read the {boardName}";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            Services.Get<GameStateService>().SetState(GameState.InGameMenu);
            EventBus<ContractBoardOpenedEvent>.Raise(new ContractBoardOpenedEvent(this));
        }

        /// <summary>Redraws when the day, your standing tier or the contracts you hold change.</summary>
        void Refresh()
        {
            if (contracts == null) return;
            if (log == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    log = player.GetComponent<QuestLog>();
                    reputation = player.GetComponent<Reputation>();
                }
            }
            int day = Calendar.Today;
            int tier = reputation != null ? reputation.TierIndex : 0;
            int taken = 17;
            foreach (var c in contracts) if (c != null && Held(c)) taken = unchecked(taken * 31 + c.GetInstanceID());
            if (day == postedDay && tier == postedTier && taken == postedTaken) return;
            postedDay = day;
            postedTier = tier;
            postedTaken = taken;

            posted.Clear();
            foreach (var c in contracts) if (c != null && Held(c)) posted.Add(c);

            // Today's draw: a shuffle seeded by the day.
            var candidates = new List<QuestData>();
            foreach (var c in contracts) if (c != null && !posted.Contains(c) && c.RequiredTier <= tier + 1) candidates.Add(c);
            var random = new System.Random(unchecked(day * 7919 + 131));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            int fresh = 0, teasers = 0;
            foreach (var c in candidates)
            {
                if (fresh >= postedPerDay) break;
                bool locked = c.RequiredTier > tier;
                if (locked && teasers >= 1) continue; // at most one locked teaser a day
                posted.Add(c);
                fresh++;
                if (locked) teasers++;
            }
        }

        /// <summary>Taken and not handed in, or handed in today ("Done today" stays on the board).</summary>
        bool Held(QuestData contract)
        {
            if (log == null) return false;
            var status = log.StatusOf(contract);
            return status is QuestStatus.Active or QuestStatus.Ready || (status == QuestStatus.Completed && contract.RequiredTier <= TierNow && !log.CanStart(contract));
        }

        int TierNow => reputation != null ? reputation.TierIndex : 0;
    }

    public readonly struct ContractBoardOpenedEvent : IEvent
    {
        public readonly ContractBoard Board;
        public ContractBoardOpenedEvent(ContractBoard board) { Board = board; }
    }
}
