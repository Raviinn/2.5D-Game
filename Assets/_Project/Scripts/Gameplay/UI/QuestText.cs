using System.Text;

namespace Beast.Gameplay
{
    /// <summary>Shared quest formatting for the prototype quest UIs.</summary>
    public static class QuestText
    {
        public static string Rewards(QuestData quest)
        {
            var sb = new StringBuilder();
            if (quest.GoldReward > 0) Append(sb, $"{quest.GoldReward} gold");
            if (quest.ItemRewards != null)
                foreach (var reward in quest.ItemRewards)
                    if (reward.Item != null) Append(sb, $"{reward.Count}× {reward.Item.DisplayName}");
            if (quest.CombatXp > 0) Append(sb, $"{quest.CombatXp} Combat XP");
            if (quest.FarmingXp > 0) Append(sb, $"{quest.FarmingXp} Farming XP");
            if (quest.StandingReward > 0) Append(sb, $"+{quest.StandingReward} standing");
            return sb.Length > 0 ? sb.ToString() : "—";
        }

        /// <summary>The objective list, ticked ones in 'doneHex' (default: the dark theme's green).</summary>
        public static string Objectives(QuestData quest, QuestLog log, string doneHex = null)
        {
            doneHex ??= UITheme.GoodHex;
            var sb = new StringBuilder();
            if (quest.Objectives == null) return string.Empty;
            bool finished = log.StatusOf(quest) == QuestStatus.Completed;
            for (int i = 0; i < quest.Objectives.Length; i++)
            {
                var objective = quest.Objectives[i];
                bool done = finished || log.IsObjectiveDone(quest, i);
                int progress = finished ? objective.Count : log.ObjectiveProgress(quest, i);
                string count = objective.Count > 1 ? $" ({progress}/{objective.Count})" : string.Empty;
                sb.Append(done ? $"<color={doneHex}>■ " : "□ ").Append(objective.Text).Append(count).Append(done ? "</color>" : "").Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>Total progress across all objectives, e.g. (4, 6) for "2/3 bandits + 2/3 turnips".</summary>
        public static (int done, int total) Progress(QuestData quest, QuestLog log)
        {
            int done = 0, total = 0;
            if (quest.Objectives == null) return (0, 0);
            bool finished = log.StatusOf(quest) == QuestStatus.Completed;
            for (int i = 0; i < quest.Objectives.Length; i++)
            {
                total += quest.Objectives[i].Count;
                done += finished ? quest.Objectives[i].Count : log.ObjectiveProgress(quest, i);
            }
            return (done, total);
        }

        public static string TypeColor(QuestType type) => type switch
        {
            QuestType.Story => UITheme.GoldHex,
            QuestType.Contract => UITheme.InfoHex,
            _ => UITheme.TextHex,
        };

        /// <summary>Quest-type colour readable on parchment (story gold-dark, contracts blue, side ink).</summary>
        public static string TypeColorOnPaper(QuestType type) => type switch
        {
            QuestType.Story => UITheme.InkGoldDarkHex,
            QuestType.Contract => UITheme.InfoOnPaperHex,
            _ => UITheme.InkHex,
        };

        /// <summary>Quest-type colour readable on ink panels.</summary>
        public static string TypeColorOnInk(QuestType type) => type switch
        {
            QuestType.Story => UITheme.InkGoldHex,
            QuestType.Contract => UITheme.InfoOnInkHex,
            _ => UITheme.OffWhiteHex,
        };

        static void Append(StringBuilder sb, string text)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(text);
        }
    }
}
