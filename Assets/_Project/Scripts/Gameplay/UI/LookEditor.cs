using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The character look editor shared by the main menu's creator and the homestead mirror: a turnable preview of the
    /// real in-game sprite on an ink stage, and parchment rows for name, hair, hair colour, skin and outfit.
    /// IMGUI; call from OnGUI. Owns one preview texture: call Dispose when done.
    /// </summary>
    public sealed class LookEditor
    {
        static readonly string[] WeaponNames = { "Sword & Shield", "Greatsword" };
        const float LabelWidth = 160f;
        const float RowGap = 74f;
        const string NameControl = "HeroName";

        public CharacterAppearance Look = new();
        public WeaponLook PreviewWeapon;
        int direction;
        Texture2D sheet;
        CharacterAppearance sheetLook;
        WeaponLook sheetWeapon;
        GUIStyle label, centred, nameField, namePlaceholder;

        /// <summary>The preview's sprite sheet (built on the first draw).</summary>
        public Texture2D PreviewSheet => sheet;
        /// <summary>Which way the preview faces: 0 = toward you, then round in eighths.</summary>
        public int Direction
        {
            get => direction;
            set => direction = (value % 8 + 8) % 8;
        }

        /// <summary>Starts editing a copy of this look, facing forward.</summary>
        public void Begin(CharacterAppearance look, WeaponLook weapon)
        {
            Look = (look ?? new CharacterAppearance()).Clone();
            PreviewWeapon = weapon;
            direction = 0;
        }

        public void Dispose()
        {
            if (sheet != null) Object.Destroy(sheet);
            sheet = null;
            sheetLook = null;
        }

        /// <summary>The ink stage with the idling figure and turn buttons.</summary>
        public void DrawStage(Rect stage)
        {
            EnsureStyles();
            UITheme.InkPanel(stage);
            DrawPreview(new Rect(stage.center.x - 120f, stage.y + 26f, 240f, 320f));
            if (UITheme.BrushButton(new Rect(stage.x + 18f, stage.yMax - 60f, 64f, 42f), "‹", light: true)) direction = (direction + 1) % 8;
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(stage.x + 94f, stage.yMax - 60f, stage.width - 188f, 42f), UITheme.Spaced("Turn"), centred);
            GUI.color = oldColor;
            if (UITheme.BrushButton(new Rect(stage.xMax - 82f, stage.yMax - 60f, 64f, 42f), "›", light: true)) direction = (direction + 7) % 8;
        }

        /// <summary>The rows, from the top of 'area'. Returns the y below the last row.</summary>
        public float DrawFields(Rect area, bool weaponPreview)
        {
            EnsureStyles();
            float x = area.x;
            float fieldX = x + LabelWidth;
            float fieldWidth = area.xMax - fieldX;
            float y = area.y;

            Label(x, y, "Name");
            var field = new Rect(fieldX, y, fieldWidth - 150f, 44f);
            if (UITheme.BrushButton(new Rect(field.xMax + 10f, y, 140f, 44f), "Random")) Look.name = CharacterAppearance.RandomName(Look.name);
            UITheme.PaperSlot(field, GUI.GetNameOfFocusedControl() == NameControl, field.Contains(Event.current.mousePosition));
            GUI.SetNextControlName(NameControl);
            string typed = GUI.TextField(new Rect(field.x + 4f, field.y, field.width - 8f, field.height), Look.name ?? string.Empty, CharacterAppearance.MaxNameLength, nameField);
            Look.name = CharacterAppearance.CleanName(typed, trim: false);
            if (string.IsNullOrEmpty(Look.name) && GUI.GetNameOfFocusedControl() != NameControl)
                GUI.Label(new Rect(field.x + 4f, field.y, field.width - 8f, field.height), $"{CharacterAppearance.DefaultName} (type a name)", namePlaceholder);
            y += RowGap - 8f;

            Label(x, y, "Hair");
            Look.hairStyle = UITheme.PaperStepper(new Rect(fieldX, y, fieldWidth, 44f), Look.hairStyle, CharacterAppearance.HairStyleNames.Length,
                CharacterAppearance.HairStyleNames[Look.hairStyle]);
            y += RowGap - 8f;

            Label(x, y, "Hair colour");
            Look.hairColor = Swatches(new Rect(fieldX, y, fieldWidth, 40f), Look.hairColor, CharacterAppearance.HairColors);
            GUI.Label(new Rect(fieldX, y + 42f, fieldWidth, 22f), CharacterAppearance.HairColorNames[Look.hairColor], UITheme.PaperMuted);
            y += RowGap;

            Label(x, y, "Skin");
            Look.skinTone = Swatches(new Rect(fieldX, y, fieldWidth, 40f), Look.skinTone, CharacterAppearance.SkinTones);
            GUI.Label(new Rect(fieldX, y + 42f, fieldWidth, 22f), CharacterAppearance.SkinToneNames[Look.skinTone], UITheme.PaperMuted);
            y += RowGap;

            Label(x, y, "Outfit");
            Look.outfit = UITheme.PaperStepper(new Rect(fieldX, y, fieldWidth, 44f), Look.outfit, CharacterAppearance.Outfits.Length,
                CharacterAppearance.Outfits[Look.outfit].Name);
            y += RowGap - 8f;

            if (weaponPreview)
            {
                Label(x, y, "Preview with");
                PreviewWeapon = (WeaponLook)UITheme.PaperOptions(new Rect(fieldX, y, fieldWidth, 44f), (int)PreviewWeapon, WeaponNames);
                y += 50f;
            }
            return y;
        }

        /// <summary>Random hair, colours and outfit (the name is kept).</summary>
        public void Randomise()
        {
            string name = Look.name;
            Look = CharacterAppearance.Random();
            Look.name = name;
        }

        void Label(float x, float y, string text) => GUI.Label(new Rect(x, y, LabelWidth, 44f), text, label);

        /// <summary>A row of colour squares on parchment; the chosen one has a vermilion frame. Returns the chosen index.</summary>
        static int Swatches(Rect rect, int selected, Color32[] colors)
        {
            float size = Mathf.Min(rect.height, (rect.width - 10f * (colors.Length - 1)) / colors.Length);
            for (int i = 0; i < colors.Length; i++)
            {
                var cell = new Rect(rect.x + i * (size + 10f), rect.y, size, size);
                bool chosen = i == selected;
                UITheme.Fill(cell, chosen ? UITheme.Vermilion : UITheme.Ink);
                float inset = chosen ? 4f : 1.5f;
                UITheme.Fill(new Rect(cell.x + inset, cell.y + inset, cell.width - inset * 2f, cell.height - inset * 2f), colors[i]);
                if (UITheme.PaperClick(cell)) selected = i;
            }
            return selected;
        }

        /// <summary>One frame of the idle clip, facing 'direction' (0 = toward you), from a sheet built for the current look.</summary>
        void DrawPreview(Rect rect)
        {
            if (sheet == null || sheetLook == null || !sheetLook.SameAs(Look) || sheetWeapon != PreviewWeapon)
            {
                if (sheet != null) Object.Destroy(sheet);
                sheet = CharacterSpriteBuilder.Build(Look.ToPalette(PreviewWeapon));
                sheetLook = Look.Clone();
                sheetWeapon = PreviewWeapon;
            }

            var idle = CharacterSpriteBuilder.Layout[0];
            int frame = idle.Start + (int)(Time.unscaledTime * idle.Fps) % idle.Count;
            int row = direction > 4 ? 8 - direction : direction;
            bool flip = direction > 4;

            float w = sheet.width, h = sheet.height;
            float cellW = CharacterSpriteBuilder.CellWidth / w, cellH = CharacterSpriteBuilder.CellHeight / h;
            float u = frame * cellW;
            float v = (h - (row + 1) * CharacterSpriteBuilder.CellHeight) / h;
            var uv = flip ? new Rect(u + cellW, v, -cellW, cellH) : new Rect(u, v, cellW, cellH);

            // A soft pool of light under the feet.
            UITheme.DrawIcon(new Rect(rect.center.x - 80f, rect.yMax - 24f, 160f, 30f), UITheme.CircleIcon, new Color(1f, 1f, 1f, 0.08f));
            GUI.DrawTextureWithTexCoords(rect, sheet, uv);
        }

        void EnsureStyles()
        {
            if (label != null && label.font == UITheme.PaperBody.font) return;
            label = new GUIStyle(UITheme.PaperBody) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            centred = new GUIStyle(UITheme.InkBody) { alignment = TextAnchor.MiddleCenter };
            namePlaceholder = new GUIStyle(UITheme.PaperMuted) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontStyle = FontStyle.Italic, padding = new RectOffset(12, 12, 0, 0) };
            nameField = new GUIStyle(UITheme.PaperBody)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                richText = false,
                padding = new RectOffset(12, 12, 0, 0),
                normal = { background = null, textColor = UITheme.Ink },
                focused = { background = null, textColor = UITheme.Ink },
                hover = { background = null, textColor = UITheme.Ink },
                active = { background = null, textColor = UITheme.Ink },
            };
        }
    }
}
