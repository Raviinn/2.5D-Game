using Beast.Core;

namespace Beast.Gameplay
{
    /// <summary>Raised when the player harvests a crop. Farming XP, quests and stats listen.</summary>
    public readonly struct CropHarvestedEvent : IEvent
    {
        public readonly CropData Crop;
        public readonly int Count;
        public CropHarvestedEvent(CropData crop, int count) { Crop = crop; Count = count; }
    }

    public enum FarmAction { Till, Plant, Clear, Harvest, Water }

    /// <summary>Raised when the player works a farm tile (till, plant, clear, harvest, water).</summary>
    public readonly struct FarmActionEvent : IEvent
    {
        public readonly FarmAction Action;
        public readonly UnityEngine.Vector3 Position;
        public FarmActionEvent(FarmAction action, UnityEngine.Vector3 position) { Action = action; Position = position; }
    }

    /// <summary>Raised after the player sleeps (the HUD fades to black and shows the new day).</summary>
    public readonly struct SleptEvent : IEvent
    {
        public readonly int Day;
        public readonly int Hour;
        public readonly bool Saved;
        public SleptEvent(int day, int hour, bool saved) { Day = day; Hour = hour; Saved = saved; }
    }
}
