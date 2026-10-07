using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The "ink and parchment" half of the theme (UI restyle after Ghost of Tsushima):
    /// - parchment menus with a soft ink-wash edge, ink strips with dry-brush ends, one vermilion for selection
    /// - Cormorant Garamond (display, spaced capitals) and Alegreya Sans (body), from Resources/UI/Fonts
    /// - widgets: brush buttons, paper cards, ink panels, the title swash, key hints, footer tips, parchment windows
    /// All textures are drawn in code from a fixed seed, so they look the same every run. Screens move from the dark
    /// widgets to these one at a time (see Docs/superpowers/plans/2026-10-07-tsushima-ui.md).
    /// </summary>
    public static partial class UITheme
    {
        // ---------- Palette ----------

        public static readonly Color Parchment = Hex(0xE9E6DF);
        public static readonly Color Ink = Hex(0x141414);
        public static readonly Color Paper = Hex(0xF4F2EE);
        public static readonly Color Vermilion = Hex(0xD9534F);
        public static readonly Color InkGold = Hex(0xC9A86A);
        public static readonly Color InkGoldDark = Hex(0x8A6A2C);
        public static readonly Color OffWhite = Hex(0xF2EFE8);
        public static readonly Color MutedOnPaper = Hex(0x55514A);
        public static readonly Color MutedOnInk = Hex(0xBDB7AA);
        static readonly Color PaperLine = Hex(0xC9C4BA);

        public const string InkHex = "#141414";
        public const string VermilionHex = "#d9534f";
        public const string InkGoldHex = "#c9a86a";
        public const string InkGoldDarkHex = "#8a6a2c";
        public const string OffWhiteHex = "#f2efe8";
        public const string MutedOnPaperHex = "#55514a";
        public const string MutedOnInkHex = "#bdb7aa";
        /// <summary>"Good" (done, learned, bonuses) readable on parchment and on ink.</summary>
        public const string GoodOnPaperHex = "#3f6e33";
        public const string GoodOnInkHex = "#9cc98a";
        /// <summary>Contracts' blue, readable on parchment and on ink.</summary>
        public const string InfoOnPaperHex = "#2f5f8a";
        public const string InfoOnInkHex = "#8fbdee";

        // ---------- Fonts ----------

        static Font bodyFont, bodyBoldFont, displayFont;

        /// <summary>
        /// Alegreya Sans draws smaller than the default font at the same point size, so body sizes are scaled up
        /// (the layouts were tuned with the old font).
        /// </summary>
        const float BodyFontScale = 1.15f;

        static int Sized(Font font, int size) =>
            font != null && (font == bodyFont || font == bodyBoldFont) && FontsLoaded ? Mathf.RoundToInt(size * BodyFontScale) : size;

        /// <summary>True when the bundled fonts were found (otherwise every style uses Unity's default font).</summary>
        public static bool FontsLoaded { get; private set; }

        static void LoadFonts()
        {
            var fallback = GUI.skin.label.font;
            var regular = Resources.Load<Font>("UI/Fonts/AlegreyaSans-Regular");
            var bold = Resources.Load<Font>("UI/Fonts/AlegreyaSans-Bold");
            var display = Resources.Load<Font>("UI/Fonts/CormorantGaramond-SemiBold");
            FontsLoaded = regular != null && bold != null && display != null;
            bodyFont = regular != null ? regular : fallback;
            bodyBoldFont = bold != null ? bold : bodyFont;
            displayFont = display != null ? display : bodyBoldFont;
        }

        // ---------- Styles ----------

        public static GUIStyle DisplayTitle { get; private set; }
        public static GUIStyle TabLabel { get; private set; }
        public static GUIStyle MenuItem { get; private set; }
        public static GUIStyle InkHeader { get; private set; }
        public static GUIStyle PaperBody { get; private set; }
        public static GUIStyle PaperMuted { get; private set; }
        public static GUIStyle InkBody { get; private set; }
        public static GUIStyle InkSmall { get; private set; }
        public static GUIStyle BrushButtonStyle { get; private set; }
        static GUIStyle keyHintKey, keyHintLabel, paperOption, cardTitle, cardCount, cardSubtitle;

        // ---------- Textures ----------

        static Texture2D parchmentTex, inkStripTex, vermilionStripTex, lightStripTex, redBlockTex, inkBlockTex, lightBlockTex,
            swashTex, paperCardTex, inkPanelTex;
        static readonly RectOffset StripBorder = new(28, 28, 0, 0);
        static readonly RectOffset BlockBorder = new(12, 12, 12, 12);
        static readonly RectOffset SwashBorder = new(40, 90, 0, 0);
        static readonly RectOffset CardBorder = new(8, 8, 8, 8);

        /// <summary>Test hook: every generated ink texture (null entries mean something failed to build).</summary>
        public static Texture2D[] InkTextures => new[]
            { parchmentTex, inkStripTex, vermilionStripTex, lightStripTex, redBlockTex, inkBlockTex, lightBlockTex, swashTex, paperCardTex, inkPanelTex };

        static void BuildInk()
        {
            var random = new System.Random(7);
            parchmentTex = ParchmentTexture(256, random);
            inkStripTex = BrushStrip(256, 48, 22, Ink, random);
            vermilionStripTex = BrushStrip(256, 48, 22, Vermilion, random);
            lightStripTex = BrushStrip(256, 48, 22, OffWhite, random);
            redBlockTex = WobblyBlock(128, 3f, Vermilion, random);
            inkBlockTex = WobblyBlock(128, 3f, Ink, random);
            lightBlockTex = WobblyBlock(128, 3f, OffWhite, random);
            swashTex = SwashTexture(512, 64, OffWhite, random);
            paperCardTex = FlatCard(64, Paper, PaperLine);
            inkPanelTex = FlatCard(64, new Color(Ink.r, Ink.g, Ink.b, 0.96f), new Color(0.24f, 0.23f, 0.22f, 0.96f));

            DisplayTitle = Styled(displayFont, 64, OffWhite, TextAnchor.MiddleCenter, wrap: false);
            TabLabel = Styled(displayFont, 20, OffWhite, TextAnchor.MiddleCenter, wrap: false);
            MenuItem = Styled(displayFont, 28, OffWhite, TextAnchor.MiddleLeft, wrap: false);
            InkHeader = Styled(displayFont, 24, Ink, TextAnchor.MiddleLeft, wrap: false);
            PaperBody = Styled(bodyFont, 17, Ink, TextAnchor.UpperLeft, wrap: true);
            PaperMuted = Styled(bodyFont, 15, MutedOnPaper, TextAnchor.UpperLeft, wrap: true);
            InkBody = Styled(bodyFont, 17, OffWhite, TextAnchor.UpperLeft, wrap: true);
            InkSmall = Styled(bodyFont, 15, OffWhite, TextAnchor.MiddleLeft, wrap: false);
            BrushButtonStyle = Styled(displayFont, 18, OffWhite, TextAnchor.MiddleCenter, wrap: false);
            keyHintKey = Styled(bodyBoldFont, 15, Ink, TextAnchor.MiddleLeft, wrap: false);
            keyHintLabel = Styled(bodyFont, 15, Ink, TextAnchor.MiddleLeft, wrap: false);
            paperOption = Styled(bodyFont, 17, Ink, TextAnchor.MiddleCenter, wrap: false);
            cardTitle = Styled(displayFont, 19, Ink, TextAnchor.MiddleLeft, wrap: false);
            cardCount = Styled(bodyFont, 16, MutedOnPaper, TextAnchor.MiddleRight, wrap: false);
            cardSubtitle = Styled(bodyFont, 15, MutedOnPaper, TextAnchor.UpperLeft, wrap: true);
            spacedCache.Clear();
        }

        static GUIStyle Styled(Font font, int size, Color color, TextAnchor anchor, bool wrap) => new()
        {
            font = font,
            fontSize = Sized(font, size),
            richText = true,
            wordWrap = wrap,
            alignment = anchor,
            normal = { textColor = color },
            hover = { textColor = color },
        };

        // ---------- Text ----------

        static readonly Dictionary<string, string> spacedCache = new();

        /// <summary>
        /// Display capitals with letter-spacing (IMGUI has none): "New game" → "N E W   G A M E" with thin spaces.
        /// </summary>
        public static string Spaced(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (spacedCache.TryGetValue(text, out var cached)) return cached;
            var sb = new System.Text.StringBuilder(text.Length * 2);
            string upper = text.ToUpperInvariant();
            for (int i = 0; i < upper.Length; i++)
            {
                char c = upper[i];
                if (c == ' ') { sb.Append("   "); continue; }
                sb.Append(c);
                if (i < upper.Length - 1 && upper[i + 1] != ' ') sb.Append(' ');
            }
            return spacedCache[text] = sb.ToString();
        }

        // ---------- Widgets ----------

        /// <summary>Full parchment with an ink-wash fade toward the edges (menus, windows).</summary>
        public static void ParchmentBackground(Rect rect)
        {
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(rect, parchmentTex, ScaleMode.StretchToFill);
        }

        /// <summary>A card on parchment: paper with a hairline, or a vermilion brush block when selected.</summary>
        public static void PaperCard(Rect rect, bool selected)
        {
            if (selected) SlicedWith(rect, redBlockTex, BlockBorder);
            else SlicedWith(rect, paperCardTex, CardBorder);
        }

        /// <summary>A solid ink panel (details panes on parchment, the creator's preview stage).</summary>
        public static void InkPanel(Rect rect) => SlicedWith(rect, inkPanelTex, CardBorder);

        /// <summary>An ink band with dry-brush ends.</summary>
        public static void InkStrip(Rect rect) => SlicedWith(rect, inkStripTex, StripBorder);

        /// <summary>The white brush swash behind the highlighted title-screen item.</summary>
        public static void Swash(Rect rect) => SlicedWith(rect, swashTex, SwashBorder);

        /// <summary>
        /// An ink brush strip with spaced capitals; vermilion while hovered. Raises Clicked (interface sound).
        /// Narrow buttons (arrows) are a brush-painted block instead, so the ragged ends don't swallow them.
        /// light: an off-white stroke with ink text, for buttons on ink panels. Long labels shrink to fit.
        /// </summary>
        public static bool BrushButton(Rect rect, string text, bool enabled = true, bool light = false)
        {
            bool hovered = enabled && rect.Contains(Event.current.mousePosition);
            bool narrow = rect.width < 90f;
            var old = GUI.color;
            if (!enabled) GUI.color = new Color(old.r, old.g, old.b, old.a * 0.45f);
            if (narrow) SlicedWith(rect, hovered ? redBlockTex : light ? lightBlockTex : inkBlockTex, BlockBorder);
            else SlicedWith(rect, hovered ? vermilionStripTex : light ? lightStripTex : inkStripTex, StripBorder);

            var style = BrushButtonStyle;
            int baseSize = style.fontSize;
            var baseFont = style.font;
            bool glyph = narrow && text.Length <= 1;
            string label = narrow ? text : Spaced(text);
            if (glyph)
            {
                // Arrows and single glyphs: the bold body face, large (the display face's ‹ › are tiny).
                style.font = bodyBoldFont;
                style.fontSize = Mathf.RoundToInt(rect.height * 0.62f);
            }
            else if (narrow)
            {
                // Short words in a small button ("All", "×5"): the body face (Cormorant's old-style numerals read tiny), unspaced.
                style.font = bodyFont;
                style.fontSize = Sized(bodyFont, 18);
                while (style.fontSize > 12 && style.CalcSize(new GUIContent(label)).x > rect.width - 18f) style.fontSize--;
            }
            else
                while (style.fontSize > 12 && style.CalcSize(new GUIContent(label)).x > rect.width - 60f) style.fontSize--;
            style.normal.textColor = light && !hovered ? Ink : OffWhite;
            GUI.Label(rect, label, style);
            style.normal.textColor = OffWhite;
            style.fontSize = baseSize;
            style.font = baseFont;
            GUI.color = old;
            return enabled && Click(GUI.Button(rect, GUIContent.none, GUIStyle.none));
        }

        /// <summary>An invisible click area (for custom-drawn controls) that still plays the click sound.</summary>
        public static bool PaperClick(Rect rect) => Click(GUI.Button(rect, GUIContent.none, GUIStyle.none));

        /// <summary>A row of paper cards, the chosen one a vermilion block (e.g. Sword &amp; Shield | Greatsword). Returns the chosen index.</summary>
        public static int PaperOptions(Rect rect, int selected, string[] options, bool enabled = true)
        {
            const float gap = 8f;
            float width = (rect.width - gap * (options.Length - 1)) / options.Length;
            var old = GUI.color;
            if (!enabled) GUI.color = new Color(old.r, old.g, old.b, old.a * 0.45f);
            for (int i = 0; i < options.Length; i++)
            {
                var cell = new Rect(rect.x + i * (width + gap), rect.y, width, rect.height);
                bool chosen = i == selected;
                bool hovered = enabled && cell.Contains(Event.current.mousePosition);
                PaperCard(cell, chosen);
                if (hovered && !chosen) Fill(new Rect(cell.x, cell.yMax - 3f, cell.width, 3f), Vermilion);
                paperOption.normal.textColor = chosen ? OffWhite : Ink;
                GUI.Label(cell, options[i], paperOption);
                if (enabled && Click(GUI.Button(cell, GUIContent.none, GUIStyle.none))) selected = i;
            }
            GUI.color = old;
            return selected;
        }

        /// <summary>‹ value › on parchment: brush arrows either side of a paper card. Returns the new index.</summary>
        public static int PaperStepper(Rect rect, int index, int count, string text, bool enabled = true)
        {
            const float arrow = 46f;
            if (BrushButton(new Rect(rect.x, rect.y, arrow, rect.height), "‹", enabled && index > 0)) index--;
            var middle = new Rect(rect.x + arrow + 6f, rect.y, rect.width - 2f * (arrow + 6f), rect.height);
            PaperCard(middle, false);
            paperOption.normal.textColor = Ink;
            GUI.Label(middle, text, paperOption);
            if (BrushButton(new Rect(rect.xMax - arrow, rect.y, arrow, rect.height), "›", enabled && index < count - 1)) index++;
            return Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        }

        /// <summary>A slider on parchment: ink track, vermilion fill, ink thumb (click or drag anywhere on it). Returns the new value.</summary>
        public static float PaperSlider(Rect rect, float value, float min, float max, float step = 0f, bool enabled = true)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var evt = Event.current;
            var track = new Rect(rect.x + 8f, rect.center.y - 2f, rect.width - 16f, 4f);
            float t = Mathf.InverseLerp(min, max, value);
            if (enabled)
            {
                switch (evt.GetTypeForControl(id))
                {
                    case EventType.MouseDown when evt.button == 0 && rect.Contains(evt.mousePosition):
                        GUIUtility.hotControl = id;
                        t = Mathf.InverseLerp(track.x, track.xMax, evt.mousePosition.x);
                        GUI.changed = true;
                        evt.Use();
                        break;
                    case EventType.MouseDrag when GUIUtility.hotControl == id:
                        t = Mathf.InverseLerp(track.x, track.xMax, evt.mousePosition.x);
                        GUI.changed = true;
                        evt.Use();
                        break;
                    case EventType.MouseUp when GUIUtility.hotControl == id:
                        GUIUtility.hotControl = 0;
                        evt.Use();
                        break;
                }
            }
            value = Mathf.Lerp(min, max, t);
            if (step > 0f) value = Mathf.Clamp(Mathf.Round((value - min) / step) * step + min, min, max);
            t = Mathf.InverseLerp(min, max, value);

            if (evt.type == EventType.Repaint)
            {
                float alpha = enabled ? 1f : 0.4f;
                Fill(track, new Color(Ink.r, Ink.g, Ink.b, 0.25f * alpha));
                Fill(new Rect(track.x, track.y, track.width * t, track.height), new Color(Vermilion.r, Vermilion.g, Vermilion.b, alpha));
                bool active = GUIUtility.hotControl == id || (enabled && rect.Contains(evt.mousePosition));
                var thumb = new Rect(track.x + track.width * t - 7f, rect.center.y - 12f, 14f, 24f);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                SlicedWith(thumb, active ? redBlockTex : inkBlockTex, BlockBorder);
                GUI.color = old;
            }
            return value;
        }

        /// <summary>A check box on parchment (paper box, vermilion check) with a label to its right. Returns the new value.</summary>
        public static bool PaperToggle(Rect rect, bool value, string label = null)
        {
            var box = new Rect(rect.x, rect.center.y - 13f, 26f, 26f);
            bool hovered = rect.Contains(Event.current.mousePosition);
            PaperCard(box, false);
            if (hovered) Fill(new Rect(box.x, box.yMax - 2f, box.width, 2f), Vermilion);
            if (value) DrawIcon(new Rect(box.x + 4f, box.y + 4f, 18f, 18f), CheckIcon, Vermilion, flipY: true);
            if (!string.IsNullOrEmpty(label))
            {
                paperOption.normal.textColor = Ink;
                paperOption.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(box.xMax + 12f, rect.y, rect.width - 38f, rect.height), label, paperOption);
                paperOption.alignment = TextAnchor.MiddleCenter;
            }
            if (Click(GUI.Button(rect, GUIContent.none, GUIStyle.none))) value = !value;
            return value;
        }

        /// <summary>
        /// "[Esc] Back   [F] Select", right-aligned so its right edge is at rightX. Returns the hints' left edge.
        /// onInk: off-white text for dark backgrounds (the title screen).
        /// </summary>
        public static float KeyHints(float rightX, float y, bool onInk, params (string key, string label)[] hints)
        {
            var color = onInk ? OffWhite : Ink;
            keyHintKey.normal.textColor = color;
            keyHintLabel.normal.textColor = color;
            float x = rightX;
            for (int i = hints.Length - 1; i >= 0; i--)
            {
                var key = new GUIContent($"[{hints[i].key}]");
                var label = new GUIContent(hints[i].label);
                float labelWidth = keyHintLabel.CalcSize(label).x;
                float keyWidth = keyHintKey.CalcSize(key).x;
                x -= labelWidth;
                GUI.Label(new Rect(x, y, labelWidth + 2f, 26f), label, keyHintLabel);
                x -= keyWidth + 6f;
                GUI.Label(new Rect(x, y, keyWidth + 2f, 26f), key, keyHintKey);
                x -= 26f;
            }
            return x + 26f;
        }

        /// <summary>The bottom-left tip strip: an ink band with one line of (rich) text.</summary>
        public static void FooterTip(Rect rect, string richText)
        {
            InkStrip(rect);
            float textX = Mathf.Max(rect.x + 22f, 40f); // strips may start off-screen (a brush stroke from the edge)
            GUI.Label(new Rect(textX, rect.y, rect.xMax - textX - 30f, rect.height), richText, InkSmall);
        }

        /// <summary>
        /// A centred parchment window with a 1 px ink frame, a spaced-capitals title (+ optional subtitle on the right)
        /// and an ink rule under it. Returns the content area.
        /// </summary>
        public static Rect ParchmentWindow(float width, float height, string title, string subtitle = null, bool backdrop = true)
        {
            if (backdrop) Backdrop(0.45f);
            width = Mathf.Min(width, Width - 32f);
            height = Mathf.Min(height, Height - 32f);
            var rect = new Rect((Width - width) * 0.5f, (Height - height) * 0.5f, width, height);
            ParchmentBackground(rect);
            var line = new Color(Ink.r, Ink.g, Ink.b, 0.85f);
            Fill(new Rect(rect.x, rect.y, rect.width, 1f), line);
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
            Fill(new Rect(rect.x, rect.y, 1f, rect.height), line);
            Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);

            GUI.Label(new Rect(rect.x + 26f, rect.y + 14f, rect.width - 52f, 40f), Spaced(title), InkHeader);
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(rect.x + 26f, rect.y + 24f, rect.width - 52f, 24f), subtitle, RightAlignedPaperMuted);
            Fill(new Rect(rect.x + 24f, rect.y + 58f, rect.width - 48f, 1f), new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            return new Rect(rect.x + 24f, rect.y + 70f, rect.width - 48f, rect.height - 90f);
        }

        static GUIStyle rightAlignedPaperMuted;
        static GUIStyle RightAlignedPaperMuted =>
            rightAlignedPaperMuted ??= new GUIStyle(PaperMuted) { alignment = TextAnchor.MiddleRight, wordWrap = false };

        /// <summary>A thin progress bar for parchment or ink: a faint track and a solid fill.</summary>
        public static void ThinBar(Rect rect, float fill, Color color, bool onInk = false)
        {
            var track = onInk ? new Color(1f, 1f, 1f, 0.15f) : new Color(Ink.r, Ink.g, Ink.b, 0.15f);
            Fill(rect, track);
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), color);
        }

        /// <summary>A straight line of the given width between two GUI points (skill-tree links).</summary>
        public static void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var delta = to - from;
            float length = delta.magnitude;
            if (length < 0.5f) return;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            RotateAround(angle, from);
            Fill(new Rect(from.x, from.y - width * 0.5f, length, width), color);
            GUI.matrix = matrix;
        }

        public enum NodeState { Locked, Available, Learned }

        /// <summary>
        /// A skill node: an ink diamond with a rim — gold when learned, vermilion when selected, ink when it can be learned,
        /// grey when locked. 'glyph' (a letter or slot key) sits in the middle.
        /// </summary>
        public static void DiamondNode(Rect rect, NodeState state, bool selected, string glyph)
        {
            Color rim = selected ? Vermilion : state switch
            {
                NodeState.Learned => InkGold,
                NodeState.Available => Ink,
                _ => new Color(0.62f, 0.60f, 0.56f),
            };
            Color body = state == NodeState.Locked ? new Color(0.36f, 0.35f, 0.33f) : Ink;
            DrawIcon(rect, DiamondIcon, rim);
            float inset = rect.width * 0.16f;
            DrawIcon(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f), DiamondIcon, body);
            if (state == NodeState.Learned)
            {
                float inner = rect.width * 0.30f;
                DrawIcon(new Rect(rect.x + inner, rect.y + inner, rect.width - inner * 2f, rect.height - inner * 2f), DiamondIcon,
                    new Color(InkGold.r, InkGold.g, InkGold.b, 0.25f));
            }
            if (!string.IsNullOrEmpty(glyph))
            {
                var style = BrushButtonStyle;
                int size = style.fontSize;
                var color = style.normal.textColor;
                style.fontSize = Mathf.RoundToInt(rect.height * 0.32f);
                style.normal.textColor = state == NodeState.Learned ? InkGold : state == NodeState.Locked ? new Color(0.75f, 0.73f, 0.70f) : OffWhite;
                GUI.Label(rect, glyph, style);
                style.fontSize = size;
                style.normal.textColor = color;
            }
        }

        /// <summary>A floating ink tooltip near the mouse, kept on screen (for parchment screens).</summary>
        public static void InkTooltip(string text, float width = 300f)
        {
            var content = new GUIContent(text);
            float height = InkBody.CalcHeight(content, width - 24f) + 18f;
            var mouse = Event.current.mousePosition;
            var rect = new Rect(mouse.x + 18f, mouse.y + 18f, width, height);
            if (rect.xMax > Width - 8f) rect.x = mouse.x - width - 12f;
            if (rect.yMax > Height - 8f) rect.y = Height - height - 8f;
            InkPanel(rect);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 9f, width - 24f, height - 18f), text, InkBody);
        }

        /// <summary>A small count chip (ink, off-white number) for the corner of a slot.</summary>
        public static void CountChip(Rect slot, int count)
        {
            string text = count.ToString();
            float width = InkSmall.CalcSize(new GUIContent(text)).x + 10f;
            var chip = new Rect(slot.xMax - width - 4f, slot.yMax - 22f, width, 19f);
            Fill(chip, new Color(Ink.r, Ink.g, Ink.b, 0.85f));
            var old = InkSmall.alignment;
            InkSmall.alignment = TextAnchor.MiddleCenter;
            GUI.Label(chip, text, InkSmall);
            InkSmall.alignment = old;
        }

        /// <summary>A paper slot: hover underline in vermilion; selected = a vermilion frame.</summary>
        public static void PaperSlot(Rect rect, bool selected, bool hovered)
        {
            if (selected)
            {
                Fill(rect, Vermilion);
                SlicedWith(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), paperCardTex, CardBorder);
                return;
            }
            PaperCard(rect, false);
            if (hovered) Fill(new Rect(rect.x + 1f, rect.yMax - 3f, rect.width - 2f, 2f), Vermilion);
        }

        /// <summary>
        /// A selectable card on parchment with a spaced-capitals title, an optional right-hand count and a second line;
        /// red brush block when selected. Returns true when clicked.
        /// </summary>
        public static bool FilterCard(Rect rect, string title, string count, string subtitle, bool selected)
        {
            bool hovered = !selected && rect.Contains(Event.current.mousePosition);
            PaperCard(rect, selected);
            if (hovered) Fill(new Rect(rect.x + 1f, rect.yMax - 3f, rect.width - 2f, 3f), Vermilion);
            var titleStyle = cardTitle;
            titleStyle.normal.textColor = selected ? OffWhite : Ink;
            float titleY = string.IsNullOrEmpty(subtitle) ? rect.y : rect.y + 8f;
            float titleHeight = string.IsNullOrEmpty(subtitle) ? rect.height : 30f;
            GUI.Label(new Rect(rect.x + 18f, titleY, rect.width - 36f, titleHeight), Spaced(title), titleStyle);
            if (!string.IsNullOrEmpty(count))
            {
                cardCount.normal.textColor = selected ? OffWhite : MutedOnPaper;
                GUI.Label(new Rect(rect.x + 18f, titleY, rect.width - 36f, titleHeight), count, cardCount);
            }
            if (!string.IsNullOrEmpty(subtitle))
            {
                cardSubtitle.normal.textColor = selected ? OffWhite : MutedOnPaper;
                GUI.Label(new Rect(rect.x + 18f, rect.y + 40f, rect.width - 36f, rect.height - 46f), subtitle, cardSubtitle);
            }
            return PaperClick(rect);
        }

        // ---------- Drawing helpers ----------

        static readonly Dictionary<RectOffset, GUIStyle> slicedStyles = new();

        /// <summary>9-slice draw with this texture's own border (the shared Sliced() assumes the frame textures' 10 px).</summary>
        static void SlicedWith(Rect rect, Texture2D texture, RectOffset border)
        {
            if (Event.current.type != EventType.Repaint || texture == null) return;
            if (!slicedStyles.TryGetValue(border, out var style))
                slicedStyles[border] = style = new GUIStyle { border = border };
            style.normal.background = texture;
            style.Draw(rect, GUIContent.none, false, false, false, false);
        }

        // ---------- Texture generation ----------

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// <summary>1D value noise in [0,1]: 'points' random knots, smoothly interpolated (wraps around).</summary>
        sealed class Noise1D
        {
            readonly float[] knots;
            public Noise1D(System.Random random, int points)
            {
                knots = new float[points];
                for (int i = 0; i < points; i++) knots[i] = (float)random.NextDouble();
            }

            /// <summary>t in [0,1] along the whole curve.</summary>
            public float At(float t)
            {
                float p = Mathf.Repeat(t, 1f) * knots.Length;
                int i = (int)p;
                float f = p - i;
                f = f * f * (3f - 2f * f);
                return Mathf.Lerp(knots[i % knots.Length], knots[(i + 1) % knots.Length], f);
            }
        }

        static Texture2D NewInkTexture(int width, int height) => new(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };

        static Texture2D ParchmentTexture(int size, System.Random random)
        {
            var tex = NewInkTexture(size, size);
            var pixels = new Color[size * size];
            var fibres = new Noise1D(random, 97);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x / (size - 1f) * 2f - 1f, dy = y / (size - 1f) * 2f - 1f;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.35f, Mathf.Sqrt(dx * dx + dy * dy)));
                    float grain = ((float)random.NextDouble() - 0.5f) * 0.02f + (fibres.At((x * 0.37f + y * 3.1f) / 97f) - 0.5f) * 0.02f;
                    float shade = 1f - 0.18f * edge + grain;
                    pixels[y * size + x] = new Color(Parchment.r * shade, Parchment.g * shade, Parchment.b * shade, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>A horizontal brush band: solid body, ragged dry-brush ends (each row ends at its own place, with gaps).</summary>
        static Texture2D BrushStrip(int width, int height, int rag, Color color, System.Random random)
        {
            var tex = NewInkTexture(width, height);
            var pixels = new Color[width * height];
            var leftEnds = new Noise1D(random, 9);
            var rightEnds = new Noise1D(random, 9);
            var streaks = new Noise1D(random, 23);
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                // Rows near the top and bottom edges are shorter, so the band reads as one stroke.
                float edgeBias = Mathf.Pow(Mathf.Abs(v - 0.5f) * 2f, 3f) * 0.6f;
                float left = rag * Mathf.Clamp01(leftEnds.At(v) * 0.8f + edgeBias);
                float right = width - 1 - rag * Mathf.Clamp01(rightEnds.At(v) * 0.8f + edgeBias);
                float rowStreak = streaks.At(v);
                // Dry brush: some rows (bristles) run out early, leaving thin pale lines into the ragged end.
                float dryLeft = rowStreak > 0.68f ? rag * (rowStreak - 0.68f) * 3.2f : 0f;
                float dryRight = rowStreak > 0.68f ? rag * (streaks.At(v + 0.5f) > 0.5f ? (rowStreak - 0.68f) * 3.2f : 0f) : 0f;
                for (int x = 0; x < width; x++)
                {
                    float alpha = Mathf.Clamp01(x - left + 0.5f) * Mathf.Clamp01(right - x + 0.5f);
                    if (x - left < dryLeft || right - x < dryRight) alpha *= 0.12f;
                    float fromEnd = Mathf.Min(x - left, right - x);
                    if (fromEnd < rag && random.NextDouble() < 0.05 * (1f - fromEnd / rag)) alpha *= 0.3f; // speckle near the tips
                    alpha *= 0.93f + 0.07f * rowStreak;
                    pixels[y * width + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>A filled square whose edges wobble a few pixels (a brush-painted block).</summary>
        static Texture2D WobblyBlock(int size, float wobble, Color color, System.Random random)
        {
            var tex = NewInkTexture(size, size);
            var pixels = new Color[size * size];
            var top = new Noise1D(random, 11);
            var bottom = new Noise1D(random, 11);
            var left = new Noise1D(random, 11);
            var right = new Noise1D(random, 11);
            for (int y = 0; y < size; y++)
            {
                float v = y / (size - 1f);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f);
                    float d = Mathf.Min(
                        Mathf.Min(x - wobble * left.At(v), size - 1 - wobble * right.At(v) - x),
                        Mathf.Min(y - wobble * bottom.At(u), size - 1 - wobble * top.At(u) - y));
                    float alpha = Mathf.Clamp01(d + 0.5f);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>A long brush swash: full height on the left, tapering and breaking into dry streaks toward the right.</summary>
        static Texture2D SwashTexture(int width, int height, Color color, System.Random random)
        {
            var tex = NewInkTexture(width, height);
            var pixels = new Color[width * height];
            var wobble = new Noise1D(random, 13);
            var streaks = new Noise1D(random, 31);
            float centre = height * 0.5f;
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                float rise = Mathf.SmoothStep(0.55f, 1f, Mathf.Clamp01(t / 0.06f));
                float taper = 1f - 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));
                float half = (height * 0.5f - 2f) * rise * taper + (wobble.At(t) - 0.5f) * 3f;
                float mid = centre + (wobble.At(t + 0.37f) - 0.5f) * 4f;
                for (int y = 0; y < height; y++)
                {
                    float d = half - Mathf.Abs(y + 0.5f - mid);
                    float alpha = Mathf.Clamp01(d + 0.5f);
                    if (t > 0.6f && streaks.At(y / (float)height) < (t - 0.6f) * 1.8f) alpha *= 0.1f; // dry tip
                    pixels[y * width + x] = new Color(color.r, color.g, color.b, alpha * 0.96f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D FlatCard(int size, Color fill, Color line)
        {
            var tex = NewInkTexture(size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = x == 0 || y == 0 || x == size - 1 || y == size - 1 ? line : fill;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
