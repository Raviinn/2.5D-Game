using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>The tabs of the game menu, in top-bar order.</summary>
    public enum GameMenuTab
    {
        Map,
        Journal,
        Bag,
        Character,
        Options,
    }

    /// <summary>A screen shown as one tab of the game menu (GameMenu draws the frame: top bar, footer, key hints).</summary>
    public interface IGameMenuTab
    {
        /// <summary>False while the tab has nothing to show (e.g. no player in the scene).</summary>
        bool CanOpen { get; }

        /// <summary>Called each time the tab becomes the visible one.</summary>
        void OnTabOpened();

        /// <summary>Draws the tab's content inside the shell (call inside OnGUI, after UITheme.Begin).</summary>
        void DrawTab(Rect content);

        /// <summary>One line for the footer strip (rich text).</summary>
        string FooterTip { get; }

        /// <summary>The tab's own key hints, shown left of the shell's "[Q / E] Switch  [Esc] Close".</summary>
        (string key, string label)[] KeyHints { get; }

        /// <summary>True while a sub-view (a settings category, a confirm) is open: Esc closes that first.</summary>
        bool HasSubView { get; }

        void CloseSubView();
    }
}
