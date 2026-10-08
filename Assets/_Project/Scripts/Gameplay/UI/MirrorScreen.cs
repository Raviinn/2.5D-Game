using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The mirror window: the character creator's controls (name, hair, colours, outfit) on the player's current look,
    /// with a turnable preview holding the weapon in use. "Keep this look" applies it; Cancel or Esc leaves it as it was.
    /// Menu hotkeys (I, C, J, Tab) don't close it, so they can be typed in the name.
    /// </summary>
    public sealed class MirrorScreen : MonoBehaviour
    {
        readonly LookEditor editor = new();
        Mirror mirror;
        PlayerAppearance appearance;
        GameStateService state;

        /// <summary>The mirror whose window is open (null when closed).</summary>
        public Mirror Mirror => mirror;
        /// <summary>The look being edited (test hook).</summary>
        public CharacterAppearance Editing => editor.Look;

        void OnEnable()
        {
            EventBus<MirrorOpenedEvent>.Subscribe(OnOpened);
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus<MirrorOpenedEvent>.Unsubscribe(OnOpened);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) appearance = player.GetComponent<PlayerAppearance>();
            state = Services.Get<GameStateService>();
        }

        void OnDestroy() => editor.Dispose();

        void OnOpened(MirrorOpenedEvent evt)
        {
            if (appearance == null) return;
            mirror = evt.Mirror;
            editor.Begin(appearance.Appearance, appearance.ShownLook);
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) mirror = null;
        }

        void Update()
        {
            if (mirror != null && state != null && state.Current == GameState.InGameMenu) state.BlockHotkeyClose = true;
        }

        /// <summary>Applies the edited look and name, and closes the window.</summary>
        public void Keep()
        {
            if (mirror == null) return;
            string before = appearance.PlayerName;
            appearance.SetAppearance(editor.Look);
            string after = appearance.PlayerName;
            state.SetState(GameState.Playing);
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent(after != before ? $"You are now {after}." : "A fresh look."));
        }

        void OnGUI()
        {
            if (mirror == null || appearance == null || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();

            var content = UITheme.ParchmentWindow(1000f, 640f, "The mirror", "Change your name, hair and clothes");
            editor.DrawStage(new Rect(content.x, content.y, 320f, content.height - 70f));
            editor.DrawFields(new Rect(content.x + 352f, content.y + 4f, content.width - 352f, content.height - 70f), weaponPreview: false);

            float footerY = content.yMax - 48f;
            if (UITheme.BrushButton(new Rect(content.x, footerY, 170f, 48f), "Cancel")) state.SetState(GameState.Playing);
            if (UITheme.BrushButton(new Rect(content.x + 182f, footerY, 220f, 48f), "Randomise")) editor.Randomise();
            if (UITheme.BrushButton(new Rect(content.xMax - 300f, footerY, 300f, 48f), "Keep this look")) Keep();
        }
    }
}
