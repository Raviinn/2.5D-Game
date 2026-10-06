using Beast.Core;

namespace Beast.Gameplay
{
    public readonly struct QuestStartedEvent : IEvent
    {
        public readonly QuestData Quest;
        public QuestStartedEvent(QuestData quest) { Quest = quest; }
    }

    /// <summary>Raised when any objective's progress changes.</summary>
    public readonly struct QuestProgressEvent : IEvent
    {
        public readonly QuestData Quest;
        public QuestProgressEvent(QuestData quest) { Quest = quest; }
    }

    /// <summary>Raised once when all of a quest's objectives become done (before turn-in).</summary>
    public readonly struct QuestReadyEvent : IEvent
    {
        public readonly QuestData Quest;
        public QuestReadyEvent(QuestData quest) { Quest = quest; }
    }

    public readonly struct QuestAbandonedEvent : IEvent
    {
        public readonly QuestData Quest;
        public QuestAbandonedEvent(QuestData quest) { Quest = quest; }
    }

    /// <summary>The tracked (focused) quest changed. Quest can be null when nothing is active.</summary>
    public readonly struct QuestFocusChangedEvent : IEvent
    {
        public readonly QuestData Quest;
        public QuestFocusChangedEvent(QuestData quest) { Quest = quest; }
    }

    /// <summary>Raised on turn-in. Reputation listens (StandingReward).</summary>
    public readonly struct QuestCompletedEvent : IEvent
    {
        public readonly QuestData Quest;
        public QuestCompletedEvent(QuestData quest) { Quest = quest; }
    }
}
