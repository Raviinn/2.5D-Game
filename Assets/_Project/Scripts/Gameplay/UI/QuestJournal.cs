using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The Journal tab of the game menu (J), in the ink-and-parchment style:
    /// - filter cards on the left (All / Story / Side / Contracts / Completed, with counts) and, under them, a card with
    ///   the player's standing with the town (tier, progress to the next tier, what the tier gives)
    /// - the quest list in the middle (the tracked quest has a gold diamond; the selected one is a vermilion block)
    /// - an ink panel on the right: summary, objectives, reward, where to go; Track (the quest the HUD, minimap and
    ///   waypoint follow) and Abandon (side quests and contracts only, after a confirm — Esc cancels it)
    /// </summary>
    public sealed class QuestJournal : MonoBehaviour, IGameMenuTab
    {
        enum Filter { All, Story, Side, Contracts, Completed }

        static readonly string[] FilterNames = { "All", "Story", "Side", "Contracts", "Completed" };
        const float CardsWidth = 260f;
        const float ListWidth = 520f;
        const float RowHeight = 72f;

        QuestLog log;
        Reputation reputation;
        Transform player;
        Filter filter;
        QuestData selected;
        bool confirmAbandon;
        Vector2 listScroll;
        Vector2 detailScroll;
        readonly List<QuestData> rows = new();
        GUIStyle rowTitle, rowStatus, headerRight, nameStyle, metaStyle, bodyStyle, warningStyle;

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                log = playerObject.GetComponent<QuestLog>();
                reputation = playerObject.GetComponent<Reputation>();
            }
        }

        // ---------- Game menu tab ----------

        public bool CanOpen => log != null;
        public string FooterTip => "The <color=#c9a86a>tracked quest</color> shows on the HUD, the minimap and the waypoint";
        public (string key, string label)[] KeyHints => null;
        public bool HasSubView => confirmAbandon;
        public void CloseSubView() => confirmAbandon = false;

        public void OnTabOpened()
        {
            selected = log.FocusedQuest; // open on the tracked quest
            confirmAbandon = false;
        }

        public void DrawTab(Rect bounds)
        {
            if (log == null) return;
            EnsureStyles();
            BuildRows();
            if (selected != null && !rows.Contains(selected)) selected = null;
            if (selected == null && rows.Count > 0) selected = rows[0];

            // Filters | list | details, the details panel taking the rest of the width (capped, then centred).
            float detailsWidth = Mathf.Min(760f, bounds.width - CardsWidth - ListWidth - 80f);
            float total = CardsWidth + 40f + ListWidth + 40f + detailsWidth;
            float x = bounds.x + (bounds.width - total) * 0.5f;
            DrawFilters(new Rect(x, bounds.y, CardsWidth, bounds.height));
            DrawList(new Rect(x + CardsWidth + 40f, bounds.y, ListWidth, bounds.height));
            DrawDetails(new Rect(x + CardsWidth + ListWidth + 80f, bounds.y, detailsWidth, bounds.height));
        }

        // ---------- Filters & standing ----------

        void DrawFilters(Rect area)
        {
            float y = area.y;
            for (int i = 0; i < FilterNames.Length; i++)
            {
                var f = (Filter)i;
                if (UITheme.FilterCard(new Rect(area.x, y, area.width, 60f), FilterNames[i], Count(f).ToString(), null, filter == f) && filter != f)
                {
                    filter = f;
                    selected = null;
                    confirmAbandon = false;
                    listScroll = Vector2.zero;
                }
                y += 70f;
            }
            if (reputation != null && reputation.IsConfigured)
                DrawStanding(new Rect(area.x, area.yMax - 170f, area.width, 170f));
        }

        void DrawStanding(Rect card)
        {
            UITheme.PaperCard(card, false);
            float x = card.x + 18f;
            float width = card.width - 36f;
            GUI.Label(new Rect(x, card.y + 12f, width, 26f), UITheme.Fit(reputation.FactionName, UITheme.InkHeader, width), UITheme.InkHeader);
            GUI.Label(new Rect(x, card.y + 44f, width, 24f), $"<color={UITheme.VermilionHex}>{reputation.TierName}</color>", UITheme.PaperBody);

            var tier = reputation.Tier;
            var next = reputation.NextTier;
            float fill = next == null ? 1f : Mathf.InverseLerp(tier.MinStanding, next.MinStanding, reputation.Standing);
            UITheme.ThinBar(new Rect(x, card.y + 76f, width, 5f), fill, UITheme.Vermilion);
            string progress = next == null ? $"Standing {reputation.Standing} · highest tier" : $"Standing {reputation.Standing} / {next.MinStanding} · next: {next.Name}";
            GUI.Label(new Rect(x, card.y + 88f, width, 22f), progress, UITheme.PaperMuted);
            GUI.Label(new Rect(x, card.y + 112f, width, 46f), $"<color={UITheme.GoodOnPaperHex}>{tier.Perk}</color>", UITheme.PaperMuted);
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
            GUI.Label(new Rect(area.x, area.y, area.width, 40f), UITheme.Spaced("Journal"), UITheme.InkHeader);
            GUI.Label(new Rect(area.x, area.y, area.width, 40f), $"{rows.Count} {(rows.Count == 1 ? "quest" : "quests")}", headerRight);
            UITheme.Fill(new Rect(area.x, area.y + 48f, area.width, 1f), new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.4f));

            var view = new Rect(area.x, area.y + 62f, area.width, area.height - 62f);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(1, rows.Count) * (RowHeight + 8f));
            listScroll = GUI.BeginScrollView(view, listScroll, content);

            if (rows.Count == 0)
            {
                string empty = filter == Filter.Completed
                    ? "Nothing completed yet."
                    : "No quests here yet.\nTalk to people marked with ! or check the contracts board.";
                GUI.Label(new Rect(4f, 4f, content.width - 8f, 80f), empty, UITheme.PaperMuted);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var quest = rows[i];
                var row = new Rect(0f, i * (RowHeight + 8f), content.width, RowHeight);
                bool isSelected = quest == selected;
                bool hovered = !isSelected && row.Contains(Event.current.mousePosition);
                UITheme.PaperCard(row, isSelected);
                if (hovered) UITheme.Fill(new Rect(row.x + 1f, row.yMax - 3f, row.width - 2f, 3f), UITheme.Vermilion);

                float textX = row.x + 18f;
                if (quest == log.FocusedQuest)
                {
                    UITheme.DrawIcon(new Rect(row.x + 16f, row.y + 15f, 16f, 16f), UITheme.DiamondIcon, isSelected ? UITheme.OffWhite : UITheme.InkGoldDark);
                    textX += 24f;
                }
                string titleHex = isSelected ? UITheme.OffWhiteHex : QuestText.TypeColorOnPaper(quest.Type);
                GUI.Label(new Rect(textX, row.y + 8f, row.xMax - textX - 12f, 30f), $"<color={titleHex}>{quest.Title}</color>", rowTitle);
                rowStatus.normal.textColor = isSelected ? UITheme.OffWhite : UITheme.MutedOnPaper;
                GUI.Label(new Rect(row.x + 18f, row.y + 40f, row.width - 30f, 24f), StatusText(quest, isSelected), rowStatus);

                if (UITheme.PaperClick(row))
                {
                    selected = quest;
                    confirmAbandon = false;
                }
            }
            GUI.EndScrollView();
        }

        string StatusText(QuestData quest, bool onRed)
        {
            string good = onRed ? UITheme.OffWhiteHex : UITheme.GoodOnPaperHex;
            string accent = onRed ? UITheme.OffWhiteHex : UITheme.InkGoldDarkHex;
            switch (log.StatusOf(quest))
            {
                case QuestStatus.Ready: return UITheme.Colored($"Ready · return to {quest.TurnInAt}", good);
                case QuestStatus.Completed: return UITheme.Colored("Completed", good);
                case QuestStatus.Active:
                    var (done, total) = QuestText.Progress(quest, log);
                    return $"<color={accent}>In progress</color> · {done}/{total} · {quest.Type}";
                default: return "Not started";
            }
        }

        // ---------- Details ----------

        void DrawDetails(Rect area)
        {
            UITheme.InkPanel(area);
            if (selected == null) return;

            var quest = selected;
            var status = log.StatusOf(quest);
            bool active = status is QuestStatus.Active or QuestStatus.Ready;

            float x = area.x + 30f;
            float width = area.width - 60f;
            float y = area.y + 28f;

            GUI.Label(new Rect(x, y, width, 36f), $"<color={QuestText.TypeColorOnInk(quest.Type)}>{quest.Title}</color>", nameStyle);
            y += 42f;
            string state = status switch
            {
                QuestStatus.Ready => $"<color={UITheme.GoodOnInkHex}>Ready</color>",
                QuestStatus.Completed => $"<color={UITheme.GoodOnInkHex}>Completed</color>",
                QuestStatus.Active => $"<color={UITheme.InkGoldHex}>In progress</color>",
                _ => "Not started",
            };
            GUI.Label(new Rect(x, y, width, 24f), $"{state}   ·   {quest.Type} quest   ·   return to {quest.TurnInAt}", metaStyle);
            y += 32f;

            if (active)
            {
                var (done, total) = QuestText.Progress(quest, log);
                UITheme.ThinBar(new Rect(x, y, width, 5f), total > 0 ? done / (float)total : 0f, status == QuestStatus.Ready ? new Color(0.61f, 0.79f, 0.54f) : UITheme.Vermilion, onInk: true);
                y += 20f;
            }
            UITheme.Fill(new Rect(x, y, width, 1f), new Color(1f, 1f, 1f, 0.18f));
            y += 18f;

            string target = string.Empty;
            if (active && player != null && QuestNavigator.TryGetTarget(quest, log, player.position, out var position, out var label))
                target = $"\n\n<color={UITheme.InkGoldHex}>Where to go</color>\n{label}  <color={UITheme.MutedOnInkHex}>({Vector3.Distance(player.position, position):0} m away)</color>";

            string body = $"<i><color={UITheme.MutedOnInkHex}>{quest.Summary}</color></i>" +
                          $"\n\n<color={UITheme.InkGoldHex}>Objectives</color>\n{QuestText.Objectives(quest, log, UITheme.GoodOnInkHex)}" +
                          $"\n\n<color={UITheme.InkGoldHex}>Reward</color>\n{QuestText.Rewards(quest)}{target}";
            float bodyHeight = bodyStyle.CalcHeight(new GUIContent(body), width - 20f);
            var bodyView = new Rect(x, y, width, area.yMax - y - (active ? 104f : 20f));
            detailScroll = GUI.BeginScrollView(bodyView, detailScroll, new Rect(0f, 0f, width - 20f, bodyHeight));
            GUI.Label(new Rect(0f, 0f, width - 20f, bodyHeight), body, bodyStyle);
            GUI.EndScrollView();

            if (active) DrawActions(new Rect(x, area.yMax - 82f, width, 52f), quest);
        }

        void DrawActions(Rect bar, QuestData quest)
        {
            bool tracked = quest == log.FocusedQuest;
            if (confirmAbandon)
            {
                // The warning takes the Track button's place so it has room to be read.
                int penalty = reputation != null ? reputation.AbandonPenaltyFor(quest) : 0;
                string warning = penalty > 0 ? $"Abandon? Progress is lost and you lose {penalty} standing." : "Abandon? Progress is lost.";
                GUI.Label(new Rect(bar.x, bar.y - 40f, bar.width, 32f), warning, warningStyle);
                float half = bar.width * 0.5f - 6f;
                if (UITheme.BrushButton(new Rect(bar.x, bar.y, half, bar.height), "Yes, abandon", true, light: true))
                {
                    log.Abandon(quest);
                    selected = null;
                    confirmAbandon = false;
                }
                if (UITheme.BrushButton(new Rect(bar.x + half + 12f, bar.y, half, bar.height), "Keep it", true, light: true)) confirmAbandon = false;
                return;
            }

            bool story = quest.Type == QuestType.Story;
            float trackWidth = story ? bar.width : bar.width * 0.6f - 6f;
            if (UITheme.BrushButton(new Rect(bar.x, bar.y, trackWidth, bar.height), tracked ? "Tracking" : "Track this quest", !tracked, light: true))
                log.SetFocus(quest);
            if (story)
                GUI.Label(new Rect(bar.x, bar.yMax + 2f, bar.width, 22f), "Story quests can't be abandoned.", metaStyle);
            else if (UITheme.BrushButton(new Rect(bar.x + trackWidth + 12f, bar.y, bar.width - trackWidth - 12f, bar.height), "Abandon", true, light: true))
                confirmAbandon = true;
        }

        void EnsureStyles()
        {
            if (rowTitle != null && rowTitle.font == UITheme.PaperBody.font) return;
            rowTitle = new GUIStyle(UITheme.PaperBody) { wordWrap = false, alignment = TextAnchor.MiddleLeft, fontSize = UITheme.PaperBody.fontSize + 1 };
            rowStatus = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            headerRight = new GUIStyle(UITheme.PaperMuted) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            nameStyle = new GUIStyle(UITheme.InkHeader) { normal = { textColor = UITheme.OffWhite } };
            metaStyle = new GUIStyle(UITheme.InkSmall) { normal = { textColor = UITheme.MutedOnInk } };
            bodyStyle = new GUIStyle(UITheme.InkBody);
            warningStyle = new GUIStyle(UITheme.InkBody) { normal = { textColor = UITheme.Vermilion } };
        }
    }
}
