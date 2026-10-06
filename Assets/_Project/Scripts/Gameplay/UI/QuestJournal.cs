using System.Collections.Generic;
using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Prototype journal (IMGUI, J, pauses the game): filter tabs (All / Story / Side / Contracts / Completed),
    /// a quest list, and a details pane with Track (choose the quest the HUD, minimap and waypoint follow)
    /// and Abandon (side quests and contracts only, with confirmation). A card under the list shows the
    /// player's standing with the town: tier, progress to the next tier and what the tier gives.
    /// </summary>
    public sealed class QuestJournal : MonoBehaviour
    {
        enum Filter { All, Story, Side, Contracts, Completed }

        static readonly string[] FilterNames = { "All", "Story", "Side", "Contracts", "Completed" };
        const float RowHeight = 58f;
        const float StandingCardHeight = 112f;

        QuestLog log;
        Reputation reputation;
        Transform player;
        GameStateService state;
        InputAction openAction;
        bool isOpen;
        Filter filter;
        QuestData selected;
        bool confirmAbandon;
        Vector2 listScroll;
        Vector2 detailScroll;
        readonly List<QuestData> rows = new();

        void OnEnable() => EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        void OnDisable() => EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                log = playerObject.GetComponent<QuestLog>();
                reputation = playerObject.GetComponent<Reputation>();
            }
            state = Services.Get<GameStateService>();
            openAction = Services.Get<InputService>().Journal;
        }

        void Update()
        {
            if (log != null && openAction.WasPressedThisFrame() && state.Current == GameState.Playing)
            {
                state.SetState(GameState.InGameMenu);
                isOpen = true;
                selected = log.FocusedQuest; // open on the tracked quest
                confirmAbandon = false;
            }
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) isOpen = false;
        }

        void OnGUI()
        {
            if (log == null || !isOpen || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();

            var area = UITheme.Window(1040f, 680f, "Journal", null,
                $"<color={UITheme.MutedHex}>Tracked quests show on the HUD, minimap and waypoint.   ·   J / Esc: close</color>");

            DrawTabs(new Rect(area.x, area.y, area.width, 34f));
            BuildRows();
            if (selected != null && !rows.Contains(selected)) selected = null;
            if (selected == null && rows.Count > 0) selected = rows[0];

            float top = area.y + 46f;
            bool showStanding = reputation != null && reputation.IsConfigured;
            float listBottom = showStanding ? area.yMax - StandingCardHeight - 12f : area.yMax;
            DrawList(new Rect(area.x, top, 360f, listBottom - top));
            if (showStanding) DrawStanding(new Rect(area.x, area.yMax - StandingCardHeight, 360f, StandingCardHeight));
            DrawDetails(new Rect(area.x + 376f, top, area.width - 376f, area.yMax - top));
        }

        // ---------- Tabs & rows ----------

        void DrawTabs(Rect area)
        {
            float width = area.width / FilterNames.Length;
            for (int i = 0; i < FilterNames.Length; i++)
            {
                var f = (Filter)i;
                if (!UITheme.Tab(new Rect(area.x + i * width, area.y, width - 8f, area.height), $"{FilterNames[i]}  ({Count(f)})", filter == f) || filter == f)
                    continue;
                filter = f;
                selected = null;
                confirmAbandon = false;
                listScroll = Vector2.zero;
            }
        }

        void BuildRows()
        {
            rows.Clear();
            if (filter == Filter.Completed)
            {
                rows.AddRange(log.CompletedQuests);
                return;
            }
            foreach (var quest in log.SortedActive())
                if (Matches(quest, filter)) rows.Add(quest);
        }

        int Count(Filter f)
        {
            if (f == Filter.Completed)
            {
                int done = 0;
                foreach (var _ in log.CompletedQuests) done++;
                return done;
            }
            int n = 0;
            foreach (var quest in log.ActiveQuests)
                if (Matches(quest, f)) n++;
            return n;
        }

        static bool Matches(QuestData quest, Filter f) => f switch
        {
            Filter.Story => quest.Type == QuestType.Story,
            Filter.Side => quest.Type == QuestType.Side,
            Filter.Contracts => quest.Type == QuestType.Contract,
            _ => true,
        };

        // ---------- List ----------

        void DrawList(Rect area)
        {
            UITheme.Inset(area);
            var view = new Rect(area.x + 4f, area.y + 4f, area.width - 8f, area.height - 8f);
            var content = new Rect(0f, 0f, view.width - 20f, Mathf.Max(1, rows.Count) * RowHeight + 4f);
            listScroll = GUI.BeginScrollView(view, listScroll, content);

            if (rows.Count == 0)
            {
                string empty = filter == Filter.Completed
                    ? "Nothing completed yet."
                    : "No quests here yet.\nTalk to people marked with ! or check the contracts board.";
                GUI.Label(new Rect(12f, 12f, content.width - 24f, 80f), $"<color={UITheme.MutedHex}>{empty}</color>", UITheme.Body);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var quest = rows[i];
                var row = new Rect(2f, 2f + i * RowHeight, content.width - 4f, RowHeight - 4f);
                if (UITheme.Row(row, string.Empty, quest == selected))
                {
                    selected = quest;
                    confirmAbandon = false;
                }

                bool tracked = quest == log.FocusedQuest;
                float textX = row.x + 14f;
                if (tracked)
                {
                    UITheme.DrawIcon(new Rect(row.x + 10f, row.y + 10f, 14f, 14f), UITheme.DiamondIcon, UITheme.Gold);
                    textX += 20f;
                }
                GUI.Label(new Rect(textX, row.y + 6f, row.xMax - textX - 8f, 22f),
                    $"<b><color={QuestText.TypeColor(quest.Type)}>{quest.Title}</color></b>", UITheme.BodyMiddle);
                GUI.Label(new Rect(row.x + 14f, row.y + 30f, row.width - 22f, 18f), StatusText(quest), UITheme.Small);
            }
            GUI.EndScrollView();
        }

        string StatusText(QuestData quest)
        {
            switch (log.StatusOf(quest))
            {
                case QuestStatus.Ready: return UITheme.Colored($"Ready · return to {quest.TurnInAt}", UITheme.GoodHex);
                case QuestStatus.Completed: return UITheme.Colored("Completed", UITheme.GoodHex);
                case QuestStatus.Active:
                    var (done, total) = QuestText.Progress(quest, log);
                    return $"<color={UITheme.GoldHex}>In progress</color> <color={UITheme.MutedHex}>· {done}/{total} · {quest.Type}</color>";
                default: return UITheme.Colored("Not started", UITheme.MutedHex);
            }
        }

        // ---------- Standing ----------

        void DrawStanding(Rect card)
        {
            UITheme.Panel(card);
            float x = card.x + 16f;
            float width = card.width - 32f;
            GUI.Label(new Rect(x, card.y + 10f, width, 24f), $"<b>{reputation.FactionName}</b>", UITheme.BodyMiddle);
            GUI.Label(new Rect(x, card.y + 10f, width, 24f), $"<color={UITheme.GoldHex}><b>{reputation.TierName}</b></color>", UITheme.SmallRight);

            var tier = reputation.Tier;
            var next = reputation.NextTier;
            float fill = next == null ? 1f : Mathf.InverseLerp(tier.MinStanding, next.MinStanding, reputation.Standing);
            UITheme.Bar(new Rect(x, card.y + 42f, width, 8f), fill, UITheme.Good);

            string progress = next == null
                ? $"Standing {reputation.Standing}  ·  highest tier"
                : $"Standing {reputation.Standing} / {next.MinStanding}  ·  next: {next.Name}";
            GUI.Label(new Rect(x, card.y + 56f, width, 20f), UITheme.Colored(progress, UITheme.MutedHex), UITheme.Small);
            GUI.Label(new Rect(x, card.y + 80f, width, 20f),
                UITheme.Colored(UITheme.Fit(tier.Perk, UITheme.Small, width), UITheme.GoodHex), UITheme.Small);
        }

        // ---------- Details ----------

        void DrawDetails(Rect area)
        {
            UITheme.Inset(area);
            if (selected == null) return;

            var quest = selected;
            var status = log.StatusOf(quest);
            bool active = status is QuestStatus.Active or QuestStatus.Ready;

            float x = area.x + 20f;
            float width = area.width - 40f;
            float y = area.y + 16f;

            GUI.Label(new Rect(x, y, width - 140f, 30f), $"<color={QuestText.TypeColor(quest.Type)}>{quest.Title}</color>", UITheme.Header);
            var (badgeText, badgeColor) = status switch
            {
                QuestStatus.Ready => ("READY", UITheme.Good),
                QuestStatus.Completed => ("COMPLETED", UITheme.Good),
                QuestStatus.Active => ("IN PROGRESS", UITheme.Gold),
                _ => ("NOT STARTED", UITheme.Muted),
            };
            UITheme.Badge(new Rect(area.xMax - 140f, y + 4f, 120f, 22f), badgeText, badgeColor);
            y += 32f;
            GUI.Label(new Rect(x, y, width, 20f), $"<color={UITheme.MutedHex}>{quest.Type} quest  ·  Return to {quest.TurnInAt}</color>", UITheme.Small);
            y += 26f;

            if (active)
            {
                var (done, total) = QuestText.Progress(quest, log);
                UITheme.Bar(new Rect(x, y, width, 8f), total > 0 ? done / (float)total : 0f, status == QuestStatus.Ready ? UITheme.Good : UITheme.Xp);
                y += 20f;
            }

            string target = string.Empty;
            if (active && player != null && QuestNavigator.TryGetTarget(quest, log, player.position, out var position, out var label))
                target = $"\n\n<b>Where to go</b>\n{label}  <color={UITheme.MutedHex}>({Vector3.Distance(player.position, position):0} m away)</color>";

            string body = $"<i><color={UITheme.MutedHex}>{quest.Summary}</color></i>\n\n<b>Objectives</b>\n{QuestText.Objectives(quest, log)}" +
                          $"\n\n<b>Reward</b>\n<color={UITheme.GoldHex}>{QuestText.Rewards(quest)}</color>{target}";
            float bodyHeight = UITheme.Body.CalcHeight(new GUIContent(body), width - 20f);
            var bodyView = new Rect(x, y, width, area.yMax - y - (active ? 70f : 16f));
            detailScroll = GUI.BeginScrollView(bodyView, detailScroll, new Rect(0f, 0f, width - 20f, bodyHeight));
            GUI.Label(new Rect(0f, 0f, width - 20f, bodyHeight), body, UITheme.Body);
            GUI.EndScrollView();

            if (active) DrawActions(new Rect(x, area.yMax - 58f, width, 40f), quest);
        }

        void DrawActions(Rect bar, QuestData quest)
        {
            bool tracked = quest == log.FocusedQuest;
            if (confirmAbandon)
            {
                // The warning takes the Track button's place so it has room to be read.
                int penalty = reputation != null ? reputation.AbandonPenaltyFor(quest) : 0;
                string warning = penalty > 0 ? $"Abandon? Progress is lost and you lose {penalty} standing." : "Abandon? Progress is lost.";
                GUI.Label(new Rect(bar.x, bar.y - 4f, bar.width - 270f, bar.height + 8f), $"<color={UITheme.BadHex}>{warning}</color>", UITheme.Body);
                if (UITheme.Button(new Rect(bar.xMax - 256f, bar.y, 130f, bar.height), "Yes, abandon"))
                {
                    log.Abandon(quest);
                    selected = null;
                    confirmAbandon = false;
                }
                if (UITheme.Button(new Rect(bar.xMax - 116f, bar.y, 116f, bar.height), "Keep it", primary: true)) confirmAbandon = false;
                return;
            }

            if (UITheme.Button(new Rect(bar.x, bar.y, 190f, bar.height), tracked ? "Tracking" : "Track this quest", !tracked, primary: !tracked))
                log.SetFocus(quest);

            if (quest.Type == QuestType.Story)
                GUI.Label(new Rect(bar.x + 206f, bar.y, 300f, bar.height), $"<color={UITheme.MutedHex}>Story quests can't be abandoned.</color>", UITheme.Small);
            else if (UITheme.Button(new Rect(bar.xMax - 150f, bar.y, 150f, bar.height), "Abandon"))
                confirmAbandon = true;
        }
    }
}
