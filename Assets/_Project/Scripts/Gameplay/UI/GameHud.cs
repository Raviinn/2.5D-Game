using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The gameplay HUD (prototype IMGUI), laid out once so nothing overlaps:
    /// - bottom centre: health / stamina / poise
    /// - bottom right: action slots (E, Q skills · R food · X weapon) with cooldowns
    /// - bottom left: key hints for the menus
    /// - left: notification feed (pickups, gold, XP, messages)
    /// - centre: the interaction prompt
    /// - under the minimap (top right): day, time and gold
    /// - full screen: defeat overlay and the sleep fade
    /// Created automatically in any scene with a Player (see UIBootstrap).
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        const int MaxNotes = 6;
        const float NoteLifetime = 4f;
        const float SlotSize = 68f;
        const float SlotGap = 10f;
        const float SleepHold = 1.3f;
        const float SleepFade = 0.8f;
        /// <summary>Prompt row (interaction, climbing controls): just above the player's head, never on top of it.</summary>
        const float PromptY = 0.58f;

        /// <summary>Top-right minimap footprint; the clock and the quest tracker sit underneath it.</summary>
        public const float MinimapSize = 220f;
        public const float MinimapMargin = 12f;
        public const float ClockTop = MinimapMargin + MinimapSize + 10f;
        public const float TrackerTop = ClockTop + 38f;

        struct Note
        {
            public string Text;
            public Color Color;
            public float Time;
        }

        GameStateService state;
        WorldClock clock;
        Combatant combatant;
        Stamina stamina;
        PlayerCombat combat;
        PlayerSkills skills;
        QuickItemUser itemUser;
        PlayerEquipment equipment;
        Inventory inventory;
        PlayerInteractor interactor;
        PlayerClimber climber;
        PlayerFatigue fatigue;
        PlayerBuffs buffs;
        PlayerAppearance appearance;
        WeatherSystem weather;
        DayNightCycle dayNight;

        readonly Note[] notes = new Note[MaxNotes];
        int nextNote;
        float sleepStart = float.NegativeInfinity;
        int sleepDay;
        int sleepHour;
        bool sleepSaved;

        void OnEnable()
        {
            EventBus<ItemsAddedEvent>.Subscribe(OnItemsAdded);
            EventBus<GoldChangedEvent>.Subscribe(OnGoldChanged);
            EventBus<ItemUsedEvent>.Subscribe(OnItemUsed);
            EventBus<HudMessageEvent>.Subscribe(OnHudMessage);
            EventBus<XpGainedEvent>.Subscribe(OnXpGained);
            EventBus<ReputationChangedEvent>.Subscribe(OnReputationChanged);
            EventBus<SleptEvent>.Subscribe(OnSlept);
            EventBus<GameSavedEvent>.Subscribe(OnSaved);
            EventBus<GameLoadedEvent>.Subscribe(OnLoaded);
        }

        void OnDisable()
        {
            EventBus<ItemsAddedEvent>.Unsubscribe(OnItemsAdded);
            EventBus<GoldChangedEvent>.Unsubscribe(OnGoldChanged);
            EventBus<ItemUsedEvent>.Unsubscribe(OnItemUsed);
            EventBus<HudMessageEvent>.Unsubscribe(OnHudMessage);
            EventBus<XpGainedEvent>.Unsubscribe(OnXpGained);
            EventBus<ReputationChangedEvent>.Unsubscribe(OnReputationChanged);
            EventBus<SleptEvent>.Unsubscribe(OnSlept);
            EventBus<GameSavedEvent>.Unsubscribe(OnSaved);
            EventBus<GameLoadedEvent>.Unsubscribe(OnLoaded);
        }

        void Start()
        {
            state = Services.Get<GameStateService>();
            Services.TryGet(out clock);
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            combatant = player.GetComponent<Combatant>();
            stamina = player.GetComponent<Stamina>();
            combat = player.GetComponent<PlayerCombat>();
            skills = player.GetComponent<PlayerSkills>();
            itemUser = player.GetComponent<QuickItemUser>();
            equipment = player.GetComponent<PlayerEquipment>();
            inventory = player.GetComponent<Inventory>();
            interactor = player.GetComponent<PlayerInteractor>();
            climber = player.GetComponent<PlayerClimber>();
            fatigue = player.GetComponent<PlayerFatigue>();
            buffs = player.GetComponent<PlayerBuffs>();
            appearance = player.GetComponent<PlayerAppearance>();
        }

        // ---------- Notifications ----------

        void OnItemsAdded(ItemsAddedEvent evt) => AddNote($"+{evt.Count}  {evt.Item.DisplayName}", UITheme.OffWhite);
        void OnItemUsed(ItemUsedEvent evt) => AddNote($"Used {evt.Item.DisplayName}", UITheme.MutedOnInk);
        void OnHudMessage(HudMessageEvent evt) => AddNote(evt.Text, UITheme.Vermilion);
        void OnXpGained(XpGainedEvent evt) => AddNote($"+{evt.Amount} {evt.Discipline} XP", UITheme.Xp);

        void OnReputationChanged(ReputationChangedEvent evt) =>
            AddNote(evt.Delta > 0 ? $"+{evt.Delta} Standing" : $"−{-evt.Delta} Standing", evt.Delta > 0 ? UITheme.Good : UITheme.Bad);
        void OnSaved(GameSavedEvent evt) { if (Time.unscaledTime - sleepStart > 1f) AddNote("Game saved", UITheme.Good); }
        void OnLoaded(GameLoadedEvent evt) => AddNote("Game loaded", UITheme.Good);

        void OnGoldChanged(GoldChangedEvent evt)
        {
            if (evt.Delta > 0) AddNote($"+{evt.Delta} gold", UITheme.Gold);
        }

        void OnSlept(SleptEvent evt)
        {
            sleepStart = Time.unscaledTime;
            sleepDay = evt.Day;
            sleepHour = evt.Hour;
            sleepSaved = evt.Saved;
        }

        void AddNote(string text, Color color)
        {
            notes[nextNote] = new Note { Text = text, Color = color, Time = Time.unscaledTime };
            nextNote = (nextNote + 1) % MaxNotes;
        }

        // ---------- Drawing ----------

        void OnGUI()
        {
            if (state == null) return;
            UITheme.Begin(-10); // above menus: notifications and fades must stay visible

            bool playing = state.Current == GameState.Playing;
            if (playing && combatant != null)
            {
                DrawVitals();
                DrawActionSlots();
                if (!DrawClimbHints()) DrawPrompt();
                DrawClock();
                DrawMenuHints();
                DrawDefeat();
            }
            if (playing || state.Current == GameState.InGameMenu) DrawNotes();
            DrawSleepFade();
        }

        void DrawVitals()
        {
            float width = 480f;
            float x = (UITheme.Width - width) * 0.5f;
            float y = UITheme.Height - 78f;

            if (combat != null && combat.Class != null && combat.Moveset != null)
                UITheme.ShadowLabel(new Rect(x, y - 26f, width, 22f), appearance != null
                        ? $"{UITheme.Spaced(appearance.PlayerName)}   <color={UITheme.MutedOnInkHex}>{combat.Class.DisplayName} · {combat.Moveset.DisplayName}</color>"
                        : $"{UITheme.Spaced(combat.Class.DisplayName)}   <color={UITheme.MutedOnInkHex}>{combat.Moveset.DisplayName}</color>",
                    UITheme.Small, UITheme.OffWhite);
            if (fatigue != null && fatigue.Level != FatigueLevel.Rested)
            {
                bool exhausted = fatigue.Level == FatigueLevel.Exhausted;
                UITheme.ShadowLabel(new Rect(x, y + 36f, width, 22f), UITheme.Spaced(exhausted ? "Exhausted" : "Tired"), UITheme.SmallCenter,
                    exhausted ? UITheme.Vermilion : UITheme.InkGold);
            }

            // Food and potion buffs, bottom-left of the bars: "+10% Attack 2:41".
            if (buffs != null && buffs.Buffs.Count > 0)
            {
                var line = new System.Text.StringBuilder();
                foreach (var buff in buffs.Buffs)
                {
                    if (line.Length > 0) line.Append("   ");
                    int seconds = Mathf.CeilToInt(buff.Remaining);
                    line.Append($"{buff.Source.Buff} <color={UITheme.MutedOnInkHex}>{seconds / 60}:{seconds % 60:00}</color>");
                }
                UITheme.ShadowLabel(new Rect(x, y + 36f, width, 22f), line.ToString(), UITheme.Small, UITheme.InkGold);
            }

            // Thin bars on an ink track (Tsushima-style): health in vermilion, stamina and poise below it.
            float hp = combatant.MaxHealth > 0f ? combatant.Health / combatant.MaxHealth : 0f;
            var hpRect = new Rect(x, y + 4f, width, 10f);
            // Low health: the track pulses red so it's noticed without looking at numbers.
            if (hp < 0.3f && !combatant.IsDead)
            {
                float pulse = 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
                UITheme.Fill(new Rect(hpRect.x - 3f, hpRect.y - 3f, hpRect.width + 6f, hpRect.height + 6f), new Color(UITheme.Vermilion.r, 0.12f, 0.12f, pulse));
            }
            HudBar(hpRect, hp, UITheme.Vermilion);
            UITheme.ShadowLabel(new Rect(x, y - 26f, width, 22f), $"{combatant.Health:0} / {combatant.MaxHealth:0}", UITheme.SmallRight, UITheme.OffWhite);
            if (stamina != null)
                HudBar(new Rect(x, y + 20f, width, 5f), stamina.Max > 0f ? stamina.Current / stamina.Max : 0f, new Color(0.62f, 0.78f, 0.52f));
            HudBar(new Rect(x + width * 0.25f, y + 31f, width * 0.5f, 3f),
                combatant.MaxPoise > 0f ? combatant.Poise / combatant.MaxPoise : 0f, UITheme.PoiseColor);
        }

        /// <summary>A HUD bar: see-through ink track, solid fill, no frame.</summary>
        static void HudBar(Rect rect, float fill, Color color)
        {
            UITheme.Fill(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), new Color(0.07f, 0.07f, 0.07f, 0.7f));
            UITheme.Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), color);
        }

        void DrawActionSlots()
        {
            float total = 4 * SlotSize + 3 * SlotGap;
            float x = UITheme.Width - total - 24f;
            float y = UITheme.Height - SlotSize - 58f;

            for (int i = 0; i < PlayerSkills.SlotCount && skills != null; i++)
            {
                var skill = skills.SlotSkill(i);
                float remaining = skills.CooldownRemaining(i);
                float ratio = skill != null && skill.Cooldown > 0f ? remaining / skill.Cooldown : 0f;
                Slot(new Rect(x + i * (SlotSize + SlotGap), y, SlotSize, SlotSize), i == 0 ? "E" : "Q",
                    skill != null ? skill.DisplayName : "Empty", null, skill != null ? Initials(skill.DisplayName) : string.Empty,
                    ratio, remaining, skill == null);
            }

            var food = itemUser != null ? itemUser.QuickItem : null;
            Slot(new Rect(x + 2 * (SlotSize + SlotGap), y, SlotSize, SlotSize), "R",
                food != null ? $"{food.DisplayName} ×{inventory.CountOf(food)}" : "No food", food, null, 0f, 0f, food == null);

            var weapon = equipment != null ? equipment.ActiveWeapon : null;
            string weaponName = weapon != null ? weapon.DisplayName : combat != null && combat.Moveset != null ? combat.Moveset.DisplayName : "—";
            Slot(new Rect(x + 3 * (SlotSize + SlotGap), y, SlotSize, SlotSize), "X", weaponName, weapon, weapon == null ? Initials(weaponName) : null, 0f, 0f, false);
        }

        void Slot(Rect rect, string key, string caption, ItemData item, string initials, float cooldownRatio, float cooldownLeft, bool empty)
        {
            UITheme.HudPanel(rect);
            var inner = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f);
            if (item != null) UITheme.ItemIcon(inner, item);
            else if (!string.IsNullOrEmpty(initials)) GUI.Label(inner, $"<b>{initials}</b>", UITheme.Big);

            if (empty) UITheme.Fill(inner, new Color(0f, 0f, 0f, 0.35f));
            if (cooldownRatio > 0f)
            {
                // Cooldown shade drains downward; the seconds left sit on top.
                UITheme.Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, (rect.height - 4f) * Mathf.Clamp01(cooldownRatio)), new Color(0f, 0f, 0f, 0.6f));
                UITheme.ShadowLabel(rect, $"<b>{cooldownLeft:0.0}</b>", UITheme.BodyCenter, UITheme.Text);
            }

            // Key cap in the corner, caption underneath.
            key = UITheme.KeyLabel(key);
            var keyRect = new Rect(rect.x - 6f, rect.y - 8f, Mathf.Max(24f, UITheme.KeyStyle.CalcSize(new GUIContent(key)).x + 8f), 22f);
            UITheme.KeyCap(keyRect);
            GUI.Label(keyRect, key, UITheme.KeyStyle);
            var captionRect = new Rect(rect.x - SlotGap * 0.5f + 1f, rect.yMax + 3f, rect.width + SlotGap - 2f, 20f);
            UITheme.ShadowLabel(captionRect, UITheme.Fit(caption, UITheme.SmallCenter, captionRect.width), UITheme.SmallCenter, empty ? UITheme.MutedOnInk : UITheme.OffWhite);
        }

        static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var parts = name.Split(' ', '&');
            string result = string.Empty;
            foreach (var part in parts)
                if (part.Length > 0 && char.IsLetter(part[0])) result += char.ToUpperInvariant(part[0]);
            return result.Length > 2 ? result.Substring(0, 2) : result;
        }

        void DrawPrompt()
        {
            if (interactor == null || interactor.Focus == null || string.IsNullOrEmpty(interactor.FocusPrompt.Text)) return;
            var prompt = interactor.FocusPrompt;

            var content = new GUIContent(prompt.Text);
            float textWidth = UITheme.Body.CalcSize(content).x;
            float width = textWidth + (prompt.CanInteract ? 66f : 32f);
            var rect = new Rect((UITheme.Width - width) * 0.5f, UITheme.Height * PromptY, width, 40f);
            UITheme.Pill(rect);
            if (prompt.CanInteract)
            {
                UITheme.KeyHint(rect.x + 12f, rect.y + 8f, "F", null);
                GUI.Label(new Rect(rect.x + 50f, rect.y, textWidth + 8f, rect.height), prompt.Text, UITheme.BodyMiddle);
            }
            else
            {
                var old = GUI.color;
                GUI.color = UITheme.Muted;
                GUI.Label(new Rect(rect.x + 16f, rect.y, textWidth + 8f, rect.height), prompt.Text, UITheme.BodyMiddle);
                GUI.color = old;
            }
        }

        /// <summary>Climbing controls in the prompt spot (they replace the interaction prompt). False if there are none.</summary>
        bool DrawClimbHints()
        {
            if (climber == null || climber.Hints.Count == 0) return false;
            // Near an ivy wall the "Climb" hint only shows when there's no interaction prompt to compete with.
            if (!climber.IsActive && interactor != null && interactor.Focus != null) return false;

            const float gap = 8f; // on top of KeyHint's own spacing
            float width = 24f - 14f - gap; // padding, minus the last hint's trailing space
            foreach (var (key, label) in climber.Hints) width += KeyHintWidth(key, label) + gap;
            var rect = new Rect((UITheme.Width - width) * 0.5f, UITheme.Height * PromptY, width, 40f);
            UITheme.Pill(rect);
            float x = rect.x + 12f;
            foreach (var (key, label) in climber.Hints) x += UITheme.KeyHint(x, rect.y + 8f, key, label) + gap;

            string warning = climber.Warning;
            if (warning != null)
                UITheme.ShadowLabel(new Rect(0f, rect.yMax + 6f, UITheme.Width, 24f), warning, UITheme.BodyCenter, UITheme.Bad);
            return true;
        }

        /// <summary>The width UITheme.KeyHint will return for this key and label.</summary>
        static float KeyHintWidth(string key, string label)
        {
            float keyWidth = Mathf.Max(26f, UITheme.KeyStyle.CalcSize(new GUIContent(key)).x + 12f);
            float labelWidth = string.IsNullOrEmpty(label) ? 0f : UITheme.Small.CalcSize(new GUIContent(label)).x;
            return keyWidth + (labelWidth > 0f ? labelWidth + 10f : 0f) + 14f;
        }

        /// <summary>Sun or moon, swapped for a cloud or rain when the weather turns.</summary>
        void DrawSkyIcon(Rect rect)
        {
            if (weather == null) Services.TryGet(out weather);
            if (dayNight == null) Services.TryGet(out dayNight);
            bool night = dayNight != null && dayNight.IsNight;
            var current = weather != null ? weather.Current : Weather.Clear;
            if (current == Weather.Rain) UITheme.DrawIcon(rect, UITheme.RainIcon, UITheme.Info);
            else if (current == Weather.Cloudy) UITheme.DrawIcon(rect, UITheme.CloudIcon, UITheme.MutedOnInk);
            else if (night) UITheme.DrawIcon(rect, UITheme.MoonIcon, new Color(0.8f, 0.86f, 1f));
            else UITheme.DrawIcon(rect, UITheme.SunIcon, UITheme.InkGold);
        }

        void DrawClock()
        {
            if (clock == null) return;
            float width = MinimapSize;
            var rect = new Rect(UITheme.Width - width - MinimapMargin, ClockTop, width, 30f);
            UITheme.HudPanel(rect);
            GUI.Label(new Rect(rect.x + 10f, rect.y, 110f, rect.height), $"<b>Day {clock.Day}</b>   {clock.Hour:00}:{clock.Minute:00}", UITheme.Small);
            DrawSkyIcon(new Rect(rect.x + 120f, rect.y + 6f, 18f, 18f));
            if (inventory != null)
            {
                UITheme.DrawIcon(new Rect(rect.xMax - 76f, rect.y + 8f, 14f, 14f), UITheme.CoinIcon, UITheme.InkGold);
                GUI.Label(new Rect(rect.xMax - 58f, rect.y, 50f, rect.height), $"<b>{inventory.Gold}</b>", UITheme.Small);
            }
        }

        static string Period(int hour) => hour switch
        {
            >= 5 and < 12 => "Morning",
            >= 12 and < 17 => "Afternoon",
            >= 17 and < 21 => "Evening",
            _ => "Night",
        };

        void DrawMenuHints()
        {
            float x = 18f;
            float y = UITheme.Height - 40f;
            if (UITheme.UsingGamepad)
            {
                // The pad opens the game menu on View (then LB / RB for the tabs) and Options on Start.
                x += UITheme.KeyHint(x, y, "Start", "Options", 0.85f);
                UITheme.KeyHint(x, y, "View", "Bag · Character · Journal · Map", 0.85f);
                return;
            }
            x += UITheme.KeyHint(x, y, "Esc", "Menu", 0.85f);
            x += UITheme.KeyHint(x, y, "Tab", "Bag", 0.85f);
            x += UITheme.KeyHint(x, y, "C", "Character", 0.85f);
            x += UITheme.KeyHint(x, y, "J", "Journal", 0.85f);
            UITheme.KeyHint(x, y, "M", "Map", 0.85f);
        }

        void DrawNotes()
        {
            float x = 24f;
            float y = UITheme.Height * 0.60f;
            for (int i = 1; i <= MaxNotes; i++)
            {
                ref var note = ref notes[(nextNote - i + MaxNotes) % MaxNotes];
                if (note.Text == null) continue;
                float age = Time.unscaledTime - note.Time;
                if (age > NoteLifetime) continue;

                float alpha = age > NoteLifetime - 1f ? NoteLifetime - age : 1f;
                float slide = Mathf.Clamp01(age / 0.15f); // slides in from the left
                float width = UITheme.Small.CalcSize(new GUIContent(note.Text)).x + 28f;
                var rect = new Rect(x - (1f - slide) * 40f, y, width, 30f);

                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                UITheme.Pill(rect, 0.9f);
                UITheme.Fill(new Rect(rect.x + 6f, rect.y + 9f, 3f, 12f), note.Color);
                GUI.Label(new Rect(rect.x + 16f, rect.y, width, rect.height), note.Text, UITheme.Small);
                GUI.color = old;
                y -= 36f;
            }
        }

        void DrawDefeat()
        {
            if (combat == null || !combat.IsDead) return;
            UITheme.Fill(new Rect(0f, 0f, UITheme.Width, UITheme.Height), new Color(0.25f, 0f, 0f, 0.45f));
            UITheme.ShadowLabel(new Rect(0f, UITheme.Height * 0.38f, UITheme.Width, 70f), UITheme.Spaced("Defeated"), UITheme.Huge, UITheme.Vermilion);
            UITheme.ShadowLabel(new Rect(0f, UITheme.Height * 0.38f + 72f, UITheme.Width, 30f),
                $"You stagger back to safety… {combat.RespawnTimeLeft:0.0}s", UITheme.BodyCenter, UITheme.Text);
        }

        void DrawSleepFade()
        {
            float age = Time.unscaledTime - sleepStart;
            if (age > SleepHold + SleepFade) return;
            float alpha = age < SleepHold ? 1f : 1f - (age - SleepHold) / SleepFade;
            UITheme.Fill(new Rect(0f, 0f, UITheme.Width, UITheme.Height), new Color(0f, 0f, 0f, alpha));
            var color = new Color(UITheme.Text.r, UITheme.Text.g, UITheme.Text.b, alpha);
            UITheme.ShadowLabel(new Rect(0f, UITheme.Height * 0.42f, UITheme.Width, 60f), UITheme.Spaced($"Day {sleepDay}"), UITheme.Huge, color);
            UITheme.ShadowLabel(new Rect(0f, UITheme.Height * 0.42f + 62f, UITheme.Width, 30f),
                $"{Period(sleepHour)}, {sleepHour:00}:00 · You feel rested{(sleepSaved ? " · Game saved" : string.Empty)}", UITheme.BodyCenter, color);
        }
    }
}
