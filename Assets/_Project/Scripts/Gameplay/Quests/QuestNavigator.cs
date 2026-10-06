using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Works out where a quest's waypoint should point, from what's in the world right now:
    /// ready to hand in → the NPC / board that takes it; Kill → nearest matching enemy;
    /// Harvest or a crop to collect → the field; loot to collect → nearest enemy that drops it;
    /// Custom (talk) objectives → the NPC the quest is handed in to.
    /// </summary>
    public static class QuestNavigator
    {
        public static bool TryGetTarget(QuestData quest, QuestLog log, Vector3 from, out Vector3 position, out string label)
        {
            position = default;
            label = null;
            if (quest == null || log == null) return false;

            if (log.IsReady(quest)) return TryGetTurnIn(quest, from, out position, out label);

            int index = FirstUnfinishedObjective(quest, log);
            if (index < 0) return TryGetTurnIn(quest, from, out position, out label);
            var objective = quest.Objectives[index];

            switch (objective.Type)
            {
                case ObjectiveType.Kill:
                    if (TryNearestEnemy(from, e => objective.Enemy == null || e.Data == objective.Enemy, out position))
                    {
                        label = objective.Enemy != null ? objective.Enemy.DisplayName : "Enemy";
                        return true;
                    }
                    break;

                case ObjectiveType.Harvest:
                    if (TryNearestField(from, out position)) { label = "Field"; return true; }
                    break;

                case ObjectiveType.Collect:
                    if (CropCatalog.IsProduce(objective.Item) && TryNearestField(from, out position)) { label = "Field"; return true; }
                    if (TryNearestEnemy(from, e => Drops(e, objective.Item), out position))
                    {
                        label = $"{objective.Item.DisplayName} (loot)";
                        return true;
                    }
                    break;
            }

            // Custom objectives, or nothing found in the world: point at whoever the quest belongs to.
            return TryGetTurnIn(quest, from, out position, out label);
        }

        static int FirstUnfinishedObjective(QuestData quest, QuestLog log)
        {
            if (quest.Objectives == null) return -1;
            for (int i = 0; i < quest.Objectives.Length; i++)
                if (!log.IsObjectiveDone(quest, i)) return i;
            return -1;
        }

        static bool TryGetTurnIn(QuestData quest, Vector3 from, out Vector3 position, out string label)
        {
            position = default;
            label = null;
            float best = float.MaxValue;
            foreach (var interactable in Interactable.Active)
            {
                string name = interactable switch
                {
                    DialogueSpeaker speaker when speaker.TurnsIn(quest) => speaker.DisplayName,
                    ContractBoard board when board.Posts(quest) => board.BoardName,
                    _ => null,
                };
                if (name == null) continue;

                float distance = (interactable.transform.position - from).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                position = interactable.transform.position;
                label = name;
            }
            return label != null;
        }

        static bool TryNearestEnemy(Vector3 from, System.Func<EnemyController, bool> matches, out Vector3 position)
        {
            position = default;
            float best = float.MaxValue;
            bool found = false;
            foreach (var enemy in EnemyController.Active)
            {
                if (enemy.Combatant.IsDead || enemy.Data == null || !matches(enemy)) continue;
                float distance = (enemy.transform.position - from).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                position = enemy.transform.position;
                found = true;
            }
            return found;
        }

        static bool TryNearestField(Vector3 from, out Vector3 position)
        {
            position = default;
            float best = float.MaxValue;
            bool found = false;
            foreach (var interactable in Interactable.Active)
            {
                if (interactable is not FarmPlot field) continue;
                Vector3 center = field.WorldCenter;
                float distance = (center - from).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                position = center;
                found = true;
            }
            return found;
        }

        static bool Drops(EnemyController enemy, ItemData item)
        {
            var loot = enemy.Data.Loot;
            if (loot == null || loot.Entries == null || item == null) return false;
            foreach (var entry in loot.Entries)
                if (entry.Item == item) return true;
            return false;
        }
    }
}
