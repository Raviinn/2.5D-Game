using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Title screen, in the ink-and-parchment style (UI restyle step 1).
    /// - Home: a stormy sky with drifting clouds and falling ash, "BEAST" in spaced capitals, and a plain list on the left:
    ///   Continue (the most recent save), New Game, Load Game, Settings, Quit. The highlighted item sits on a white brush
    ///   swash; the mouse or Up / Down (W / S) move it, Enter / Space chooses.
    /// - New Game: pick a slot (an occupied one asks before it's overwritten), then the character creator
    ///   (hair, hair colour, skin tone, outfit). Load Game: each slot's day, time, play time and details; delete after a confirm.
    ///   Both are parchment windows over the storm.
    /// - Settings: the shared SettingsPanel.
    /// Lives in the MainMenu scene (Milestone 16 setup).
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        enum Page { Home, NewGame, LoadGame, Create }
        enum Confirm { None, Overwrite, Delete }

        const float ItemSpacing = 56f;

        readonly SlotInfo[] slots = new SlotInfo[SaveService.SlotCount];
        int continueSlot = -1;
        bool anySave;

        Page page;
        Confirm confirm;
        int confirmSlot = -1;
        string busyText;
        string error;
        /// <summary>The home item on the swash (kept when the mouse leaves the list, like a console menu).</summary>
        int highlighted;

        readonly SettingsPanel settingsPanel = new();

        // Character creator
        int createSlot = -1;
        readonly LookEditor editor = new();

        GameStateService state;
        SaveService save;
        SceneLoader loader;

        // Storm backdrop
        Texture2D sky, clouds, farHills, nearHills;
        Vector4[] ash;
        GUIStyle menuItemOnSwash, subtitleStyle, centredInkBody, paperLabel, slotTitle;

        readonly struct HomeItem
        {
            public readonly string Label;
            public readonly bool Enabled;
            public readonly Action Choose;
            public HomeItem(string label, bool enabled, Action choose) { Label = label; Enabled = enabled; Choose = choose; }
        }

        void Awake()
        {
            // In Awake (not Start) so buttons and tests work from the very first frame the menu exists.
            Services.TryGet(out state);
            Services.TryGet(out save);
            Services.TryGet(out loader);
        }

        void Start()
        {
            if (state != null && state.Current is not (GameState.MainMenu or GameState.Loading)) state.SetState(GameState.MainMenu);
            BuildBackdrop();
            Refresh();
        }

        void OnDestroy()
        {
            foreach (var tex in new[] { sky, clouds, farHills, nearHills })
                if (tex != null) Destroy(tex);
            editor.Dispose();
        }

        /// <summary>Re-reads every slot from disk.</summary>
        public void Refresh()
        {
            if (save == null) return;
            anySave = false;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = save.ReadSlot(i);
                anySave |= slots[i].Exists;
            }
            continueSlot = save.MostRecentSlot();
        }

        // ---------- Actions (also used by tests) ----------

        public void Continue()
        {
            if (continueSlot >= 0) LoadSlot(continueSlot);
        }

        public async void LoadSlot(int slot)
        {
            if (busyText != null || save == null) return;
            busyText = "Loading…";
            error = null;
            bool ok = false;
            try { ok = await save.LoadAsync(slot); }
            catch (Exception e) { Debug.LogException(e); }
            if (ok || this == null) return; // on success this scene is gone
            busyText = null;
            error = $"Slot {slot + 1} couldn't be loaded.";
            Refresh();
        }

        /// <summary>Starts a new game in the slot with the default look (skips the character creator).</summary>
        public void StartNewGame(int slot) => StartNewGame(slot, new CharacterAppearance());

        public async void StartNewGame(int slot, CharacterAppearance appearance)
        {
            if (busyText != null || save == null || loader == null) return;
            busyText = "Starting a new game…";
            error = null;
            PlayerAppearance.Pending = (appearance ?? new CharacterAppearance()).Clone();
            bool ok = false;
            try { ok = await save.NewGameAsync(slot, loader.FirstScene); }
            catch (Exception e) { Debug.LogException(e); }
            if (ok || this == null) return;
            PlayerAppearance.Pending = null;
            busyText = null;
            error = "Couldn't start a new game.";
            Refresh();
        }

        public void DeleteSlot(int slot)
        {
            if (save != null && !save.DeleteSlot(slot)) error = $"Slot {slot + 1} couldn't be deleted.";
            confirm = Confirm.None;
            Refresh();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Opens the character creator for a new game in this slot (fresh default look).</summary>
        public void OpenCreator(int slot)
        {
            createSlot = slot;
            editor.Begin(new CharacterAppearance(), WeaponLook.SwordAndShield);
            Open(Page.Create);
        }

        /// <summary>Test hook: the look being edited in the creator.</summary>
        public CharacterAppearance CreatorLook => editor.Look;
        /// <summary>Test hook: the creator's editor (preview sheet, facing, weapon).</summary>
        public LookEditor Creator => editor;

        void Open(Page next)
        {
            page = next;
            confirm = Confirm.None;
            error = null;
            Refresh();
        }

        List<HomeItem> HomeItems()
        {
            var items = new List<HomeItem>(5);
            if (continueSlot >= 0) items.Add(new HomeItem("Continue", true, Continue));
            items.Add(new HomeItem("New Game", true, () => Open(Page.NewGame)));
            items.Add(new HomeItem("Load Game", anySave, () => Open(Page.LoadGame)));
            items.Add(new HomeItem("Settings", true, settingsPanel.Open));
            items.Add(new HomeItem("Quit", true, Quit));
            return items;
        }

        // ---------- Drawing ----------

        void OnGUI()
        {
            if (state == null || state.Current is not (GameState.MainMenu or GameState.Loading)) return;
            UITheme.Begin(-5);
            EnsureStyles();
            DrawBackdrop();
            if (!settingsPanel.IsOpen) DrawTitle();

            if (busyText != null || state.Current == GameState.Loading)
            {
                GUI.Label(new Rect(0f, UITheme.Height * 0.62f, UITheme.Width, 40f), UITheme.Spaced(busyText ?? "Loading…"), centredInkBody);
                return;
            }

            if (settingsPanel.IsOpen)
            {
                settingsPanel.Draw();
                return;
            }

            bool escape = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            if ((escape || (page != Page.Home && UITheme.ConsumeNavBack())) && page != Page.Home)
            {
                if (confirm != Confirm.None) confirm = Confirm.None;
                else Open(page == Page.Create ? Page.NewGame : Page.Home);
                if (escape) Event.current.Use();
            }

            if (page == Page.Home) DrawHome();
            else if (page == Page.Create) DrawCreator();
            else DrawSlots();

            if (error != null)
                GUI.Label(new Rect(0f, UITheme.Height - 96f, UITheme.Width, 26f), $"<color={UITheme.VermilionHex}>{error}</color>", centredInkBody);
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(32f, UITheme.Height - 46f, 400f, 26f), $"Beast · prototype v{Application.version}", UITheme.InkSmall);
            GUI.color = oldColor;
            if (page != Page.Home) UITheme.KeyHints(UITheme.Width - 40f, UITheme.Height - 76f, true, ("Esc", "Back"));
        }

        void EnsureStyles()
        {
            if (menuItemOnSwash != null && menuItemOnSwash.font == UITheme.MenuItem.font) return;
            menuItemOnSwash = new GUIStyle(UITheme.MenuItem) { normal = { textColor = UITheme.Ink } };
            subtitleStyle = new GUIStyle(UITheme.InkSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            centredInkBody = new GUIStyle(UITheme.InkBody) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            paperLabel = new GUIStyle(UITheme.PaperBody) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            slotTitle = new GUIStyle(UITheme.InkHeader) { fontSize = 22 };
        }

        void DrawTitle()
        {
            float w = UITheme.Width, h = UITheme.Height;
            bool home = page == Page.Home;
            var titleRect = home ? new Rect(w * 0.5f, h * 0.13f, w * 0.45f, 110f) : new Rect(0f, h * 0.04f, w, 90f);
            var style = UITheme.DisplayTitle;
            int oldSize = style.fontSize;
            style.fontSize = home ? 96 : 60;
            UITheme.ShadowLabel(titleRect, UITheme.Spaced("Beast"), style, UITheme.OffWhite);
            style.fontSize = oldSize;
            if (home)
                UITheme.ShadowLabel(new Rect(titleRect.x, titleRect.yMax + 2f, titleRect.width, 30f), "A tale of the Hollows", subtitleStyle,
                    new Color(UITheme.OffWhite.r, UITheme.OffWhite.g, UITheme.OffWhite.b, 0.7f));
        }

        void DrawHome()
        {
            var items = HomeItems();
            highlighted = Mathf.Clamp(highlighted, 0, items.Count - 1);
            float x = UITheme.Width * 0.07f;
            float y = UITheme.Height * 0.46f;

            // Keyboard: Up / Down (W / S) move the swash over enabled items; Enter / Space chooses.
            var evt = Event.current;
            if (evt.type == EventType.KeyDown)
            {
                // Arrows belong to the pad / arrow-key navigation (UINavigator); W / S still move the swash directly.
                int step = evt.keyCode is KeyCode.S ? 1 : evt.keyCode is KeyCode.W ? -1 : 0;
                if (step != 0)
                {
                    for (int i = 0, next = highlighted; i < items.Count; i++)
                    {
                        next = (next + step + items.Count) % items.Count;
                        if (items[next].Enabled) { highlighted = next; break; }
                    }
                    evt.Use();
                }
                else if ((evt.keyCode == KeyCode.Space || (!UITheme.NavShowing && evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)) && items[highlighted].Enabled)
                {
                    evt.Use();
                    items[highlighted].Choose();
                    return;
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                var row = new Rect(x, y + i * ItemSpacing, 440f, 50f);
                if (items[i].Enabled && (row.Contains(evt.mousePosition) || UITheme.IsNavFocused(row))) highlighted = i;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = new Rect(x, y + i * ItemSpacing, 440f, 50f);
                var text = new Rect(row.x + 22f, row.y, row.width - 22f, row.height);
                var old = GUI.color;
                if (!item.Enabled) GUI.color = new Color(1f, 1f, 1f, 0.35f);
                if (i == highlighted)
                {
                    UITheme.Swash(row);
                    GUI.Label(text, UITheme.Spaced(item.Label), menuItemOnSwash);
                }
                else
                {
                    UITheme.ShadowLabel(text, UITheme.Spaced(item.Label), UITheme.MenuItem, UITheme.OffWhite);
                }
                GUI.color = old;
                if (item.Enabled && UITheme.NavClick(row, preferred: i == highlighted, drawFocus: false)) // the swash shows the focus
                {
                    item.Choose();
                    return;
                }
            }

            if (continueSlot >= 0)
            {
                var info = slots[continueSlot];
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                GUI.Label(new Rect(x + 22f, y + items.Count * ItemSpacing + 8f, 600f, 26f),
                    $"Continue: slot {continueSlot + 1} · {DayLine(info.Summary)} · played {PlayTime(info.Summary.playSeconds)}", UITheme.InkSmall);
                GUI.color = old;
            }
        }

        void DrawSlots()
        {
            bool newGame = page == Page.NewGame;
            const float cardHeight = 118f;
            const float gap = 12f;
            float height = 70f + slots.Length * (cardHeight + gap) + 86f;
            var content = UITheme.ParchmentWindow(800f, height, newGame ? "New Game" : "Load Game",
                newGame ? "Choose a slot for this journey" : "Choose a save", backdrop: false);

            float y = content.y;
            foreach (var info in slots)
            {
                DrawSlotCard(new Rect(content.x, y, content.width, cardHeight), info, newGame);
                y += cardHeight + gap;
            }

            if (UITheme.BrushButton(new Rect(content.x, content.yMax - 46f, 170f, 46f), "Back")) Open(Page.Home);
        }

        void DrawSlotCard(Rect rect, SlotInfo info, bool newGame)
        {
            UITheme.PaperCard(rect, false);
            float x = rect.x + 20f;
            float textWidth = rect.width - 290f;
            GUI.Label(new Rect(x, rect.y + 10f, textWidth, 30f), UITheme.Spaced($"Slot {info.Slot + 1}"), slotTitle);

            if (!info.Exists)
                GUI.Label(new Rect(x, rect.y + 46f, textWidth, 24f), "Empty", UITheme.PaperMuted);
            else if (!info.Readable)
            {
                GUI.Label(new Rect(x, rect.y + 44f, textWidth, 24f), $"<color={UITheme.VermilionHex}>Damaged save</color>", UITheme.PaperBody);
                GUI.Label(new Rect(x, rect.y + 70f, textWidth, 22f), "It can't be read, or it's from a newer version of the game.", UITheme.PaperMuted);
            }
            else
            {
                var summary = info.Summary;
                GUI.Label(new Rect(x, rect.y + 44f, textWidth, 24f), DayLine(summary), UITheme.PaperBody);
                if (summary.details != null && summary.details.Count > 0)
                    GUI.Label(new Rect(x, rect.y + 68f, textWidth, 22f), string.Join(" · ", summary.details), UITheme.PaperMuted);
                GUI.Label(new Rect(x, rect.y + 90f, textWidth, 22f),
                    $"Played {PlayTime(summary.playSeconds)} · saved {info.SavedAt:MMM d, HH:mm}", UITheme.PaperMuted);
            }

            var buttons = new Rect(rect.xMax - 256f, rect.y + 14f, 236f, rect.height - 28f);
            if (confirm != Confirm.None && confirmSlot == info.Slot)
            {
                DrawConfirm(buttons, info);
                return;
            }

            if (newGame)
            {
                string label = info.Exists ? "Start over here" : "Start here";
                if (UITheme.BrushButton(new Rect(buttons.x, buttons.center.y - 23f, buttons.width, 46f), label))
                {
                    if (info.Exists) Ask(Confirm.Overwrite, info.Slot);
                    else OpenCreator(info.Slot);
                }
                return;
            }

            if (!info.Exists) return; // nothing to load or delete
            if (info.Readable && UITheme.BrushButton(new Rect(buttons.x, buttons.y, buttons.width, 44f), "Load")) LoadSlot(info.Slot);
            if (UITheme.BrushButton(new Rect(buttons.x + 40f, buttons.y + 52f, buttons.width - 40f, 38f), "Delete")) Ask(Confirm.Delete, info.Slot);
        }

        void DrawConfirm(Rect area, SlotInfo info)
        {
            bool overwrite = confirm == Confirm.Overwrite;
            GUI.Label(new Rect(area.x - 50f, area.y - 6f, area.width + 50f, 44f),
                $"<color={UITheme.VermilionHex}>{(overwrite ? $"Start over? Slot {info.Slot + 1}'s save will be lost." : $"Delete slot {info.Slot + 1} for good?")}</color>",
                UITheme.PaperMuted);
            float half = area.width * 0.5f - 5f;
            if (UITheme.BrushButton(new Rect(area.x, area.y + 44f, half, 42f), overwrite ? "Overwrite" : "Delete"))
            {
                confirm = Confirm.None;
                if (overwrite) OpenCreator(info.Slot);
                else DeleteSlot(info.Slot);
            }
            if (UITheme.BrushButton(new Rect(area.x + half + 10f, area.y + 44f, half, 42f), "Cancel")) confirm = Confirm.None;
        }

        void Ask(Confirm kind, int slot)
        {
            confirm = kind;
            confirmSlot = slot;
        }

        static string DayLine(SaveSummary summary) =>
            summary == null || summary.day <= 0 ? "Day ?" : $"Day {summary.day} · {summary.hour:00}:{summary.minute:00}";

        static string PlayTime(double seconds)
        {
            int minutes = (int)(seconds / 60.0);
            return minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60:00} min";
        }

        // ---------- Character creator ----------

        void DrawCreator()
        {
            var content = UITheme.ParchmentWindow(1000f, 680f, "Create your hero", $"New game in slot {createSlot + 1}", backdrop: false);
            editor.DrawStage(new Rect(content.x, content.y, 320f, content.height - 70f));
            float y = editor.DrawFields(new Rect(content.x + 352f, content.y + 4f, content.width - 352f, content.height - 70f), weaponPreview: true);
            GUI.Label(new Rect(content.x + 512f, y, content.width - 512f, 40f), "In the world you carry whichever weapon you have equipped.", UITheme.PaperMuted);

            float footerY = content.yMax - 48f;
            if (UITheme.BrushButton(new Rect(content.x, footerY, 170f, 48f), "Back")) Open(Page.NewGame);
            if (UITheme.BrushButton(new Rect(content.x + 182f, footerY, 220f, 48f), "Randomise")) editor.Randomise();
            if (UITheme.BrushButton(new Rect(content.xMax - 320f, footerY, 320f, 48f), "Begin your journey"))
                StartNewGame(createSlot, editor.Look);
        }

        // ---------- Storm backdrop (placeholder art drawn in code) ----------

        void BuildBackdrop()
        {
            sky = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var top = new Color(0.11f, 0.125f, 0.145f);
            var horizon = new Color(0.23f, 0.25f, 0.28f);
            var bottom = new Color(0.10f, 0.11f, 0.12f);
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f; // 0 bottom, 1 top
                var c = t < 0.38f ? Color.Lerp(bottom, horizon, t / 0.38f) : Color.Lerp(horizon, top, (t - 0.38f) / 0.62f);
                sky.SetPixel(0, y, c);
            }
            sky.Apply();

            clouds = CloudTexture(512, 128, 21);
            farHills = Ridge(new Color(0.14f, 0.15f, 0.17f), 11, 0.55f, pines: false);
            nearHills = Ridge(new Color(0.055f, 0.06f, 0.065f), 29, 0.35f, pines: true);

            // Ash: x, y (0-1), fall speed, sway phase.
            var random = new System.Random(16);
            ash = new Vector4[140];
            for (int i = 0; i < ash.Length; i++)
                ash[i] = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), 0.015f + (float)random.NextDouble() * 0.03f, (float)random.NextDouble() * 6.28f);
        }

        /// <summary>Soft storm clouds: tileable value noise (wraps left-right), faded out at the top and bottom of the strip.</summary>
        static Texture2D CloudTexture(int width, int height, int seed)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var random = new System.Random(seed);
            int[] octaveX = { 5, 11, 23, 47 }, octaveY = { 2, 4, 8, 16 };
            float[] weights = { 0.5f, 0.27f, 0.15f, 0.08f };
            var octaves = new float[octaveX.Length][,];
            for (int o = 0; o < octaves.Length; o++)
            {
                octaves[o] = new float[octaveX[o], octaveY[o] + 1];
                for (int x = 0; x < octaveX[o]; x++) for (int y = 0; y <= octaveY[o]; y++) octaves[o][x, y] = (float)random.NextDouble();
            }
            var colour = new Color(0.36f, 0.39f, 0.43f);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float envelope = Mathf.Sin(v * Mathf.PI);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float n = 0f;
                    for (int o = 0; o < octaves.Length; o++) n += weights[o] * Sample(octaves[o], octaveX[o], octaveY[o], u, v);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((n - 0.4f) * 3f)) * envelope;
                    pixels[y * width + x] = new Color(colour.r, colour.g, colour.b, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static float Sample(float[,] lattice, int cellsX, int cellsY, float u, float v)
        {
            float fx = u * cellsX, fy = v * cellsY;
            int x0 = (int)fx, y0 = Mathf.Min((int)fy, cellsY - 1);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int x1 = (x0 + 1) % cellsX;
            x0 %= cellsX;
            float a = Mathf.Lerp(lattice[x0, y0], lattice[x1, y0], tx);
            float b = Mathf.Lerp(lattice[x0, y0 + 1], lattice[x1, y0 + 1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>A silhouette strip: rolling hills (and pine tops) over transparent sky.</summary>
        static Texture2D Ridge(Color color, int seed, float baseHeight, bool pines)
        {
            const int width = 1024, height = 256;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[width * height];
            var random = new System.Random(seed);
            float p1 = (float)random.NextDouble() * 10f, p2 = (float)random.NextDouble() * 10f;
            var tops = new float[width];
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                // Whole periods across the strip, so it tiles seamlessly as it drifts.
                tops[x] = height * (baseHeight + 0.12f * Mathf.Sin(u * Mathf.PI * 4f + p1) + 0.06f * Mathf.Sin(u * Mathf.PI * 14f + p2));
            }
            if (pines)
            {
                var hills = (float[])tops.Clone(); // trees stand on the hills, never on each other
                for (int i = 0; i < 70; i++)
                {
                    int cx = random.Next(width);
                    float treeHeight = 30f + (float)random.NextDouble() * 50f;
                    float halfWidth = treeHeight * 0.28f;
                    float ground = hills[cx];
                    for (int dx = -(int)halfWidth; dx <= (int)halfWidth; dx++)
                    {
                        int x = (cx + dx + width) % width; // wrap, so trees at the edges tile too
                        tops[x] = Mathf.Min(height - 2f, Mathf.Max(tops[x], ground + treeHeight * (1f - Mathf.Abs(dx) / halfWidth)));
                    }
                }
            }
            var c = (Color32)color;
            var clear = new Color32(c.r, c.g, c.b, 0);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    pixels[y * width + x] = y < tops[x] ? c : clear;
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        void DrawBackdrop()
        {
            float w = UITheme.Width, h = UITheme.Height;
            GUI.DrawTexture(new Rect(0f, 0f, w, h), sky, ScaleMode.StretchToFill);

            float time = Time.unscaledTime;
            // Three cloud bands drifting at different speeds.
            DrawClouds(h * 0.02f, h * 0.30f, time * 0.006f, 0.55f, 1.6f);
            DrawClouds(h * 0.18f, h * 0.26f, time * 0.011f + 0.3f, 0.40f, 1.1f);
            DrawClouds(h * 0.34f, h * 0.20f, time * 0.018f + 0.6f, 0.28f, 0.8f);

            float drift = time * 4f % w;
            DrawStrip(farHills, h * 0.42f, h * 0.58f, -drift * 0.4f);
            DrawStrip(nearHills, h * 0.58f, h * 0.42f, -drift);

            // Falling ash: slow, swaying, wrapping around.
            var old = GUI.color;
            foreach (var a in ash)
            {
                float y = Mathf.Repeat(a.y + time * a.z, 1f);
                float x = Mathf.Repeat(a.x + Mathf.Sin(time * 0.6f + a.w) * 0.01f + time * 0.004f, 1f);
                float size = 2f + (a.w % 1f);
                UITheme.Fill(new Rect(x * w, y * h, size, size), new Color(0.95f, 0.94f, 0.91f, 0.2f + 0.35f * Mathf.Abs(Mathf.Sin(a.w))));
            }
            GUI.color = old;
            UITheme.Fill(new Rect(0f, h - 2f, w, 2f), new Color(0.05f, 0.05f, 0.055f));
        }

        void DrawClouds(float y, float height, float offset, float alpha, float scale)
        {
            float w = UITheme.Width;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(new Rect(0f, y, w, height), clouds, new Rect(offset, 0f, scale, 1f));
            GUI.color = old;
        }

        static void DrawStrip(Texture2D tex, float y, float height, float offset)
        {
            float w = UITheme.Width;
            offset %= w;
            GUI.DrawTexture(new Rect(offset, y, w, height), tex, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(offset + w, y, w, height), tex, ScaleMode.StretchToFill);
        }
    }
}
