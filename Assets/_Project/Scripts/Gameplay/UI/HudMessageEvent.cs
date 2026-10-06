using Beast.Core;

namespace Beast.Gameplay
{
    /// <summary>A short notice for the HUD's notification feed (e.g. "Bag is full", "Game saved").</summary>
    public readonly struct HudMessageEvent : IEvent
    {
        public readonly string Text;
        public HudMessageEvent(string text) { Text = text; }
    }
}
