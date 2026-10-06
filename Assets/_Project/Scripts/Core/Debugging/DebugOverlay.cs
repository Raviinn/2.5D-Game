using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Core
{
    /// <summary>
    /// Development-only info line and hotkeys. Added by the Bootstrapper in the Editor and Development builds.
    /// F1 +1 day · F2 +1 hour · F3 toggle overlay · F4 weather (WeatherSystem) · F5 save · F9 load (both use the
    /// active save slot, like the pause menu). Inactive on the main menu.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        GameStateService state;
        WorldClock clock;
        SaveService save;
        bool visible = true;
        GUIStyle style;

        public void Initialize(GameStateService state, WorldClock clock, SaveService save)
        {
            this.state = state;
            this.clock = clock;
            this.save = save;
        }

        bool InGame => state.Current is not (GameState.Boot or GameState.MainMenu or GameState.Loading);

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !InGame) return;

            if (keyboard.f1Key.wasPressedThisFrame) clock.AdvanceMinutes(WorldClock.MinutesPerDay);
            if (keyboard.f2Key.wasPressedThisFrame) clock.AdvanceMinutes(60);
            if (keyboard.f3Key.wasPressedThisFrame) visible = !visible;
            if (keyboard.f5Key.wasPressedThisFrame) save.Save();
            if (keyboard.f9Key.wasPressedThisFrame) save.Load();
        }

        void OnGUI()
        {
            if (!visible || !InGame) return;

            // Small and out of the way (bottom-left, above the HUD's key hints); same 1080p virtual scale as the HUD.
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontSize = 12, richText = true };
            float scale = Mathf.Max(0.5f, Screen.height / 1080f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float height = Screen.height / scale;
            GUI.Label(new Rect(18f, height - 92f, 640f, 36f),
                $"<color=#9a9a9a>DEV  {state.Current} · Day {clock.Day} {clock.Hour:00}:{clock.Minute:00} · slot {save.ActiveSlot + 1}\n" +
                "F1 +day · F2 +hour · F4 weather · F5 save · F9 load · F6/F7 +XP · F8 +standing · F3 hide</color>",
                style);
            GUI.matrix = oldMatrix;
        }
    }
}
