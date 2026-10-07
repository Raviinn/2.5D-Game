using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Draws the player's sprite sheet from their chosen look (character creator) and the weapon they're using:
    /// sword and shield, or a two-handed greatsword. The sheet is redrawn whenever either changes.
    /// Only takes over while the Visual still uses a placeholder-sized sheet: real art (any other size) is left alone.
    /// Saved with the game.
    /// </summary>
    public sealed class PlayerAppearance : MonoBehaviour, ISaveable
    {
        /// <summary>The look picked in the character creator, waiting for the new game's player to spawn.</summary>
        public static CharacterAppearance Pending;

        [SerializeField] string saveId = "player.appearance";
        [SerializeField, Tooltip("Found in children when empty.")] DirectionalSpriteRenderer spriteRenderer;

        CharacterAppearance appearance = new();
        PlayerCombat combat;
        readonly Dictionary<WeaponLook, Texture2D> sheets = new(); // one per weapon look, for instant swaps
        bool canDraw;

        public string SaveId => saveId;
        public CharacterAppearance Appearance => appearance.Clone();
        /// <summary>The weapon look currently shown.</summary>
        public WeaponLook ShownLook { get; private set; }
        public Texture2D CurrentSheet => sheets.TryGetValue(ShownLook, out var sheet) ? sheet : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Pending = null;

        void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<DirectionalSpriteRenderer>();
            if (Pending != null)
            {
                appearance = Pending.Clone();
                appearance.Clamp();
                Pending = null;
            }

            var sheet = spriteRenderer != null ? spriteRenderer.Sheet : null;
            canDraw = sheet != null && sheet.Texture != null &&
                      sheet.Texture.width == CharacterSpriteBuilder.Columns * CharacterSpriteBuilder.CellWidth &&
                      sheet.Texture.height == CharacterSpriteBuilder.Rows * CharacterSpriteBuilder.CellHeight;
        }

        void OnEnable()
        {
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start() => Show(CurrentLook(), force: true);

        void LateUpdate()
        {
            var look = CurrentLook();
            if (look != ShownLook) Show(look, force: false);
        }

        void OnDestroy() => ClearSheets();

        public void SetAppearance(CharacterAppearance next)
        {
            if (next == null || next.SameAs(appearance)) return;
            appearance = next.Clone();
            appearance.Clamp();
            ClearSheets();
            Show(CurrentLook(), force: true);
        }

        WeaponLook CurrentLook() => combat != null && combat.Moveset != null ? combat.Moveset.ResolvedLook : WeaponLook.SwordAndShield;

        void Show(WeaponLook look, bool force)
        {
            if (!canDraw) return;
            if (!force && look == ShownLook && sheets.ContainsKey(look)) return;
            ShownLook = look;
            if (!sheets.TryGetValue(look, out var sheet))
            {
                sheet = CharacterSpriteBuilder.Build(appearance.ToPalette(look));
                sheets[look] = sheet;
            }
            spriteRenderer.SetTextureOverride(sheet);
        }

        void ClearSheets()
        {
            if (spriteRenderer != null) spriteRenderer.SetTextureOverride(null);
            foreach (var sheet in sheets.Values)
                if (sheet != null) Destroy(sheet);
            sheets.Clear();
        }

        public string CaptureState() => JsonUtility.ToJson(appearance);

        public void RestoreState(string json)
        {
            var loaded = new CharacterAppearance();
            JsonUtility.FromJsonOverwrite(json, loaded);
            SetAppearance(loaded);
        }
    }
}
