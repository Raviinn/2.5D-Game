using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The contracts board, on parchment: repeatable jobs as paper cards (the tracked one framed in vermilion) with a
    /// status tag, summary, objectives and reward; Accept, Turn in and Track as brush buttons. Each contract can be done
    /// once per in-game day. Some need a standing tier with the town and show as LOCKED with what's required.
    /// </summary>
    public sealed class ContractBoardScreen : MonoBehaviour
    {
        const float RowHeight = 168f;

        ContractBoard board;
        QuestLog log;
        GameStateService state;
        Vector2 scroll;
        GUIStyle tagStyle, titleStyle, bodyStyle, centred;

        void OnEnable()
        {
            EventBus<ContractBoardOpenedEvent>.Subscribe(OnBoardOpened);
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus<ContractBoardOpenedEvent>.Unsubscribe(OnBoardOpened);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) log = player.GetComponent<QuestLog>();
            state = Services.Get<GameStateService>();
        }

        void OnBoardOpened(ContractBoardOpenedEvent evt)
        {
            board = evt.Board;
            scroll = Vector2.zero;
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) board = null;
        }

        void OnGUI()
        {
            if (board == null || log == null || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();
            EnsureStyles();

            var area = UITheme.ParchmentWindow(1000f, 780f, board.BoardName, "New work every day");
            var contracts = board.Contracts;
            int count = contracts?.Length ?? 0;
            var view = new Rect(area.x, area.y, area.width, area.height - 52f);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(1, count) * (RowHeight + 10f));
            scroll = GUI.BeginScrollView(view, scroll, content);
            if (count == 0)
                GUI.Label(new Rect(4f, 4f, content.width - 8f, 40f), "No work posted today.", UITheme.PaperMuted);
            for (int i = 0; i < count; i++)
                if (contracts[i] != null) DrawContract(new Rect(0f, i * (RowHeight + 10f), content.width, RowHeight), contracts[i]);
            GUI.EndScrollView();

            GUI.Label(new Rect(area.x, area.yMax - 34f, area.width - 220f, 30f), "Accepted contracts appear in your journal and the tracker.", UITheme.PaperMuted);
            UITheme.KeyHints(area.xMax, area.yMax - 36f, false, ("Esc", "Leave"));
        }

        void DrawContract(Rect row, QuestData quest)
        {
            var status = log.StatusOf(quest);
            string locked = status == QuestStatus.Inactive ? log.LockReason(quest) : null;
            bool active = status is QuestStatus.Active or QuestStatus.Ready;
            bool tracked = quest == log.FocusedQuest;
            UITheme.PaperSlot(row, tracked, false);

            var (tag, tagHex) = status switch
            {
                QuestStatus.Active => ("In progress", UITheme.InkGoldDarkHex),
                QuestStatus.Ready => ("Ready", UITheme.GoodOnPaperHex),
                QuestStatus.Completed => ("Done today", UITheme.MutedOnPaperHex),
                _ when locked != null => ("Locked", UITheme.VermilionHex),
                _ => ("Available", UITheme.InfoOnPaperHex),
            };
            float x = row.x + 22f;
            float textWidth = row.width - 290f;
            GUI.Label(new Rect(x, row.y + 14f, textWidth, 20f), $"<color={tagHex}>{UITheme.Spaced(tag)}</color>", tagStyle);
            GUI.Label(new Rect(x, row.y + 36f, textWidth, 30f), quest.Title, titleStyle);
            if (tracked) UITheme.DrawIcon(new Rect(x + titleStyle.CalcSize(new GUIContent(quest.Title)).x + 10f, row.y + 44f, 14f, 14f), UITheme.DiamondIcon, UITheme.InkGoldDark);

            string details = active ? QuestText.Objectives(quest, log, UITheme.GoodOnPaperHex) : ObjectiveList(quest);
            GUI.Label(new Rect(x, row.y + 70f, textWidth, row.height - 74f),
                $"<i><color={UITheme.MutedOnPaperHex}>{quest.Summary}</color></i>\n{details}\n<color={UITheme.InkGoldDarkHex}>Reward: {QuestText.Rewards(quest)}</color>",
                bodyStyle);

            var buttons = new Rect(row.xMax - 250f, row.y + 22f, 228f, row.height - 44f);
            if (active)
            {
                var (done, total) = QuestText.Progress(quest, log);
                UITheme.ThinBar(new Rect(buttons.x, buttons.y, buttons.width, 5f), total > 0 ? done / (float)total : 0f,
                    status == QuestStatus.Ready ? new Color(0.25f, 0.43f, 0.2f) : UITheme.Vermilion);
            }

            var main = new Rect(buttons.x, buttons.y + 18f, buttons.width, 50f);
            var secondary = new Rect(buttons.x, buttons.y + 76f, buttons.width, 44f);
            switch (status)
            {
                case QuestStatus.Inactive when locked != null:
                    GUI.Label(new Rect(buttons.x, main.y, buttons.width, 70f), $"<color={UITheme.VermilionHex}>{locked}</color>", centred);
                    break;
                case QuestStatus.Inactive:
                    if (UITheme.BrushButton(main, "Accept", log.CanStart(quest))) log.StartQuest(quest);
                    break;
                case QuestStatus.Active:
                    GUI.Label(main, $"<color={UITheme.InkGoldDarkHex}>In progress…</color>", centred);
                    break;
                case QuestStatus.Ready:
                    if (UITheme.BrushButton(main, "Turn in")) log.TurnIn(quest);
                    break;
                case QuestStatus.Completed:
                    GUI.Label(main, "Back tomorrow", centred);
                    break;
            }

            if (active && UITheme.BrushButton(secondary, tracked ? "Tracking" : "Track", !tracked)) log.SetFocus(quest);
        }

        static string ObjectiveList(QuestData quest)
        {
            if (quest.Objectives == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (var objective in quest.Objectives)
                sb.Append("• ").Append(objective.Text).Append(objective.Count > 1 ? $" ×{objective.Count}" : string.Empty).Append('\n');
            return sb.ToString().TrimEnd('\n');
        }

        void EnsureStyles()
        {
            if (titleStyle != null && titleStyle.font == UITheme.InkHeader.font) return;
            tagStyle = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleLeft, fontSize = UITheme.PaperMuted.fontSize - 2 };
            titleStyle = new GUIStyle(UITheme.InkHeader) { fontSize = 22 };
            bodyStyle = new GUIStyle(UITheme.PaperMuted) { normal = { textColor = UITheme.Ink } };
            centred = new GUIStyle(UITheme.PaperBody) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
