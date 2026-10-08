using System.Collections.Generic;
using System.Text;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The Character tab of the game menu (C), after Ghost of Tsushima's Techniques page:
    /// - cards on the left: Combat, Farming (level, XP, unspent points) and Gear (renown, standing)
    /// - Combat / Farming: the Discipline's skills as a tree of diamond nodes, one row per tier (gold rim = learned,
    ///   ink = can be learned, grey = locked, vermilion = selected), linked to their prerequisites
    /// - Gear: the character's sprite standing among the six equipment slots
    /// - an ink panel on the right: the selected skill (Learn, or put it on E / Q) or slot (Unequip, spare gear to
    ///   equip, all stats)
    /// GameMenu opens it and draws the frame. The E / Q skill slots on the HUD are drawn by GameHud.
    /// </summary>
    public sealed class CharacterScreen : MonoBehaviour, IGameMenuTab
    {
        enum Page { Combat, Farming, Gear }

        static readonly StatType[] ShownStats =
        {
            StatType.MaxHealth, StatType.MaxStamina, StatType.MaxPoise, StatType.AttackPower, StatType.Defense,
            StatType.PoiseDamage, StatType.ParryStaminaRestore, StatType.ExtraYieldChance, StatType.DroughtTolerance,
            StatType.SeedSaveChance,
        };
        static readonly string[] SlotKeys = { "E", "Q" };
        const float CardsWidth = 270f;
        const float DetailsWidth = 470f;
        const float NodeSize = 78f;
        const float TierSpacing = 170f;
        /// <summary>Links leave a node below its name label.</summary>
        const float LinkDrop = NodeSize * 0.5f + 54f;

        PlayerProgression progression;
        PlayerSkills skills;
        PlayerEquipment equipment;
        PlayerStats stats;
        Inventory inventory;
        PlayerAppearance appearance;
        GUIStyle figureName;
        DirectionalSpriteRenderer sprite;
        readonly List<SkillData> allSkills = new();
        readonly List<EquipmentData> spareGear = new();
        readonly Dictionary<SkillData, Vector2> nodeCentres = new();

        Page page;
        SkillData selectedSkill;
        int selectedSlot; // 0-1 weapons, then the armour slots in PlayerEquipment.ArmorSlotOrder
        GUIStyle nodeLabel, nameStyle, metaStyle, bodyStyle, headerRight, slotLabel, slotName, statLabel, statValue, sectionStyle;

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                progression = player.GetComponent<PlayerProgression>();
                skills = player.GetComponent<PlayerSkills>();
                equipment = player.GetComponent<PlayerEquipment>();
                stats = player.GetComponent<PlayerStats>();
                inventory = player.GetComponent<Inventory>();
                appearance = player.GetComponent<PlayerAppearance>();
                sprite = player.GetComponentInChildren<DirectionalSpriteRenderer>();
            }

            // Every SkillData in the database, grouped by Discipline, then tier.
            if (Services.TryGet(out GameDatabase database))
                foreach (var data in database.All)
                    if (data is SkillData skill) allSkills.Add(skill);
            allSkills.Sort((a, b) => a.Discipline != b.Discipline ? a.Discipline.CompareTo(b.Discipline) : a.Tier.CompareTo(b.Tier));
        }

        // ---------- Game menu tab ----------

        public bool CanOpen => progression != null && skills != null;
        public string FooterTip => page == Page.Gear
            ? "Click a slot to see what fits it from your bag"
            : "Earn <color=#c9a86a>skill points</color> by levelling each Discipline";
        public (string key, string label)[] KeyHints => null;
        public bool HasSubView => false;
        public void CloseSubView() { }
        public void OnTabOpened() { }

        public void DrawTab(Rect bounds)
        {
            if (!CanOpen) return;
            EnsureStyles();

            DrawCards(new Rect(bounds.x, bounds.y, CardsWidth, bounds.height));
            var middle = new Rect(bounds.x + CardsWidth + 40f, bounds.y, bounds.width - CardsWidth - 80f - DetailsWidth, bounds.height);
            var details = new Rect(bounds.xMax - DetailsWidth, bounds.y, DetailsWidth, bounds.height);
            if (page == Page.Gear)
            {
                DrawGear(middle);
                DrawGearDetails(details);
            }
            else
            {
                var discipline = page == Page.Combat ? Discipline.Combat : Discipline.Farming;
                DrawTree(middle, discipline);
                DrawSkillDetails(details);
            }
        }

        // ---------- Cards ----------

        void DrawCards(Rect area)
        {
            float y = area.y;
            foreach (var d in new[] { Discipline.Combat, Discipline.Farming })
            {
                var p = d == Discipline.Combat ? Page.Combat : Page.Farming;
                int points = progression.Points(d);
                bool max = progression.IsMaxLevel(d);
                string xp = max ? "Max level" : $"{progression.Xp(d)} / {progression.XpToNext(d)} XP";
                var card = new Rect(area.x, y, area.width, 118f);
                if (UITheme.FilterCard(card, d.ToString(), points > 0 ? $"{points} pt{(points > 1 ? "s" : "")}" : null,
                        $"Level {progression.Level(d)}   ·   {xp}", page == p) && page != p)
                    SelectPage(p);
                float fill = max ? 1f : progression.Xp(d) / (float)Mathf.Max(1, progression.XpToNext(d));
                UITheme.ThinBar(new Rect(card.x + 18f, card.yMax - 22f, card.width - 36f, 5f), fill,
                    page == p ? UITheme.OffWhite : UITheme.Vermilion, onInk: page == p);
                y += 132f;
            }

            string standing = Services.TryGet(out Reputation reputation) && reputation.IsConfigured
                ? $"{reputation.FactionName}: {reputation.TierName}" : "Your equipment and stats";
            if (UITheme.FilterCard(new Rect(area.x, y, area.width, 118f), "Gear", $"Renown {progression.Renown}", standing, page == Page.Gear) && page != Page.Gear)
                SelectPage(Page.Gear);
        }

        void SelectPage(Page p)
        {
            page = p;
            selectedSkill = null;
        }

        // ---------- Skill tree ----------

        void DrawTree(Rect area, Discipline discipline)
        {
            int points = progression.Points(discipline);
            GUI.Label(new Rect(area.x, area.y, area.width, 40f), UITheme.Spaced($"{discipline} skills"), UITheme.InkHeader);
            GUI.Label(new Rect(area.x, area.y, area.width, 40f),
                points > 0 ? $"<color={UITheme.InkGoldDarkHex}>{points} skill point{(points > 1 ? "s" : "")} to spend</color>" : "No points to spend", headerRight);
            UITheme.Fill(new Rect(area.x, area.y + 48f, area.width, 1f), new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.4f));

            // Rows: one per tier, nodes spread evenly across the width.
            var tiers = new SortedDictionary<int, List<SkillData>>();
            foreach (var skill in allSkills)
            {
                if (skill.Discipline != discipline) continue;
                if (!tiers.TryGetValue(skill.Tier, out var row)) tiers[skill.Tier] = row = new List<SkillData>();
                row.Add(skill);
            }
            nodeCentres.Clear();
            float y = area.y + 110f;
            foreach (var row in tiers.Values)
            {
                for (int i = 0; i < row.Count; i++)
                    nodeCentres[row[i]] = new Vector2(area.x + area.width * (i + 1f) / (row.Count + 1f), y);
                y += TierSpacing;
            }
            if (selectedSkill == null || !nodeCentres.ContainsKey(selectedSkill))
                selectedSkill = nodeCentres.Count > 0 ? FirstAvailable(discipline) : null;

            // Links first, under the nodes: prerequisites solid (gold once learned); otherwise a faint link to the nearest
            // skill one tier up, so the tree reads as one.
            var previous = new List<SkillData>();
            foreach (var row in tiers.Values)
            {
                foreach (var skill in row)
                {
                    var to = nodeCentres[skill] - new Vector2(0f, NodeSize * 0.5f + 4f);
                    bool linked = false;
                    if (skill.Prerequisites != null)
                        foreach (var pre in skill.Prerequisites)
                        {
                            if (pre == null || !nodeCentres.TryGetValue(pre, out var from)) continue;
                            bool learned = skills.IsUnlocked(pre);
                            UITheme.Line(from + new Vector2(0f, LinkDrop), to, learned ? 3f : 2f,
                                learned ? UITheme.InkGold : new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.55f));
                            linked = true;
                        }
                    if (!linked && previous.Count > 0)
                    {
                        var nearest = previous[0];
                        foreach (var p in previous)
                            if (Mathf.Abs(nodeCentres[p].x - nodeCentres[skill].x) < Mathf.Abs(nodeCentres[nearest].x - nodeCentres[skill].x)) nearest = p;
                        UITheme.Line(nodeCentres[nearest] + new Vector2(0f, LinkDrop), to, 1.5f, new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.22f));
                    }
                }
                previous = row;
            }

            foreach (var pair in nodeCentres)
            {
                var skill = pair.Key;
                var rect = new Rect(pair.Value.x - NodeSize * 0.5f, pair.Value.y - NodeSize * 0.5f, NodeSize, NodeSize);
                bool owned = skills.IsUnlocked(skill);
                var state = owned ? UITheme.NodeState.Learned : skills.WhyLocked(skill) == null ? UITheme.NodeState.Available : UITheme.NodeState.Locked;
                UITheme.DiamondNode(rect, state, skill == selectedSkill, Glyph(skill));
                nodeLabel.normal.textColor = state == UITheme.NodeState.Locked ? UITheme.MutedOnPaper : UITheme.Ink;
                // A parchment backing, so a link that skips a tier passes under the name instead of through it.
                var labelSize = nodeLabel.CalcSize(new GUIContent(skill.DisplayName));
                UITheme.Fill(new Rect(pair.Value.x - labelSize.x * 0.5f - 6f, rect.yMax + 5f, labelSize.x + 12f, labelSize.y + 2f), UITheme.Parchment);
                GUI.Label(new Rect(pair.Value.x - 95f, rect.yMax + 6f, 190f, 44f), skill.DisplayName, nodeLabel);
                if (UITheme.PaperClick(rect)) selectedSkill = skill;
            }

            if (nodeCentres.Count == 0)
                GUI.Label(new Rect(area.x, area.y + 70f, area.width, 30f), "No skills here yet.", UITheme.PaperMuted);
        }

        SkillData FirstAvailable(Discipline discipline)
        {
            SkillData first = null;
            foreach (var skill in allSkills)
            {
                if (skill.Discipline != discipline) continue;
                first ??= skill;
                if (!skills.IsUnlocked(skill) && skills.WhyLocked(skill) == null) return skill;
            }
            return first;
        }

        /// <summary>E / Q when the skill is on a slot, otherwise its initial.</summary>
        string Glyph(SkillData skill)
        {
            for (int s = 0; s < PlayerSkills.SlotCount; s++)
                if (skills.SlotSkill(s) == skill) return UITheme.KeyLabel(SlotKeys[s]);
            return string.IsNullOrEmpty(skill.DisplayName) ? "?" : skill.DisplayName.Substring(0, 1).ToUpperInvariant();
        }

        void DrawSkillDetails(Rect area)
        {
            UITheme.InkPanel(area);
            float x = area.x + 28f;
            float width = area.width - 56f;
            float y = area.y + 28f;
            var skill = selectedSkill;
            if (skill == null)
            {
                GUI.Label(new Rect(x, y, width, 40f), $"<color={UITheme.MutedOnInkHex}>Choose a skill.</color>", bodyStyle);
                return;
            }

            bool owned = skills.IsUnlocked(skill);
            string why = skills.WhyLocked(skill);
            GUI.Label(new Rect(x, y, width, 40f), UITheme.Spaced(skill.DisplayName), nameStyle);
            y += 44f;
            string kind = skill.Kind == SkillKind.Active ? $"Active · {skill.Cooldown:0} s cooldown" : "Passive";
            string moveset = skill.RequiredMoveset != null ? $" · {skill.RequiredMoveset.DisplayName}" : string.Empty;
            GUI.Label(new Rect(x, y, width, 24f), $"{skill.Discipline} · tier {skill.Tier} · {kind}{moveset}", metaStyle);
            y += 32f;
            string status = owned ? $"<color={UITheme.GoodOnInkHex}>Learned</color>"
                : why == null ? $"<color={UITheme.InkGoldHex}>Ready to learn · costs {skill.Cost} point{(skill.Cost > 1 ? "s" : "")}</color>"
                : $"<color={UITheme.VermilionHex}>{why}</color>";
            GUI.Label(new Rect(x, y, width, 26f), status, bodyStyle);
            y += 38f;
            UITheme.Fill(new Rect(x, y, width, 1f), new Color(1f, 1f, 1f, 0.18f));
            y += 18f;

            string effects = skill.Kind == SkillKind.Passive ? Describe(skill.PassiveModifiers)
                : skill.Buff != null && skill.Buff.Length > 0 ? $"Buff ({skill.BuffDuration:0} s): {Describe(skill.Buff)}" : string.Empty;
            string body = $"<color={UITheme.MutedOnInkHex}>{skill.Description}</color>";
            if (effects.Length > 0) body += $"\n\n<color={UITheme.GoodOnInkHex}>{effects}</color>";
            float bodyHeight = bodyStyle.CalcHeight(new GUIContent(body), width);
            GUI.Label(new Rect(x, y, width, bodyHeight), body, bodyStyle);

            var bottom = new Rect(x, area.yMax - 82f, width, 52f);
            if (!owned)
            {
                if (UITheme.BrushButton(bottom, $"Learn  ({skill.Cost} pt)", why == null, light: true)) skills.Unlock(skill);
            }
            else if (skill.Kind == SkillKind.Active)
            {
                GUI.Label(new Rect(x, bottom.y - 34f, width, 26f), "Use it from a skill slot:", metaStyle);
                float half = width * 0.5f - 6f;
                for (int s = 0; s < PlayerSkills.SlotCount; s++)
                {
                    bool equipped = skills.SlotSkill(s) == skill;
                    var rect = new Rect(x + s * (half + 12f), bottom.y, half, bottom.height);
                    if (UITheme.BrushButton(rect, equipped ? $"On {UITheme.KeyLabel(SlotKeys[s])}" : $"Set {UITheme.KeyLabel(SlotKeys[s])}", !equipped, light: true))
                        skills.EquipToSlot(skill, s);
                }
            }
        }

        // ---------- Gear ----------

        int SlotCount => PlayerEquipment.WeaponSlots + PlayerEquipment.ArmorSlotOrder.Count;

        EquipmentData ItemInSlot(int slot) => equipment == null ? null
            : slot < PlayerEquipment.WeaponSlots ? equipment.WeaponAt(slot) : equipment.ArmorAt(PlayerEquipment.ArmorSlotOrder[slot - PlayerEquipment.WeaponSlots]);

        string SlotName(int slot) => slot < PlayerEquipment.WeaponSlots ? $"Weapon {slot + 1}" : PlayerEquipment.ArmorSlotOrder[slot - PlayerEquipment.WeaponSlots].ToString();

        bool Fits(EquipmentData gear, int slot) => slot < PlayerEquipment.WeaponSlots
            ? gear.Slot == EquipSlot.Weapon
            : gear.Slot == PlayerEquipment.ArmorSlotOrder[slot - PlayerEquipment.WeaponSlots];

        bool Unequip(int slot) => slot < PlayerEquipment.WeaponSlots
            ? equipment.UnequipWeapon(slot)
            : equipment.UnequipArmor(PlayerEquipment.ArmorSlotOrder[slot - PlayerEquipment.WeaponSlots]);

        void DrawGear(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 40f), UITheme.Spaced("Gear"), UITheme.InkHeader);
            UITheme.Fill(new Rect(area.x, area.y + 48f, area.width, 1f), new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.4f));
            if (equipment == null) return;
            selectedSlot = Mathf.Clamp(selectedSlot, 0, SlotCount - 1);

            // The character, standing in the middle (front view, idling).
            var figure = new Rect(area.center.x - 120f, area.y + 110f, 240f, 320f);
            DrawFigure(figure);
            if (appearance != null)
                GUI.Label(new Rect(figure.x - 40f, figure.yMax + 4f, figure.width + 80f, 30f), UITheme.Spaced(appearance.PlayerName), figureName ??= new GUIStyle(UITheme.InkHeader) { alignment = TextAnchor.MiddleCenter, fontSize = 22 });

            // Weapons and the accessory on the left, head / body / legs on the right.
            const float slotWidth = 300f, slotHeight = 92f, slotGap = 22f;
            float top = area.y + 96f;
            var leftSlots = new List<int> { 0, 1 };
            var rightSlots = new List<int>();
            for (int s = PlayerEquipment.WeaponSlots; s < SlotCount; s++)
            {
                if (PlayerEquipment.ArmorSlotOrder[s - PlayerEquipment.WeaponSlots] == EquipSlot.Accessory) leftSlots.Add(s);
                else rightSlots.Add(s);
            }
            for (int i = 0; i < leftSlots.Count; i++)
                DrawSlot(new Rect(area.x, top + i * (slotHeight + slotGap), slotWidth, slotHeight), leftSlots[i]);
            for (int i = 0; i < rightSlots.Count; i++)
                DrawSlot(new Rect(area.xMax - slotWidth, top + i * (slotHeight + slotGap), slotWidth, slotHeight), rightSlots[i]);
        }

        void DrawSlot(Rect rect, int slot)
        {
            var item = ItemInSlot(slot);
            bool hovered = rect.Contains(Event.current.mousePosition);
            UITheme.PaperSlot(rect, slot == selectedSlot, hovered);
            var icon = new Rect(rect.x + 16f, rect.y + 16f, rect.height - 32f, rect.height - 32f);
            if (item != null) UITheme.ItemIcon(icon, item);
            else UITheme.Fill(icon, new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.08f));
            float textX = icon.xMax + 14f;
            string label = SlotName(slot);
            if (slot < PlayerEquipment.WeaponSlots && slot == equipment.ActiveWeaponIndex) label += $"  <color={UITheme.InkGoldDarkHex}>· in hand</color>";
            GUI.Label(new Rect(textX, rect.y + 16f, rect.xMax - textX - 12f, 22f), label, slotLabel);
            string name = item != null ? UITheme.Fit(item.DisplayName, slotName, rect.xMax - textX - 12f) : $"<color={UITheme.MutedOnPaperHex}>Empty</color>";
            GUI.Label(new Rect(textX, rect.y + 42f, rect.xMax - textX - 12f, 30f), name, slotName);
            if (UITheme.PaperClick(rect)) selectedSlot = slot;
        }

        /// <summary>The idle animation's front frame from the player's own sheet (the customised look when there is one).</summary>
        void DrawFigure(Rect rect)
        {
            var texture = appearance != null && appearance.CurrentSheet != null ? appearance.CurrentSheet
                : sprite != null && sprite.Sheet != null ? sprite.Sheet.Texture : null;
            if (texture == null) return;
            var cell = sprite != null && sprite.Sheet != null ? sprite.Sheet.CellSize : new Vector2Int(CharacterSpriteBuilder.CellWidth, CharacterSpriteBuilder.CellHeight);
            var idle = CharacterSpriteBuilder.Layout[0];
            int frame = idle.Start + (int)(Time.unscaledTime * idle.Fps) % idle.Count;
            float w = texture.width, h = texture.height;
            var uv = new Rect(frame * cell.x / w, (h - cell.y) / h, cell.x / w, cell.y / h);
            UITheme.DrawIcon(new Rect(rect.center.x - 90f, rect.yMax - 26f, 180f, 34f), UITheme.CircleIcon, new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.18f));
            GUI.DrawTextureWithTexCoords(rect, texture, uv);
        }

        void DrawGearDetails(Rect area)
        {
            UITheme.InkPanel(area);
            if (equipment == null) return;
            float x = area.x + 28f;
            float width = area.width - 56f;
            float y = area.y + 26f;

            // The selected slot.
            var item = ItemInSlot(selectedSlot);
            GUI.Label(new Rect(x, y, width, 24f), UITheme.Spaced(SlotName(selectedSlot)), sectionStyle);
            y += 30f;
            if (item != null)
            {
                GUI.Label(new Rect(x, y, width - 170f, 30f), UITheme.Fit(item.DisplayName, nameStyle, width - 170f), nameStyle);
                if (UITheme.BrushButton(new Rect(area.xMax - 28f - 160f, y - 6f, 160f, 44f), "Unequip", true, light: true) && !Unequip(selectedSlot))
                    EventBus<HudMessageEvent>.Raise(new HudMessageEvent("Bag is full"));
                y += 34f;
                string bonus = Describe(item.Modifiers);
                string wear = equipment.TryGetComponent(out GearCondition condition) ? condition.ConditionText(item) : string.Empty;
                string line = bonus.Length > 0 ? $"<color={UITheme.GoodOnInkHex}>{bonus}</color>" : "No stat bonuses";
                GUI.Label(new Rect(x, y, width, 24f), wear.Length > 0 ? $"{line}   {wear}" : line, metaStyle);
            }
            else
            {
                GUI.Label(new Rect(x, y, width, 30f), $"<color={UITheme.MutedOnInkHex}>Nothing equipped</color>", bodyStyle);
            }
            y += 44f;

            // Spare gear in the bag that fits this slot.
            GUI.Label(new Rect(x, y, width, 24f), UITheme.Spaced("In your bag"), sectionStyle);
            y += 32f;
            spareGear.Clear();
            if (inventory != null)
                for (int i = 0; i < inventory.SlotCount; i++)
                    if (!inventory[i].IsEmpty && inventory[i].Item is EquipmentData gear && Fits(gear, selectedSlot) && !spareGear.Contains(gear)) spareGear.Add(gear);
            if (spareGear.Count == 0)
            {
                GUI.Label(new Rect(x, y, width, 26f), $"<color={UITheme.MutedOnInkHex}>Nothing that fits. Merchants and enemies have some.</color>", metaStyle);
                y += 34f;
            }
            foreach (var gear in spareGear)
            {
                if (y > area.yMax - 300f) break; // keep room for the stats
                UITheme.ItemIcon(new Rect(x, y + 4f, 36f, 36f), gear);
                GUI.Label(new Rect(x + 46f, y, width - 200f, 22f), UITheme.Fit(gear.DisplayName, bodyStyle, width - 200f), bodyStyle);
                string bonus = Describe(gear.Modifiers);
                GUI.Label(new Rect(x + 46f, y + 22f, width - 200f, 20f), bonus.Length > 0 ? $"<color={UITheme.GoodOnInkHex}>{UITheme.Fit(bonus, metaStyle, width - 200f)}</color>" : "No bonuses", metaStyle);
                if (UITheme.BrushButton(new Rect(area.xMax - 28f - 140f, y + 2f, 140f, 42f), "Equip", true, light: true)) equipment.Equip(gear);
                y += 54f;
            }

            // Stats.
            float statsTop = area.yMax - 26f - 5 * 30f - 36f;
            GUI.Label(new Rect(x, statsTop, width, 24f), UITheme.Spaced("Stats"), sectionStyle);
            if (stats == null) return;
            float column = (width - 24f) * 0.5f;
            for (int i = 0; i < ShownStats.Length; i++)
            {
                var stat = ShownStats[i];
                float value = stats.Get(stat);
                string text = stat is StatType.ExtraYieldChance or StatType.SeedSaveChance ? $"{value * 100f:0}%" : $"{value:0.#}";
                float sx = x + i % 2 * (column + 24f);
                float sy = statsTop + 34f + i / 2 * 30f;
                GUI.Label(new Rect(sx, sy, column, 26f), StatNames.Of(stat), statLabel);
                GUI.Label(new Rect(sx, sy, column, 26f), text, statValue);
            }
        }

        // ---------- Helpers ----------

        void EnsureStyles()
        {
            if (nodeLabel != null && nodeLabel.font == UITheme.PaperBody.font) return;
            nodeLabel = new GUIStyle(UITheme.PaperBody) { alignment = TextAnchor.UpperCenter, fontSize = UITheme.PaperMuted.fontSize };
            nameStyle = new GUIStyle(UITheme.InkHeader) { normal = { textColor = UITheme.OffWhite } };
            metaStyle = new GUIStyle(UITheme.InkSmall) { normal = { textColor = UITheme.MutedOnInk } };
            bodyStyle = new GUIStyle(UITheme.InkBody);
            headerRight = new GUIStyle(UITheme.PaperMuted) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            slotLabel = new GUIStyle(UITheme.PaperMuted) { wordWrap = false };
            slotName = new GUIStyle(UITheme.PaperBody) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            statLabel = new GUIStyle(UITheme.InkSmall) { normal = { textColor = UITheme.MutedOnInk } };
            statValue = new GUIStyle(UITheme.InkSmall) { alignment = TextAnchor.MiddleRight };
            sectionStyle = new GUIStyle(UITheme.InkSmall) { normal = { textColor = UITheme.InkGold } };
        }

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
