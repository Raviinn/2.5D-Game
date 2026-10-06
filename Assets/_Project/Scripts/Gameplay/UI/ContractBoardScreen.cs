using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Prototype contracts board UI (IMGUI): accept repeatable jobs, see their status at a glance
    /// (badge + progress bar), track one, and turn them in here. Each contract can be done once per in-game day.
    /// Some contracts need a standing tier with the town; they show as LOCKED with what's required.
    /// </summary>
    public sealed class ContractBoardScreen : MonoBehaviour
    {
        const float RowHeight = 142f;

        ContractBoard board;
        QuestLog log;
        GameStateService state;
        Vector2 scroll;

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

            var area = UITheme.Window(940f, 700f, board.BoardName, $"<color={UITheme.MutedHex}>New work every day</color>",
                $"<color={UITheme.MutedHex}>Accepted contracts appear in your journal (J) and the tracker.   ·   Tab / Esc: close</color>");

            var contracts = board.Contracts;
            int count = contracts?.Length ?? 0;
            UITheme.Inset(area);
            var view = new Rect(area.x + 4f, area.y + 4f, area.width - 8f, area.height - 8f);
            var content = new Rect(0f, 0f, view.width - 20f, Mathf.Max(1, count) * RowHeight + 4f);
            scroll = GUI.BeginScrollView(view, scroll, content);
            if (count == 0)
                GUI.Label(new Rect(12f, 12f, content.width - 24f, 40f), $"<color={UITheme.MutedHex}>No work posted today.</color>", UITheme.Body);
            for (int i = 0; i < count; i++)
                if (contracts[i] != null) DrawContract(new Rect(4f, 4f + i * RowHeight, content.width - 8f, RowHeight - 8f), contracts[i]);
            GUI.EndScrollView();
        }

        void DrawContract(Rect row, QuestData quest)
        {
            var status = log.StatusOf(quest);
            string locked = status == QuestStatus.Inactive ? log.LockReason(quest) : null;
            bool active = status is QuestStatus.Active or QuestStatus.Ready;
            bool tracked = quest == log.FocusedQuest;
            UITheme.Slot(row, tracked, false);

            var (badgeText, badgeColor) = status switch
            {
                QuestStatus.Active => ("IN PROGRESS", UITheme.Gold),
                QuestStatus.Ready => ("READY", UITheme.Good),
                QuestStatus.Completed => ("DONE TODAY", UITheme.Muted),
                _ when locked != null => ("LOCKED", UITheme.Bad),
                _ => ("AVAILABLE", UITheme.Info),
            };
            UITheme.Badge(new Rect(row.x + 14f, row.y + 12f, 110f, 22f), badgeText, badgeColor);
            GUI.Label(new Rect(row.x + 136f, row.y + 10f, row.width - 340f, 26f), $"<b>{quest.Title}</b>", UITheme.BodyMiddle);
            if (tracked) UITheme.DrawIcon(new Rect(row.xMax - 196f, row.y + 16f, 14f, 14f), UITheme.DiamondIcon, UITheme.Gold);

            string details = active ? QuestText.Objectives(quest, log) : ObjectiveList(quest);
            GUI.Label(new Rect(row.x + 14f, row.y + 42f, row.width - 220f, row.height - 46f),
                $"<size=16><i><color={UITheme.MutedHex}>{quest.Summary}</color></i>\n{details}\n<color={UITheme.GoldHex}>Reward: {QuestText.Rewards(quest)}</color></size>",
                UITheme.Body);

            float bx = row.xMax - 180f;
            if (active)
            {
                var (done, total) = QuestText.Progress(quest, log);
                UITheme.Bar(new Rect(bx, row.y + 14f, 166f, 8f), total > 0 ? done / (float)total : 0f, status == QuestStatus.Ready ? UITheme.Good : UITheme.Xp);
            }

            var button = new Rect(bx, row.y + 34f, 166f, 38f);
            var secondary = new Rect(bx, row.y + 78f, 166f, 32f);
            switch (status)
            {
                case QuestStatus.Inactive when locked != null:
                    GUI.Label(new Rect(bx - 20f, button.y, button.width + 20f, 64f), $"<color={UITheme.BadHex}>{locked}</color>", UITheme.BodyCenter);
                    break;
                case QuestStatus.Inactive:
                    if (UITheme.Button(button, "Accept", log.CanStart(quest), primary: true)) log.StartQuest(quest);
                    break;
                case QuestStatus.Active:
                    GUI.Label(button, $"<color={UITheme.GoldHex}>In progress…</color>", UITheme.BodyCenter);
                    break;
                case QuestStatus.Ready:
                    if (UITheme.Button(button, "Turn in", primary: true)) log.TurnIn(quest);
                    break;
                case QuestStatus.Completed:
                    GUI.Label(button, $"<color={UITheme.MutedHex}>Back tomorrow</color>", UITheme.BodyCenter);
                    break;
            }

            if (active && UITheme.Button(secondary, tracked ? "Tracking" : "Track", !tracked)) log.SetFocus(quest);
        }

        static string ObjectiveList(QuestData quest)
        {
            if (quest.Objectives == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (var objective in quest.Objectives)
                sb.Append("• ").Append(objective.Text).Append(objective.Count > 1 ? $" ×{objective.Count}" : string.Empty).Append('\n');
            return sb.ToString().TrimEnd('\n');
        }
    }
}
