using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// One look for every prototype (IMGUI) screen and HUD element:
    /// - resolution independence: everything is laid out on a virtual screen 1080 units tall (Begin() scales it)
    /// - a small palette, type scale and generated frame/icon textures (no art assets needed)
    /// - shared widgets: windows, buttons, tabs, bars, badges, key hints, item icons, tooltips
    /// Swap this for the real UI (UI Toolkit) later; screens only talk to these helpers.
    /// The light "ink and parchment" widgets and the fonts live in InkTheme.cs (the other half of this class).
    /// </summary>
    public static partial class UITheme
    {
        public const float ReferenceHeight = 1080f;

        // ---------- Palette (grounded, gritty fantasy: dark leather, aged gold, parchment text) ----------

        public static readonly Color Text = new(0.93f, 0.89f, 0.80f);
        public static readonly Color Muted = new(0.74f, 0.70f, 0.62f);
        public static readonly Color Faint = new(0.45f, 0.42f, 0.38f);
        public static readonly Color Gold = new(0.91f, 0.77f, 0.42f);
        public static readonly Color Good = new(0.56f, 0.82f, 0.52f);
        public static readonly Color Bad = new(0.90f, 0.47f, 0.37f);
        public static readonly Color Info = new(0.56f, 0.74f, 0.93f);
        public static readonly Color Health = new(0.80f, 0.22f, 0.20f);
        public static readonly Color StaminaColor = new(0.42f, 0.74f, 0.36f);
        public static readonly Color PoiseColor = new(0.62f, 0.45f, 0.92f);
        public static readonly Color Xp = new(0.93f, 0.78f, 0.32f);

        public const string TextHex = "#ede3cc";
        public const string MutedHex = "#bdb29f";
        public const string GoldHex = "#e8c46a";
        public const string GoodHex = "#8fd185";
        public const string BadHex = "#e5785e";
        public const string InfoHex = "#8fbdee";

        static readonly Color PanelFill = new(0.10f, 0.085f, 0.07f, 0.96f);
        static readonly Color PanelBorder = new(0.55f, 0.43f, 0.25f, 1f);
        static readonly Color InsetFill = new(0.06f, 0.05f, 0.045f, 0.85f);
        static readonly Color InsetBorder = new(0.28f, 0.23f, 0.17f, 1f);
        static readonly Color ButtonFill = new(0.22f, 0.18f, 0.13f, 1f);
        static readonly Color ButtonHoverFill = new(0.33f, 0.26f, 0.16f, 1f);
        static readonly Color ButtonActiveFill = new(0.16f, 0.13f, 0.09f, 1f);
        static readonly Color PrimaryFill = new(0.45f, 0.33f, 0.14f, 1f);
        static readonly Color PrimaryHoverFill = new(0.58f, 0.43f, 0.18f, 1f);
        static readonly Color SelectedFill = new(0.25f, 0.19f, 0.10f, 1f);
        /// <summary>HUD over the 3D world: see-through ink (ink-and-parchment restyle, step 4).</summary>
        static readonly Color HudFill = new(0.07f, 0.07f, 0.07f, 0.74f);

        // ---------- Textures ----------

        static Texture2D panelTex, insetTex, buttonTex, buttonHoverTex, buttonActiveTex, primaryTex, primaryHoverTex,
            selectedTex, hudTex, pillTex;

        public static Texture2D DiamondIcon { get; private set; }
        public static Texture2D CircleIcon { get; private set; }
        public static Texture2D RingIcon { get; private set; }
        public static Texture2D ArrowIcon { get; private set; }
        public static Texture2D CheckIcon { get; private set; }
        public static Texture2D BoxIcon { get; private set; }
        public static Texture2D CrossIcon { get; private set; }
        public static Texture2D CoinIcon { get; private set; }
        public static Texture2D SunIcon { get; private set; }
        public static Texture2D MoonIcon { get; private set; }
        public static Texture2D CloudIcon { get; private set; }
        public static Texture2D RainIcon { get; private set; }

        // ---------- Styles ----------

        public static GUIStyle Title { get; private set; }
        public static GUIStyle Header { get; private set; }
        public static GUIStyle HeaderCenter { get; private set; }
        public static GUIStyle Body { get; private set; }
        public static GUIStyle BodyCenter { get; private set; }
        /// <summary>Single line, vertically centred (pills, rows).</summary>
        public static GUIStyle BodyMiddle { get; private set; }
        /// <summary>Bold single line (also used to measure bold text for Fit).</summary>
        public static GUIStyle BodyMiddleBold { get; private set; }
        public static GUIStyle Small { get; private set; }
        public static GUIStyle SmallCenter { get; private set; }
        public static GUIStyle SmallRight { get; private set; }
        public static GUIStyle Big { get; private set; }
        public static GUIStyle Huge { get; private set; }
        public static GUIStyle ButtonStyle { get; private set; }
        public static GUIStyle PrimaryButtonStyle { get; private set; }
        public static GUIStyle RowStyle { get; private set; }
        public static GUIStyle TabStyle { get; private set; }
        public static GUIStyle KeyStyle { get; private set; }
        public static GUIStyle BadgeStyle { get; private set; }
        public static GUIStyle IconLabel { get; private set; }

        /// <summary>Virtual-to-physical pixel scale. Layout uses Width × Height; Begin() applies the scale.</summary>
        /// <summary>Screen pixels per virtual pixel, times the player's UI scale setting (80–120%).</summary>
        public static float Scale => Mathf.Max(0.5f, Screen.height / ReferenceHeight) * UserScale;

        static float UserScale => Services.TryGet(out SettingsService settings) ? settings.Current.uiScale : 1f;
        public static float Width => Screen.width / Scale;
        public static float Height => Screen.height / Scale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => panelTex = null; // forces styles to rebuild next Play session

        /// <summary>Call at the top of every OnGUI that uses the theme.</summary>
        public static void Begin(int depth = 0)
        {
            GUI.depth = depth;
            if (panelTex == null || Title == null) Build();
            float s = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;
        }

        /// <summary>Screen pixels (e.g. Camera.WorldToScreenPoint) → GUI coordinates on the virtual screen.</summary>
        public static Vector2 ScreenToGui(Vector3 screen) => new(screen.x / Scale, (Screen.height - screen.y) / Scale);

        /// <summary>True when the world point is in front of the camera; gui is its position on the virtual screen.</summary>
        public static bool WorldToGui(Camera cam, Vector3 world, out Vector2 gui)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            gui = ScreenToGui(screen);
            return screen.z > 0f;
        }

        // ---------- Panels ----------

        /// <summary>Dims the game behind a menu so the window has the player's full attention.</summary>
        public static void Backdrop(float alpha = 0.55f) => Fill(new Rect(0f, 0f, Width, Height), new Color(0f, 0f, 0f, alpha));

        public static void Panel(Rect rect) => Sliced(rect, panelTex);

        /// <summary>A recessed area inside a window (lists, details).</summary>
        public static void Inset(Rect rect) => Sliced(rect, insetTex);

        public static void HudPanel(Rect rect) => Sliced(rect, hudTex);

        public static void Pill(Rect rect, float alpha = 1f)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha * old.a); // fade along with the caller
            Sliced(rect, pillTex);
            GUI.color = old;
        }

        /// <summary>
        /// Standard menu window: dimmed backdrop, centred frame, title (+ optional subtitle on the right),
        /// a ✕ close button, and a footer line of key hints. Returns the content area.
        /// </summary>
        public static Rect Window(float width, float height, string title, string subtitle = null, string footer = null, bool backdrop = true)
        {
            if (backdrop) Backdrop();
            width = Mathf.Min(width, Width - 32f);
            height = Mathf.Min(height, Height - 32f);
            var rect = new Rect((Width - width) * 0.5f, (Height - height) * 0.5f, width, height);
            Sliced(rect, panelTex);

            GUI.Label(new Rect(rect.x + 24f, rect.y + 14f, rect.width - 100f, 34f), title, Title);
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(rect.x + 24f, rect.y + 16f, rect.width - 80f, 30f), subtitle, RightAligned(Body));
            Fill(new Rect(rect.x + 20f, rect.y + 54f, rect.width - 40f, 1f), new Color(PanelBorder.r, PanelBorder.g, PanelBorder.b, 0.5f));
            if (CloseButton(new Rect(rect.xMax - 46f, rect.y + 12f, 32f, 32f)) && Services.TryGet(out GameStateService state))
                state.SetState(GameState.Playing);

            if (!string.IsNullOrEmpty(footer))
                GUI.Label(new Rect(rect.x + 24f, rect.yMax - 34f, rect.width - 48f, 24f), footer, Small);

            float footerSpace = string.IsNullOrEmpty(footer) ? 16f : 42f;
            return new Rect(rect.x + 20f, rect.y + 66f, rect.width - 40f, rect.height - 66f - footerSpace);
        }

        /// <summary>
        /// The dark window frame (title, optional subtitle, footer line) centred inside 'bounds' and clamped to it, with no
        /// backdrop or close button: a screen shown as a game-menu tab before its own restyle. Returns the content area.
        /// </summary>
        public static Rect WindowIn(Rect bounds, float width, float height, string title, string subtitle = null, string footer = null)
        {
            width = Mathf.Min(width, bounds.width);
            height = Mathf.Min(height, bounds.height);
            var rect = new Rect(bounds.x + (bounds.width - width) * 0.5f, bounds.y + (bounds.height - height) * 0.5f, width, height);
            Sliced(rect, panelTex);

            GUI.Label(new Rect(rect.x + 24f, rect.y + 14f, rect.width - 48f, 34f), title, Title);
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(rect.x + 24f, rect.y + 16f, rect.width - 48f, 30f), subtitle, RightAligned(Body));
            Fill(new Rect(rect.x + 20f, rect.y + 54f, rect.width - 40f, 1f), new Color(PanelBorder.r, PanelBorder.g, PanelBorder.b, 0.5f));
            if (!string.IsNullOrEmpty(footer))
                GUI.Label(new Rect(rect.x + 24f, rect.yMax - 34f, rect.width - 48f, 24f), footer, Small);

            float footerSpace = string.IsNullOrEmpty(footer) ? 16f : 42f;
            return new Rect(rect.x + 20f, rect.y + 66f, rect.width - 40f, rect.height - 66f - footerSpace);
        }

        static GUIStyle rightAlignedCache;
        static GUIStyle RightAligned(GUIStyle source)
        {
            rightAlignedCache ??= new GUIStyle(source) { alignment = TextAnchor.MiddleRight };
            return rightAlignedCache;
        }

        // ---------- Widgets ----------

        /// <summary>Raised when any themed button, tab, row or toggle is clicked (interface sounds listen).</summary>
        public static event System.Action Clicked;

        static bool Click(bool clicked)
        {
            if (clicked) Clicked?.Invoke();
            return clicked;
        }

        public static bool Button(Rect rect, string text, bool enabled = true, bool primary = false)
        {
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool clicked = GUI.Button(rect, text, primary ? PrimaryButtonStyle : ButtonStyle);
            GUI.enabled = old;
            return Click(clicked);
        }

        /// <summary>A selectable list row (highlighted when selected). Returns true when clicked.</summary>
        public static bool Row(Rect rect, string text, bool selected)
        {
            if (selected) Sliced(rect, selectedTex);
            return Click(GUI.Button(rect, text, RowStyle));
        }

        public static bool Tab(Rect rect, string text, bool active)
        {
            Sliced(rect, active ? selectedTex : insetTex);
            return Click(GUI.Button(rect, text, TabStyle));
        }

        public static bool CloseButton(Rect rect)
        {
            bool clicked = Click(GUI.Button(rect, GUIContent.none, ButtonStyle));
            var icon = new Rect(rect.center.x - 7f, rect.center.y - 7f, 14f, 14f);
            DrawIcon(icon, CrossIcon, rect.Contains(Event.current.mousePosition) ? Gold : Text);
            return clicked;
        }

        public static void Bar(Rect rect, float fill, Color color, string label = null)
        {
            Fill(rect, new Color(0f, 0f, 0f, 0.65f));
            var inner = new Rect(rect.x + 1f, rect.y + 1f, (rect.width - 2f) * Mathf.Clamp01(fill), rect.height - 2f);
            Fill(inner, color);
            // Subtle top highlight for depth.
            if (inner.height >= 6f) Fill(new Rect(inner.x, inner.y, inner.width, Mathf.Max(1f, inner.height * 0.3f)), new Color(1f, 1f, 1f, 0.12f));
            if (!string.IsNullOrEmpty(label)) ShadowLabel(new Rect(rect.x + 8f, rect.y - 1f, rect.width - 16f, rect.height + 2f), label, Small, Text);
        }

        /// <summary>Small coloured tag, e.g. IN PROGRESS.</summary>
        public static void Badge(Rect rect, string text, Color color)
        {
            Fill(rect, new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.95f));
            Fill(new Rect(rect.x, rect.y, 3f, rect.height), color);
            GUI.Label(rect, text, BadgeStyle);
        }

        /// <summary>Draws a keyboard key cap ("F": an ink chip, off-white letter) followed by a label; returns the width used.</summary>
        public static float KeyHint(float x, float y, string key, string label, float alpha = 1f)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            float keyWidth = Mathf.Max(26f, KeyStyle.CalcSize(new GUIContent(key)).x + 12f);
            var keyRect = new Rect(x, y, keyWidth, 24f);
            KeyCap(keyRect);
            GUI.Label(keyRect, key, KeyStyle);
            float labelWidth = string.IsNullOrEmpty(label) ? 0f : Small.CalcSize(new GUIContent(label)).x;
            if (labelWidth > 0f) ShadowLabel(new Rect(keyRect.xMax + 6f, y, labelWidth + 4f, 24f), label, Small, Text);
            GUI.color = old;
            return keyWidth + (labelWidth > 0f ? labelWidth + 10f : 0f) + 14f;
        }

        /// <summary>The key-cap chip behind a key letter: ink with a faint light edge.</summary>
        public static void KeyCap(Rect rect)
        {
            Fill(rect, new Color(0.07f, 0.07f, 0.07f, 0.88f));
            var edge = new Color(1f, 1f, 1f, 0.28f);
            Fill(new Rect(rect.x, rect.y, rect.width, 1f), edge);
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), edge);
            Fill(new Rect(rect.x, rect.y, 1f, rect.height), edge);
            Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), edge);
        }

        /// <summary>A themed horizontal slider (click or drag anywhere on it). Returns the new value.</summary>
        public static float Slider(Rect rect, float value, float min, float max, float step = 0f, bool enabled = true)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var evt = Event.current;
            var track = new Rect(rect.x + 8f, rect.center.y - 3f, rect.width - 16f, 6f);
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
                Fill(track, new Color(0f, 0f, 0f, 0.6f * alpha));
                Fill(new Rect(track.x, track.y, track.width * t, track.height), new Color(Gold.r, Gold.g, Gold.b, 0.85f * alpha));
                bool active = GUIUtility.hotControl == id || (enabled && rect.Contains(evt.mousePosition));
                var thumb = new Rect(track.x + track.width * t - 8f, rect.center.y - 11f, 16f, 22f);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                Sliced(thumb, active ? primaryHoverTex : primaryTex);
                GUI.color = old;
            }
            return value;
        }

        /// <summary>A themed check box with a label to its right. Returns the new value.</summary>
        public static bool Toggle(Rect rect, bool value, string label = null)
        {
            var box = new Rect(rect.x, rect.center.y - 13f, 26f, 26f);
            bool hovered = rect.Contains(Event.current.mousePosition);
            Sliced(box, hovered ? buttonHoverTex : insetTex);
            if (value) DrawIcon(new Rect(box.x + 5f, box.y + 5f, 16f, 16f), CheckIcon, Gold, flipY: true); // the icon is drawn bottom-up
            if (!string.IsNullOrEmpty(label))
                GUI.Label(new Rect(box.xMax + 10f, rect.y, rect.width - 36f, rect.height), label, BodyMiddle);
            if (Click(GUI.Button(rect, GUIContent.none, GUIStyle.none))) value = !value;
            return value;
        }

        /// <summary>A row of options, one highlighted (e.g. Off | Low | Medium | High). Returns the chosen index.</summary>
        public static int Options(Rect rect, int selected, string[] options, bool enabled = true)
        {
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            float gap = 6f;
            float width = (rect.width - gap * (options.Length - 1)) / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var cell = new Rect(rect.x + i * (width + gap), rect.y, width, rect.height);
                if (Tab(cell, i == selected ? $"<color={GoldHex}><b>{options[i]}</b></color>" : options[i], i == selected)) selected = i;
            }
            GUI.enabled = old;
            return selected;
        }

        /// <summary>‹ value › picker for long lists (resolutions). Returns the new index.</summary>
        public static int Stepper(Rect rect, int index, int count, string text, bool enabled = true)
        {
            const float arrow = 40f;
            if (Button(new Rect(rect.x, rect.y, arrow, rect.height), "<", enabled && index > 0)) index--;
            Sliced(new Rect(rect.x + arrow + 6f, rect.y, rect.width - 2f * (arrow + 6f), rect.height), insetTex);
            GUI.Label(new Rect(rect.x + arrow + 6f, rect.y, rect.width - 2f * (arrow + 6f), rect.height), text, BodyCenter);
            if (Button(new Rect(rect.xMax - arrow, rect.y, arrow, rect.height), ">", enabled && index < count - 1)) index++;
            return Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        }

        /// <summary>Text with a 1-unit drop shadow: readable over any part of the 3D world.</summary>
        public static void ShadowLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f * color.a * old.a);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), StripColor(text), style);
            GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            GUI.Label(rect, text, style);
            GUI.color = old;
        }

        public static void DrawIcon(Rect rect, Texture2D icon, Color color, bool flipY = false)
        {
            var old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            if (flipY) GUI.DrawTextureWithTexCoords(rect, icon, new Rect(0f, 1f, 1f, -1f));
            else GUI.DrawTexture(rect, icon);
            GUI.color = old;
        }

        /// <summary>
        /// Rotates following GUI drawing around a point given in virtual (layout) coordinates.
        /// Save GUI.matrix first and restore it afterwards. (GUIUtility.RotateAroundPivot expects physical pixels.)
        /// </summary>
        public static void RotateAround(float degrees, Vector2 virtualPivot) =>
            GUIUtility.RotateAroundPivot(degrees, virtualPivot * Scale);

        public static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>An inventory-style slot frame.</summary>
        public static void Slot(Rect rect, bool selected, bool hovered)
        {
            Sliced(rect, selected ? selectedTex : hovered ? buttonHoverTex : insetTex);
        }

        /// <summary>Item icon, or its placeholder colour (bevelled gem) until icons exist.</summary>
        public static void ItemIcon(Rect rect, ItemData item)
        {
            if (item == null) return;
            if (item.Icon != null)
            {
                var sprite = item.Icon;
                var tex = sprite.texture;
                var r = sprite.textureRect;
                GUI.DrawTextureWithTexCoords(rect, tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
                return;
            }

            var c = item.PlaceholderColor;
            Fill(rect, new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f));
            Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), c);
            Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, (rect.height - 4f) * 0.35f), new Color(1f, 1f, 1f, 0.22f));
            // Category letter, so placeholder items are still tellable apart.
            GUI.Label(rect, $"<b>{CategoryLetter(item.Category)}</b>", IconLabel);
        }

        static string CategoryLetter(ItemCategory category) => category switch
        {
            ItemCategory.Consumable => "F",
            ItemCategory.Seed => "S",
            ItemCategory.Crop => "C",
            ItemCategory.Equipment => "E",
            ItemCategory.Quest => "!",
            _ => "M",
        };

        /// <summary>A floating tooltip box near the mouse, kept on screen.</summary>
        public static void Tooltip(string text, float width = 320f)
        {
            var content = new GUIContent(text);
            float height = Body.CalcHeight(content, width - 20f) + 16f;
            var mouse = Event.current.mousePosition;
            var rect = new Rect(mouse.x + 18f, mouse.y + 18f, width, height);
            if (rect.xMax > Width - 8f) rect.x = mouse.x - width - 12f;
            if (rect.yMax > Height - 8f) rect.y = Height - height - 8f;
            Sliced(rect, panelTex);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, width - 20f, height - 16f), text, Body);
        }

        public static string Colored(string text, string hex) => $"<color={hex}>{text}</color>";

        /// <summary>Shortens plain text with "…" so it fits the given width in this style.</summary>
        public static string Fit(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width) return text;
            for (int length = text.Length - 1; length > 1; length--)
            {
                string candidate = text.Substring(0, length).TrimEnd() + "…";
                if (style.CalcSize(new GUIContent(candidate)).x <= width) return candidate;
            }
            return "…";
        }

        static string StripColor(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("<color", System.StringComparison.Ordinal) < 0) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, "</?color[^>]*>", string.Empty);
        }

        // ---------- Construction ----------

        static GUIStyle sliceStyle;

        /// <summary>9-slice draw: corners and borders keep their size however large the rect is.</summary>
        static void Sliced(Rect rect, Texture2D texture)
        {
            if (Event.current.type != EventType.Repaint) return;
            sliceStyle ??= new GUIStyle { border = new RectOffset(SliceBorder, SliceBorder, SliceBorder, SliceBorder) };
            sliceStyle.normal.background = texture;
            sliceStyle.Draw(rect, GUIContent.none, false, false, false, false);
        }

        const int SliceBorder = 10;

        static void Build()
        {
            LoadFonts();
            panelTex = Frame(PanelFill, PanelBorder, 2, 6, innerGlow: true);
            insetTex = Frame(InsetFill, InsetBorder, 1, 4, innerGlow: false);
            buttonTex = Frame(ButtonFill, new Color(0.45f, 0.36f, 0.22f), 1, 4, innerGlow: true);
            buttonHoverTex = Frame(ButtonHoverFill, Gold, 1, 4, innerGlow: true);
            buttonActiveTex = Frame(ButtonActiveFill, Gold, 1, 4, innerGlow: false);
            primaryTex = Frame(PrimaryFill, Gold, 1, 4, innerGlow: true);
            primaryHoverTex = Frame(PrimaryHoverFill, new Color(1f, 0.9f, 0.6f), 1, 4, innerGlow: true);
            selectedTex = Frame(SelectedFill, Gold, 1, 4, innerGlow: true);
            hudTex = Frame(HudFill, new Color(1f, 1f, 1f, 0.14f), 1, 3, innerGlow: false);
            pillTex = Frame(new Color(0.07f, 0.07f, 0.07f, 0.8f), new Color(1f, 1f, 1f, 0.12f), 1, 3, innerGlow: false);

            DiamondIcon = Icon((x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 0.9f, (x, y) => Mathf.Abs(x) + Mathf.Abs(y) > 0.62f);
            CircleIcon = Icon((x, y) => x * x + y * y <= 0.8f, (x, y) => x * x + y * y > 0.5f);
            RingIcon = Icon((x, y) => { float d = x * x + y * y; return d <= 0.9f && d >= 0.55f; }, null);
            // Arrowhead pointing up (+y is up on screen), with a notched base.
            ArrowIcon = Icon((x, y) => y <= 0.85f && Mathf.Abs(x) <= (0.85f - y) * 0.55f && y >= -0.8f + Mathf.Abs(x) * 0.6f, null);
            CheckIcon = Icon((x, y) => DistanceToSegment(x, y, -0.65f, 0.05f, -0.2f, 0.5f) < 0.18f || DistanceToSegment(x, y, -0.2f, 0.5f, 0.7f, -0.5f) < 0.18f, null);
            BoxIcon = Icon((x, y) => Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) <= 0.8f && Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) >= 0.6f, null);
            CrossIcon = Icon((x, y) => DistanceToSegment(x, y, -0.7f, -0.7f, 0.7f, 0.7f) < 0.17f || DistanceToSegment(x, y, -0.7f, 0.7f, 0.7f, -0.7f) < 0.17f, null);
            CoinIcon = Icon((x, y) => x * x + y * y <= 0.8f, (x, y) => x * x + y * y > 0.55f || (Mathf.Abs(x) < 0.12f && Mathf.Abs(y) < 0.45f));
            SunIcon = Icon((x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d <= 0.42f) return true;
                if (d < 0.58f || d > 0.95f) return false;
                float angle = Mathf.Atan2(y, x) / (Mathf.PI / 4f); // 8 rays
                return Mathf.Abs(angle - Mathf.Round(angle)) < 0.16f;
            }, null);
            MoonIcon = Icon((x, y) => x * x + y * y <= 0.72f && (x - 0.38f) * (x - 0.38f) + (y - 0.22f) * (y - 0.22f) > 0.42f, null);
            CloudIcon = Icon(InCloud, (x, y) => y > -0.25f);
            RainIcon = Icon((x, y) => InCloud(x, y - 0.32f) ||
                                      DistanceToSegment(x, y, -0.42f, -0.48f, -0.52f, -0.82f) < 0.08f ||
                                      DistanceToSegment(x, y, 0.02f, -0.48f, -0.08f, -0.82f) < 0.08f ||
                                      DistanceToSegment(x, y, 0.46f, -0.48f, 0.36f, -0.82f) < 0.08f, null);

            var font = bodyFont;
            Title = Label(32, FontStyle.Normal, Gold, displayFont);
            Header = Label(23, FontStyle.Normal, Gold, displayFont);
            HeaderCenter = new GUIStyle(Header) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            Body = Label(17, FontStyle.Normal, Text);
            BodyCenter = new GUIStyle(Body) { alignment = TextAnchor.MiddleCenter };
            BodyMiddle = new GUIStyle(Body) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            BodyMiddleBold = new GUIStyle(BodyMiddle) { font = bodyBoldFont };
            Small = Label(15, FontStyle.Normal, Text);
            Small.wordWrap = false;
            Small.alignment = TextAnchor.MiddleLeft;
            SmallCenter = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter };
            SmallRight = new GUIStyle(Small) { alignment = TextAnchor.MiddleRight };
            Big = Label(38, FontStyle.Normal, Text, displayFont);
            Big.alignment = TextAnchor.MiddleCenter;
            Huge = Label(60, FontStyle.Normal, Text, displayFont);
            Huge.alignment = TextAnchor.MiddleCenter;
            IconLabel = Label(15, FontStyle.Normal, new Color(0f, 0f, 0f, 0.55f), bodyBoldFont);
            IconLabel.alignment = TextAnchor.MiddleCenter;

            ButtonStyle = new GUIStyle
            {
                font = font,
                fontSize = Sized(font, 17),
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                padding = new RectOffset(10, 10, 4, 4),
                border = new RectOffset(SliceBorder, SliceBorder, SliceBorder, SliceBorder),
                normal = { background = buttonTex, textColor = Text },
                hover = { background = buttonHoverTex, textColor = Gold },
                active = { background = buttonActiveTex, textColor = Gold },
                focused = { background = buttonTex, textColor = Text },
            };
            // IMGUI shows the "normal" state for disabled controls, tinted by GUI.enabled; a flat look reads clearly as off.
            PrimaryButtonStyle = new GUIStyle(ButtonStyle)
            {
                font = bodyBoldFont,
                normal = { background = primaryTex, textColor = new Color(1f, 0.95f, 0.85f) },
                hover = { background = primaryHoverTex, textColor = Color.white },
                active = { background = buttonActiveTex, textColor = Gold },
            };
            RowStyle = new GUIStyle(ButtonStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                padding = new RectOffset(12, 12, 6, 6),
                normal = { background = null, textColor = Text },
                hover = { background = buttonHoverTex, textColor = Text },
                active = { background = buttonActiveTex, textColor = Text },
                focused = { background = null, textColor = Text },
            };
            TabStyle = new GUIStyle(RowStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            KeyStyle = Label(15, FontStyle.Normal, new Color(0.95f, 0.94f, 0.91f), bodyBoldFont);
            KeyStyle.alignment = TextAnchor.MiddleCenter;
            KeyStyle.wordWrap = false;
            BadgeStyle = Label(12, FontStyle.Normal, Color.white, bodyBoldFont);
            BadgeStyle.alignment = TextAnchor.MiddleCenter;
            BadgeStyle.wordWrap = false;
            rightAlignedCache = null;
            rightAlignedPaperMuted = null;
            BuildInk();
        }

        /// <summary>A label style in the body font (or the given one). Bold faces are separate fonts, so style stays Normal.</summary>
        static GUIStyle Label(int size, FontStyle style, Color color, Font font = null) => new(GUI.skin.label)
        {
            font = font != null ? font : bodyFont,
            fontSize = Sized(font != null ? font : bodyFont, size),
            fontStyle = style,
            richText = true,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            normal = { textColor = color },
            hover = { textColor = color },
        };

        /// <summary>A rounded rectangle with a border; drawn stretched (small radius keeps stretching artefacts invisible).</summary>
        static Texture2D Frame(Color fill, Color border, int borderWidth, int radius, bool innerGlow)
        {
            const int size = 64;
            var tex = NewTexture(size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, radius); // < 0 inside
                    if (d > 0.5f) { pixels[y * size + x] = Color.clear; continue; }
                    float edgeAlpha = Mathf.Clamp01(0.5f - d);
                    Color c = d > -borderWidth ? border : fill;
                    if (innerGlow && d <= -borderWidth && d > -borderWidth - 3f)
                        c = Color.Lerp(fill, border, 0.18f * (1f + (d + borderWidth) / 3f));
                    // Slight vertical gradient: lighter top, darker bottom.
                    float shade = 1f + (y / (float)size - 0.5f) * 0.12f;
                    c = new Color(c.r * shade, c.g * shade, c.b * shade, c.a * edgeAlpha);
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static float RoundedDistance(float x, float y, int size, int radius)
        {
            float hx = size * 0.5f, hy = size * 0.5f;
            float qx = Mathf.Abs(x - hx) - (hx - radius);
            float qy = Mathf.Abs(y - hy) - (hy - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>Anti-aliased white icon from a shape test in [-1,1]²; 'dark' marks interior pixels drawn slightly darker (bevel).</summary>
        /// <summary>A puffy cloud with a flat base (centred a little low).</summary>
        static bool InCloud(float x, float y) =>
            (x + 0.36f) * (x + 0.36f) + (y + 0.12f) * (y + 0.12f) <= 0.14f ||
            (x - 0.05f) * (x - 0.05f) + (y - 0.1f) * (y - 0.1f) <= 0.22f ||
            (x - 0.45f) * (x - 0.45f) + (y + 0.15f) * (y + 0.15f) <= 0.12f ||
            (Mathf.Abs(x) <= 0.7f && y <= -0.12f && y >= -0.42f);

        static Texture2D Icon(System.Func<float, float, bool> inside, System.Func<float, float, bool> rim)
        {
            const int size = 32;
            const int samples = 4;
            var tex = NewTexture(size);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int hits = 0, rims = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float x = (px + (sx + 0.5f) / samples) / size * 2f - 1f;
                            float y = (py + (sy + 0.5f) / samples) / size * 2f - 1f;
                            if (!inside(x, y)) continue;
                            hits++;
                            if (rim != null && !rim(x, y)) rims++;
                        }
                    }
                    float alpha = hits / (float)(samples * samples);
                    float shade = hits > 0 && rim != null ? Mathf.Lerp(1f, 0.78f, rims / (float)hits) : 1f;
                    pixels[py * size + px] = new Color(shade, shade, shade, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(px, py);
            var a = new Vector2(ax, ay);
            var b = new Vector2(bx, by);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static Texture2D NewTexture(int size) => new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
    }
}
