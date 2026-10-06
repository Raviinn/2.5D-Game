namespace Beast.Core
{
    public readonly struct GameStateChangedEvent : IEvent
    {
        public readonly GameState Previous;
        public readonly GameState Current;
        public GameStateChangedEvent(GameState previous, GameState current) { Previous = previous; Current = current; }
    }

    public readonly struct HourChangedEvent : IEvent
    {
        public readonly int Day;
        public readonly int Hour;
        public HourChangedEvent(int day, int hour) { Day = day; Hour = hour; }
    }

    /// <summary>Raised once per new in-game day. Farming, economy and NPC schedules tick on this.</summary>
    public readonly struct DayPassedEvent : IEvent
    {
        public readonly int Day;
        public DayPassedEvent(int day) { Day = day; }
    }

    public readonly struct SceneLoadedEvent : IEvent
    {
        public readonly string SceneName;
        public SceneLoadedEvent(string sceneName) { SceneName = sceneName; }
    }

    public readonly struct GameSavedEvent : IEvent
    {
        public readonly int Slot;
        public GameSavedEvent(int slot) { Slot = slot; }
    }

    public readonly struct GameLoadedEvent : IEvent
    {
        public readonly int Slot;
        public GameLoadedEvent(int slot) { Slot = slot; }
    }

    /// <summary>
    /// Raised just before a new game's world loads. Persistent services (the clock...) reset to a fresh start;
    /// scene objects don't need to, they're created fresh.
    /// </summary>
    public readonly struct NewGameEvent : IEvent
    {
        public readonly int Slot;
        public NewGameEvent(int slot) { Slot = slot; }
    }
}
