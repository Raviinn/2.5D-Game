using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Title screen: Continue (the most recent save), New Game (pick one of the save slots; an occupied slot asks
    /// before it's overwritten), Load Game (each slot shows its day, time, play time and a few details; slots can
    /// be deleted, after a confirm), Settings (the shared SettingsPanel) and Quit. The backdrop is a dusk sky drawn in code until real art exists.
    /// Prototype IMGUI on the shared UITheme; lives in the MainMenu scene (Milestone 16 setup).
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        enum Page { Home, NewGame, LoadGame }
        enum Confirm { None, Overwrite, Delete }

        const float ButtonWidth = 360f;
        const float ButtonHeight = 50f;

        readonly SlotInfo[] slots = new SlotInfo[SaveService.SlotCount];
        int continueSlot = -1;
        bool anySave;

        Page page;
        Confirm confirm;
        int confirmSlot = -1;
        string busyText;
        string error;

        readonly SettingsPanel settingsPanel = new();

        GameStateService state;
        SaveService save;
        SceneLoader loader;

        Texture2D sky;
        Texture2D farHills;
        Texture2D nearHills;
        Vector3[] stars;
        GUIStyle titleStyle;

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
            if (sky != null) Destroy(sky);
            if (farHills != null) Destroy(farHills);
            if (nearHills != null) Destroy(nearHills);
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

        public async void StartNewGame(int slot)
        {
            if (busyText != null || save == null || loader == null) return;
            busyText = "Starting a new game…";
            error = null;
            bool ok = false;
            try { ok = await save.NewGameAsync(slot, loader.FirstScene); }
            catch (Exception e) { Debug.LogException(e); }
            if (ok || this == null) return;
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

        void Open(Page next)
        {
            page = next;
            confirm = Confirm.None;
            error = null;
            Refresh();
        }

        // ---------- Drawing ----------

        void OnGUI()
        {
            if (state == null || state.Current is not (GameState.MainMenu or GameState.Loading)) return;
            UITheme.Begin(-5);
            DrawBackdrop();
            if (!settingsPanel.IsOpen) DrawTitle();

            if (busyText != null || state.Current == GameState.Loading)
            {
                GUI.Label(new Rect(0f, UITheme.Height * 0.6f, UITheme.Width, 40f), busyText ?? "Loading…", UITheme.HeaderCenter);
                return;
            }

            if (settingsPanel.IsOpen)
            {
                settingsPanel.Draw();
                return;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && page != Page.Home)
            {
                if (confirm != Confirm.None) confirm = Confirm.None;
                else Open(Page.Home);
                Event.current.Use();
            }

            if (page == Page.Home) DrawHome();
            else DrawSlots();

            if (error != null)
                GUI.Label(new Rect(0f, UITheme.Height - 120f, UITheme.Width, 24f), $"<color={UITheme.BadHex}>{error}</color>", UITheme.SmallCenter);
            GUI.Label(new Rect(20f, UITheme.Height - 34f, 400f, 22f), $"<color={UITheme.MutedHex}>Beast · prototype v{Application.version}</color>", UITheme.Small);
        }

        void DrawTitle()
        {
            titleStyle ??= new GUIStyle(UITheme.Huge) { fontSize = 120, alignment = TextAnchor.MiddleCenter };
            float y = page == Page.Home ? 150f : 70f;
            UITheme.ShadowLabel(new Rect(0f, y, UITheme.Width, 140f), "BEAST", titleStyle, UITheme.Gold);
            if (page == Page.Home)
                UITheme.ShadowLabel(new Rect(0f, y + 132f, UITheme.Width, 30f), "A tale of the Hollows", UITheme.BodyCenter, new Color(UITheme.Text.r, UITheme.Text.g, UITheme.Text.b, 0.8f));
        }

        void DrawHome()
        {
            float x = (UITheme.Width - ButtonWidth) * 0.5f;
            float y = 470f;
            const float gap = 14f;

            if (continueSlot >= 0)
            {
                if (UITheme.Button(new Rect(x, y, ButtonWidth, ButtonHeight), "Continue", primary: true)) Continue();
                var info = slots[continueSlot];
                GUI.Label(new Rect(x - 100f, y + ButtonHeight + 2f, ButtonWidth + 200f, 20f),
                    $"<color={UITheme.MutedHex}>Slot {continueSlot + 1} · {DayLine(info.Summary)} · played {PlayTime(info.Summary.playSeconds)}</color>", UITheme.SmallCenter);
                y += ButtonHeight + gap + 22f;
            }

            if (UITheme.Button(new Rect(x, y, ButtonWidth, ButtonHeight), "New Game", primary: continueSlot < 0)) Open(Page.NewGame);
            y += ButtonHeight + gap;
            if (UITheme.Button(new Rect(x, y, ButtonWidth, ButtonHeight), "Load Game", anySave)) Open(Page.LoadGame);
            y += ButtonHeight + gap;
            if (UITheme.Button(new Rect(x, y, ButtonWidth, ButtonHeight), "Settings")) settingsPanel.Open();
            y += ButtonHeight + gap;
            if (UITheme.Button(new Rect(x, y, ButtonWidth, ButtonHeight), "Quit")) Quit();
        }

        void DrawSlots()
        {
            const float width = 760f;
            const float cardHeight = 118f;
            const float gap = 12f;
            float height = 66f + slots.Length * (cardHeight + gap) + 70f;
            var panel = new Rect((UITheme.Width - width) * 0.5f, Mathf.Max(230f, (UITheme.Height - height) * 0.5f + 60f), width, height);
            UITheme.Panel(panel);

            bool newGame = page == Page.NewGame;
            GUI.Label(new Rect(panel.x + 24f, panel.y + 14f, width - 48f, 34f), newGame ? "New Game" : "Load Game", UITheme.Title);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 16f, width - 48f, 30f),
                $"<color={UITheme.MutedHex}>{(newGame ? "Choose a slot for this journey" : "Choose a save")}</color>", UITheme.SmallRight);
            UITheme.Fill(new Rect(panel.x + 20f, panel.y + 54f, width - 40f, 1f), new Color(0.55f, 0.43f, 0.25f, 0.5f));

            float y = panel.y + 66f;
            foreach (var info in slots)
            {
                DrawSlotCard(new Rect(panel.x + 20f, y, width - 40f, cardHeight), info, newGame);
                y += cardHeight + gap;
            }

            float footerY = panel.yMax - 58f;
            if (UITheme.Button(new Rect(panel.x + 20f, footerY, 160f, 42f), "Back")) Open(Page.Home);
            GUI.Label(new Rect(panel.x + 200f, footerY, width - 220f, 42f), $"<color={UITheme.MutedHex}>Esc  back</color>", UITheme.SmallRight);
        }

        void DrawSlotCard(Rect rect, SlotInfo info, bool newGame)
        {
            UITheme.Inset(rect);
            float x = rect.x + 18f;
            float textWidth = rect.width - 260f;
            GUI.Label(new Rect(x, rect.y + 10f, textWidth, 28f), $"Slot {info.Slot + 1}", UITheme.Header);

            if (!info.Exists)
                GUI.Label(new Rect(x, rect.y + 44f, textWidth, 24f), $"<color={UITheme.MutedHex}>Empty</color>", UITheme.Body);
            else if (!info.Readable)
            {
                GUI.Label(new Rect(x, rect.y + 40f, textWidth, 24f), $"<color={UITheme.BadHex}>Damaged save</color>", UITheme.Body);
                GUI.Label(new Rect(x, rect.y + 64f, textWidth, 20f), $"<color={UITheme.MutedHex}>It can't be read, or it's from a newer version of the game.</color>", UITheme.Small);
            }
            else
            {
                var summary = info.Summary;
                GUI.Label(new Rect(x, rect.y + 40f, textWidth, 24f), DayLine(summary), UITheme.Body);
                if (summary.details != null && summary.details.Count > 0)
                    GUI.Label(new Rect(x, rect.y + 64f, textWidth, 20f), $"<color={UITheme.MutedHex}>{string.Join(" · ", summary.details)}</color>", UITheme.Small);
                GUI.Label(new Rect(x, rect.y + 86f, textWidth, 20f),
                    $"<color={UITheme.MutedHex}>Played {PlayTime(summary.playSeconds)} · saved {info.SavedAt:MMM d, HH:mm}</color>", UITheme.Small);
            }

            var buttons = new Rect(rect.xMax - 236f, rect.y + 12f, 220f, rect.height - 24f);
            if (confirm != Confirm.None && confirmSlot == info.Slot)
            {
                DrawConfirm(buttons, info);
                return;
            }

            if (newGame)
            {
                string label = info.Exists ? "Start over here" : "Start here";
                if (UITheme.Button(new Rect(buttons.x, buttons.y + 20f, buttons.width, 46f), label, primary: !info.Exists))
                {
                    if (info.Exists) Ask(Confirm.Overwrite, info.Slot);
                    else StartNewGame(info.Slot);
                }
                return;
            }

            if (!info.Exists) return; // nothing to load or delete
            if (info.Readable && UITheme.Button(new Rect(buttons.x, buttons.y, buttons.width, 44f), "Load", primary: true)) LoadSlot(info.Slot);
            if (UITheme.Button(new Rect(buttons.x, buttons.y + 52f, buttons.width, 38f), "Delete")) Ask(Confirm.Delete, info.Slot);
        }

        void DrawConfirm(Rect area, SlotInfo info)
        {
            bool overwrite = confirm == Confirm.Overwrite;
            GUI.Label(new Rect(area.x - 40f, area.y - 4f, area.width + 40f, 40f),
                $"<color={UITheme.BadHex}>{(overwrite ? $"Start over? Slot {info.Slot + 1}'s save will be lost." : $"Delete slot {info.Slot + 1} for good?")}</color>",
                UITheme.Small);
            float half = area.width * 0.5f - 5f;
            if (UITheme.Button(new Rect(area.x, area.y + 44f, half, 42f), overwrite ? "Overwrite" : "Delete"))
            {
                confirm = Confirm.None;
                if (overwrite) StartNewGame(info.Slot);
                else DeleteSlot(info.Slot);
            }
            if (UITheme.Button(new Rect(area.x + half + 10f, area.y + 44f, half, 42f), "Cancel")) confirm = Confirm.None;
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

        // ---------- Backdrop (placeholder art drawn in code) ----------

        void BuildBackdrop()
        {
            // Dusk: deep blue overhead fading to a warm horizon.
            sky = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var top = new Color(0.04f, 0.05f, 0.11f);
            var mid = new Color(0.16f, 0.13f, 0.22f);
            var horizon = new Color(0.62f, 0.34f, 0.20f);
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f; // 0 bottom, 1 top
                var c = t < 0.35f ? Color.Lerp(horizon, mid, t / 0.35f) : Color.Lerp(mid, top, (t - 0.35f) / 0.65f);
                sky.SetPixel(0, y, c);
            }
            sky.Apply();

            farHills = Ridge(new Color(0.13f, 0.10f, 0.15f), 11, 0.55f, pines: false);
            nearHills = Ridge(new Color(0.05f, 0.045f, 0.06f), 29, 0.35f, pines: true);

            var random = new System.Random(16);
            stars = new Vector3[90];
            for (int i = 0; i < stars.Length; i++)
                stars[i] = new Vector3((float)random.NextDouble(), (float)random.NextDouble() * 0.55f, (float)random.NextDouble() * 6.28f);
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
            foreach (var star in stars)
            {
                float twinkle = 0.45f + 0.35f * Mathf.Sin(time * 1.3f + star.z * 3f);
                UITheme.Fill(new Rect(star.x * w, star.y * h, 2f, 2f), new Color(1f, 0.96f, 0.86f, twinkle));
            }

            var moon = new Rect(w * 0.74f, h * 0.12f, 90f, 90f);
            UITheme.DrawIcon(new Rect(moon.x - 40f, moon.y - 40f, moon.width + 80f, moon.height + 80f), UITheme.CircleIcon, new Color(1f, 0.92f, 0.75f, 0.08f));
            UITheme.DrawIcon(moon, UITheme.CircleIcon, new Color(0.98f, 0.93f, 0.80f, 0.95f));

            // Hills drift very slowly, for a little life.
            float drift = time * 4f % w;
            DrawStrip(farHills, h * 0.40f, h * 0.60f, -drift * 0.4f);
            DrawStrip(nearHills, h * 0.55f, h * 0.45f, -drift);
            UITheme.Fill(new Rect(0f, h - 2f, w, 2f), new Color(0.05f, 0.045f, 0.06f));
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
