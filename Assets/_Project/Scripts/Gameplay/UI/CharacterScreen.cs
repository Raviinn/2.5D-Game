using System.Collections.Generic;
using System.Text;
using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Prototype character screen (IMGUI, C, pauses the game):
    /// Disciplines + skill trees on the left, equipment + stats on the right.
    /// The E / Q skill slots on the HUD are drawn by GameHud.
    /// </summary>
    public sealed class CharacterScreen : MonoBehaviour
    {
        static readonly StatType[] ShownStats =
        {
            StatType.MaxHealth, StatType.MaxStamina, StatType.MaxPoise, StatType.AttackPower, StatType.Defense,
            StatType.PoiseDamage, StatType.ParryStaminaRestore, StatType.ExtraYieldChance, StatType.DroughtTolerance,
            StatType.SeedSaveChance,
        };
        static readonly string[] SlotKeys = { "E", "Q" };
        const float SkillRowHeight = 90f;
        GUIStyle statusStyle;

        GameStateService state;
        InputAction openAction;
        PlayerProgression progression;
        PlayerSkills skills;
        PlayerEquipment equipment;
        PlayerStats stats;
        Inventory inventory;
        readonly List<SkillData> allSkills = new();
        readonly List<EquipmentData> bagGear = new();
        bool isOpen;
        Vector2 skillScroll;
        Vector2 gearScroll;

        void OnEnable() => EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        void OnDisable() => EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);

        void Start()
        {
            state = Services.Get<GameStateService>();
            openAction = Services.Get<InputService>().Character;

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                progression = player.GetComponent<PlayerProgression>();
                skills = player.GetComponent<PlayerSkills>();
                equipment = player.GetComponent<PlayerEquipment>();
                stats = player.GetComponent<PlayerStats>();
                inventory = player.GetComponent<Inventory>();
            }

            // Skill list: every SkillData in the database, grouped by Discipline, then tier.
            if (Services.TryGet(out GameDatabase database))
                foreach (var data in database.All)
                    if (data is SkillData skill) allSkills.Add(skill);
            allSkills.Sort((a, b) => a.Discipline != b.Discipline ? a.Discipline.CompareTo(b.Discipline) : a.Tier.CompareTo(b.Tier));
        }

        void Update()
        {
            if (openAction.WasPressedThisFrame() && state.Current == GameState.Playing && progression != null && skills != null)
            {
                state.SetState(GameState.InGameMenu);
                isOpen = true;
            }
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) isOpen = false;
        }

        void OnGUI()
        {
            if (progression == null || skills == null || !isOpen || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();

            string standing = Services.TryGet(out Reputation reputation) && reputation.IsConfigured
                ? $"   ·   {reputation.FactionName}: <color={UITheme.GoldHex}><b>{reputation.TierName}</b></color>"
                : string.Empty;
            var area = UITheme.Window(1240f, 800f, "Character", $"Renown <color={UITheme.GoldHex}><b>{progression.Renown}</b></color>{standing}",
                $"<color={UITheme.MutedHex}>Skill points come from levelling each Discipline.   ·   C / Esc: close</color>");

            float leftWidth = area.width * 0.56f;
            DrawSkillColumn(new Rect(area.x, area.y, leftWidth, area.height));
            DrawGearColumn(new Rect(area.x + leftWidth + 24f, area.y, area.width - leftWidth - 24f, area.height));
        }

        // ---------- Left: Disciplines and skills ----------

        void DrawSkillColumn(Rect area)
        {
            float y = area.y;
            var disciplines = (Discipline[])System.Enum.GetValues(typeof(Discipline));
            float cardWidth = (area.width - (disciplines.Length - 1) * 12f) / disciplines.Length;
            for (int i = 0; i < disciplines.Length; i++)
                DrawDisciplineCard(new Rect(area.x + i * (cardWidth + 12f), y, cardWidth, 78f), disciplines[i]);
            y += 92f;

            GUI.Label(new Rect(area.x, y, area.width, 26f), "Skills", UITheme.Header);
            y += 30f;

            var view = new Rect(area.x, y, area.width, area.yMax - y);
            UITheme.Inset(view);
            var content = new Rect(0f, 0f, view.width - 20f, allSkills.Count * SkillRowHeight + 8f);
            skillScroll = GUI.BeginScrollView(new Rect(view.x + 4f, view.y + 4f, view.width - 8f, view.height - 8f), skillScroll, content);
            for (int i = 0; i < allSkills.Count; i++)
                DrawSkillRow(new Rect(4f, 4f + i * SkillRowHeight, content.width - 8f, SkillRowHeight - 6f), allSkills[i]);
            GUI.EndScrollView();
        }

        void DrawDisciplineCard(Rect rect, Discipline d)
        {
            UITheme.Inset(rect);
            int points = progression.Points(d);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, 24f),
                $"<b>{d}</b>  <color={UITheme.MutedHex}>Level</color> <b>{progression.Level(d)}</b>", UITheme.Body);
            if (points > 0)
                UITheme.Badge(new Rect(rect.xMax - 96f, rect.y + 10f, 84f, 20f), $"{points} POINT{(points > 1 ? "S" : "")}", UITheme.Gold);

            bool max = progression.IsMaxLevel(d);
            float fill = max ? 1f : progression.Xp(d) / (float)Mathf.Max(1, progression.XpToNext(d));
            UITheme.Bar(new Rect(rect.x + 14f, rect.y + 40f, rect.width - 28f, 10f), fill, UITheme.Xp);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 52f, rect.width - 28f, 20f),
                $"<color={UITheme.MutedHex}>{(max ? "Max level" : $"{progression.Xp(d)} / {progression.XpToNext(d)} XP")}</color>", UITheme.Small);
        }

        void DrawSkillRow(Rect row, SkillData skill)
        {
            bool owned = skills.IsUnlocked(skill);
            string why = skills.WhyLocked(skill);
            UITheme.Slot(row, owned, false);

            string kind = skill.Kind == SkillKind.Active ? $"Active · {skill.Cooldown:0}s" : "Passive";
            string requirement = skill.RequiredMoveset != null ? $" · {skill.RequiredMoveset.DisplayName}" : string.Empty;
            string status = owned ? UITheme.Colored("Learned", UITheme.GoodHex)
                : why == null ? UITheme.Colored("Available", UITheme.GoldHex)
                : UITheme.Colored(why, UITheme.MutedHex);
            string effects = skill.Kind == SkillKind.Passive ? Describe(skill.PassiveModifiers)
                : skill.Buff != null && skill.Buff.Length > 0 ? $"Buff ({skill.BuffDuration:0}s): {Describe(skill.Buff)}" : string.Empty;
            if (effects.Length > 0) effects = UITheme.Colored(effects, UITheme.GoodHex);

            GUI.Label(new Rect(row.x + 12f, row.y + 6f, row.width - 190f, 22f),
                $"<b>{skill.DisplayName}</b>  <color={UITheme.MutedHex}><size=14>{skill.Discipline} · {kind}{requirement}</size></color>", UITheme.Body);
            // Status (Learned / Available / why it's locked) sits under the button, where it has room to wrap.
            statusStyle ??= new GUIStyle(UITheme.SmallCenter) { wordWrap = true, fontSize = 13 };
            GUI.Label(new Rect(row.xMax - 170f, row.y + 54f, 164f, row.height - 54f), status, statusStyle);
            GUI.Label(new Rect(row.x + 12f, row.y + 30f, row.width - 190f, row.height - 32f),
                $"<size=15><color={UITheme.MutedHex}>{skill.Description}</color> {effects}</size>", UITheme.Body);

            if (!owned)
            {
                if (UITheme.Button(new Rect(row.xMax - 164f, row.y + 14f, 152f, 34f), $"Learn  ({skill.Cost} pt)", why == null, primary: why == null))
                    skills.Unlock(skill);
            }
            else if (skill.Kind == SkillKind.Active)
            {
                for (int s = 0; s < PlayerSkills.SlotCount; s++)
                {
                    bool equipped = skills.SlotSkill(s) == skill;
                    var rect = new Rect(row.xMax - 164f + s * 78f, row.y + 14f, 74f, 34f);
                    if (UITheme.Button(rect, equipped ? $"On {SlotKeys[s]}" : $"Set {SlotKeys[s]}", !equipped, primary: equipped))
                        skills.EquipToSlot(skill, s);
                }
            }
        }

        // ---------- Right: equipment and stats ----------

        void DrawGearColumn(Rect area)
        {
            float y = area.y;
            GUI.Label(new Rect(area.x, y, area.width, 26f), "Equipment", UITheme.Header);
            y += 30f;

            if (equipment != null)
            {
                for (int i = 0; i < PlayerEquipment.WeaponSlots; i++)
                {
                    int index = i;
                    var weapon = equipment.WeaponAt(i);
                    string label = i == equipment.ActiveWeaponIndex ? $"Weapon {i + 1} <color={UITheme.GoldHex}>(in hand)</color>" : $"Weapon {i + 1}";
                    y = GearRow(area, y, label, weapon, () => equipment.UnequipWeapon(index));
                }
                foreach (var slot in PlayerEquipment.ArmorSlotOrder)
                {
                    var armorSlot = slot;
                    y = GearRow(area, y, slot.ToString(), equipment.ArmorAt(slot), () => equipment.UnequipArmor(armorSlot));
                }
            }

            // Equippable items in the bag
            y += 8f;
            GUI.Label(new Rect(area.x, y, area.width, 26f), "Gear in your bag", UITheme.Header);
            y += 30f;
            bagGear.Clear();
            if (inventory != null)
                for (int i = 0; i < inventory.SlotCount; i++)
                    if (!inventory[i].IsEmpty && inventory[i].Item is EquipmentData gear && !bagGear.Contains(gear)) bagGear.Add(gear);

            var view = new Rect(area.x, y, area.width, 112f);
            UITheme.Inset(view);
            var content = new Rect(0f, 0f, view.width - 20f, Mathf.Max(1, bagGear.Count) * 36f + 6f);
            gearScroll = GUI.BeginScrollView(new Rect(view.x + 4f, view.y + 4f, view.width - 8f, view.height - 8f), gearScroll, content);
            if (bagGear.Count == 0)
                GUI.Label(new Rect(10f, 8f, content.width - 20f, 40f), $"<color={UITheme.MutedHex}>No spare gear. Merchants and enemies have some.</color>", UITheme.Body);
            for (int i = 0; i < bagGear.Count; i++)
            {
                var gear = bagGear[i];
                float rowY = 4f + i * 36f;
                UITheme.ItemIcon(new Rect(8f, rowY + 4f, 24f, 24f), gear);
                GUI.Label(new Rect(40f, rowY, content.width - 130f, 32f),
                    $"{gear.DisplayName} <color={UITheme.MutedHex}><size=14>{gear.Slot} · {Describe(gear.Modifiers)}</size></color>", UITheme.BodyMiddle);
                if (UITheme.Button(new Rect(content.width - 84f, rowY + 2f, 78f, 28f), "Equip") && equipment != null) equipment.Equip(gear);
            }
            GUI.EndScrollView();
            y += 124f;

            GUI.Label(new Rect(area.x, y, area.width, 26f), "Stats", UITheme.Header);
            y += 30f;
            if (stats == null) return;
            DrawStats(new Rect(area.x, y, area.width, area.yMax - y));
        }

        void DrawStats(Rect area)
        {
            UITheme.Inset(area);
            float columnWidth = (area.width - 36f) * 0.5f;
            for (int i = 0; i < ShownStats.Length; i++)
            {
                var stat = ShownStats[i];
                float value = stats.Get(stat);
                string text = stat is StatType.ExtraYieldChance or StatType.SeedSaveChance ? $"{value * 100f:0}%" : $"{value:0.#}";
                float x = area.x + 12f + i % 2 * (columnWidth + 12f);
                float y = area.y + 10f + i / 2 * 24f;
                GUI.Label(new Rect(x, y, columnWidth, 22f), $"<color={UITheme.MutedHex}>{StatNames.Of(stat)}</color>", UITheme.Small);
                GUI.Label(new Rect(x, y, columnWidth, 22f), $"<b>{text}</b>", UITheme.SmallRight);
            }
        }

        float GearRow(Rect area, float y, string label, EquipmentData item, System.Func<bool> unequip)
        {
            var row = new Rect(area.x, y, area.width, 46f);
            UITheme.Slot(row, false, false);
            if (item != null) UITheme.ItemIcon(new Rect(row.x + 8f, row.y + 9f, 28f, 28f), item);
            float textWidth = row.width - 150f;
            if (item != null)
            {
                float labelWidth = UITheme.BodyMiddle.CalcSize(new GUIContent(StripTags(label) + ": ")).x;
                string name = UITheme.Fit(item.DisplayName, UITheme.BodyMiddleBold, textWidth - labelWidth - 10f);
                GUI.Label(new Rect(row.x + 46f, row.y + 3f, textWidth, 22f), $"<color={UITheme.MutedHex}>{label}:</color> <b>{name}</b>", UITheme.BodyMiddle);
            }
            else
            {
                GUI.Label(new Rect(row.x + 46f, row.y + 3f, textWidth, 22f), $"<color={UITheme.MutedHex}>{label}: empty</color>", UITheme.BodyMiddle);
            }
            if (item != null)
                GUI.Label(new Rect(row.x + 46f, row.y + 24f, textWidth, 20f),
                    UITheme.Colored(UITheme.Fit(Describe(item.Modifiers), UITheme.Small, textWidth), UITheme.GoodHex), UITheme.Small);
            if (item != null && UITheme.Button(new Rect(row.xMax - 96f, row.y + 9f, 88f, 28f), "Unequip") && !unequip())
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent("Bag is full"));
            return y + 50f;
        }

        static string StripTags(string text) => System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty);

        /// <summary>"+15 Attack, +10% Poise" — shared with other prototype screens.</summary>
        public static string Describe(StatModifier[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var modifier in modifiers)
            {
                if (modifier.Flat == 0f && modifier.Percent == 0f) continue; // "+0 Attack" says nothing
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(modifier.ToString());
            }
            return sb.ToString();
        }
    }
}
