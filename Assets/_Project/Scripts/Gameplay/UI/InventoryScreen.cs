using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The Bag tab of the game menu (Tab / I), in the ink-and-parchment style:
    /// - category cards on the left (All, Food, Materials, Seeds &amp; crops, Gear)
    /// - paper slots in the middle (selected = vermilion frame; hover shows a tooltip)
    /// - an ink panel on the right with the selected item's details and its action (Use / Equip)
    /// Right-click uses or equips directly. GameMenu opens it and draws the frame.
    /// HUD elements (gold, quick item, notifications) live in GameHud.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour, IGameMenuTab
    {
        enum Category { All, Food, Materials, SeedsAndCrops, Gear }

        static readonly string[] CategoryNames = { "All", "Food", "Materials", "Seeds & crops", "Gear" };
        const int Columns = 6;
        const float SlotSize = 88f;
        const float Gap = 10f;
        const float CardsWidth = 250f;
        const float DetailsWidth = 460f;

        Inventory inventory;
        QuickItemUser itemUser;
        PlayerEquipment equipment;
        Category category;
        int selected = -1;
        string message;
        bool messageIsError;
        float messageTime;
        readonly List<int> shown = new();
        GUIStyle nameStyle, metaStyle, bodyStyle, headerRight;

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                inventory = player.GetComponent<Inventory>();
                itemUser = player.GetComponent<QuickItemUser>();
                equipment = player.GetComponent<PlayerEquipment>();
            }
        }

        // ---------- Game menu tab ----------

        public bool CanOpen => inventory != null;
        public string FooterTip => "Right-click an item to <color=#c9a86a>use or equip</color> it";
        public (string key, string label)[] KeyHints => new[] { ("Right-click", "Use / equip") };
        public bool HasSubView => false;
        public void CloseSubView() { }

        public void OnTabOpened()
        {
            message = null;
            if (selected >= inventory.SlotCount || (selected >= 0 && inventory[selected].IsEmpty)) selected = -1;
        }

        public void DrawTab(Rect bounds)
        {
            if (inventory == null) return;
            EnsureStyles();

            float gridWidth = Columns * (SlotSize + Gap) - Gap;
            float total = CardsWidth + 36f + gridWidth + 36f + DetailsWidth;
            float x = bounds.x + Mathf.Max(0f, (bounds.width - total) * 0.5f);

            DrawCategories(new Rect(x, bounds.y, CardsWidth, bounds.height));
            x += CardsWidth + 36f;
            DrawGrid(new Rect(x, bounds.y, gridWidth, bounds.height));
            x += gridWidth + 36f;
            DrawDetails(new Rect(x, bounds.y, DetailsWidth, bounds.height));
        }

        // ---------- Categories ----------

        static bool InCategory(ItemData item, Category c) => c switch
        {
            Category.Food => item.Category == ItemCategory.Consumable,
            Category.Materials => item.Category is ItemCategory.Material or ItemCategory.Quest,
            Category.SeedsAndCrops => item.Category is ItemCategory.Seed or ItemCategory.Crop,
            Category.Gear => item.Category == ItemCategory.Equipment,
            _ => true,
        };

        int CountIn(Category c)
        {
            int n = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
                if (!inventory[i].IsEmpty && InCategory(inventory[i].Item, c)) n++;
            return n;
        }

        void DrawCategories(Rect area)
        {
            float y = area.y;
            for (int i = 0; i < CategoryNames.Length; i++)
            {
                var c = (Category)i;
                if (UITheme.FilterCard(new Rect(area.x, y, area.width, 62f), CategoryNames[i], CountIn(c).ToString(), null, category == c) && category != c)
                {
                    category = c;
                    if (selected >= 0 && !inventory[selected].IsEmpty && !InCategory(inventory[selected].Item, c)) selected = -1;
                }
                y += 72f;
            }
        }

        // ---------- Slots ----------

        void DrawGrid(Rect area)
        {
            int used = CountIn(Category.All);
            GUI.Label(new Rect(area.x, area.y, area.width, 40f), UITheme.Spaced("Bag"), UITheme.InkHeader);
            GUI.Label(new Rect(area.x, area.y, area.width, 40f),
                $"{used} / {inventory.SlotCount} slots   ·   <color={UITheme.InkGoldDarkHex}>{inventory.Gold} gold</color>", headerRight);
            UITheme.Fill(new Rect(area.x, area.y + 48f, area.width, 1f), new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.4f));

            // All: every slot (empty ones too, so the bag's size is visible). A category: just its items, packed.
            shown.Clear();
            for (int i = 0; i < inventory.SlotCount; i++)
                if (category == Category.All || (!inventory[i].IsEmpty && InCategory(inventory[i].Item, category))) shown.Add(i);

            var top = new Vector2(area.x, area.y + 66f);
            var mouse = Event.current.mousePosition;
            int hovered = -1;
            for (int n = 0; n < shown.Count; n++)
            {
                int i = shown[n];
                var rect = new Rect(top.x + n % Columns * (SlotSize + Gap), top.y + n / Columns * (SlotSize + Gap), SlotSize, SlotSize);
                var stack = inventory[i];
                bool isHovered = rect.Contains(mouse);
                if (isHovered) hovered = i;

                UITheme.PaperSlot(rect, i == selected && !stack.IsEmpty, isHovered && !stack.IsEmpty);
                if (!stack.IsEmpty)
                {
                    UITheme.ItemIcon(new Rect(rect.x + 14f, rect.y + 12f, SlotSize - 28f, SlotSize - 28f), stack.Item);
                    if (stack.Count > 1) UITheme.CountChip(rect, stack.Count);
                }

                if (Event.current.type == EventType.MouseDown && isHovered)
                {
                    if (Event.current.button == 1 && !stack.IsEmpty) UsePrimary(stack.Item);
                    else if (Event.current.button == 0) selected = stack.IsEmpty ? -1 : i;
                    Event.current.Use();
                }
            }

            if (shown.Count == 0)
                GUI.Label(new Rect(top.x, top.y, area.width, 60f), $"Nothing in {CategoryNames[(int)category].ToLowerInvariant()} yet.", UITheme.PaperMuted);

            if (hovered >= 0 && hovered != selected && !inventory[hovered].IsEmpty)
                UITheme.InkTooltip($"{inventory[hovered].Item.DisplayName}\n<color={UITheme.MutedOnInkHex}>{CategoryLabel(inventory[hovered].Item)}</color>", 240f);
        }

        // ---------- Details ----------

        void DrawDetails(Rect area)
        {
            UITheme.InkPanel(area);
            float x = area.x + 26f;
            float width = area.width - 52f;
            float y = area.y + 26f;

            if (selected < 0 || selected >= inventory.SlotCount || inventory[selected].IsEmpty)
            {
                selected = -1;
                GUI.Label(new Rect(x, y, width, 60f), $"<color={UITheme.MutedOnInkHex}>Choose an item to see its details.</color>", bodyStyle);
                DrawMessage(area);
                return;
            }

            var stack = inventory[selected];
            var item = stack.Item;
            UITheme.ItemIcon(new Rect(area.center.x - 56f, y, 112f, 112f), item);
            y += 128f;
            GUI.Label(new Rect(x, y, width, 36f), UITheme.Spaced(item.DisplayName), nameStyle);
            y += 38f;
            GUI.Label(new Rect(x, y, width, 22f), $"{CategoryLabel(item)}  ·  ×{stack.Count}  ·  worth {item.BaseValue}g each", metaStyle);
            y += 34f;
            UITheme.Fill(new Rect(x, y, width, 1f), new Color(1f, 1f, 1f, 0.18f));
            y += 16f;

            string body = string.IsNullOrEmpty(item.Description) ? string.Empty : $"<i><color={UITheme.MutedOnInkHex}>{item.Description}</color></i>";
            string effects = item switch
            {
                ConsumableData c => RestoresText(c),
                EquipmentData e => $"<color={UITheme.InkGoldHex}>{e.Slot}</color>   {Effects(e)}",
                _ when CropCatalog.IsPlantable(item) => "Plant it on tilled soil (F at your field).",
                _ => string.Empty,
            };
            if (effects.Length > 0) body += (body.Length > 0 ? "\n\n" : string.Empty) + effects;
            float bodyHeight = bodyStyle.CalcHeight(new GUIContent(body), width);
            GUI.Label(new Rect(x, y, width, bodyHeight), body, bodyStyle);

            // The action sits at the bottom of the panel, where the eye ends up after reading.
            var button = new Rect(x, area.yMax - 84f, width, 52f);
            switch (item)
            {
                case ConsumableData consumable:
                    if (UITheme.BrushButton(button, "Use", itemUser != null, light: true))
                        Report(itemUser.TryUse(consumable), $"Used {item.DisplayName}.", "Can't use that right now.");
                    GUI.Label(new Rect(x, button.yMax + 4f, width, 22f), "R eats the first food in your bag.", metaStyle);
                    break;
                case EquipmentData gear:
                    if (UITheme.BrushButton(button, "Equip", equipment != null, light: true))
                        Report(equipment.Equip(gear), $"Equipped {item.DisplayName}.", "Can't equip that.");
                    break;
            }
            DrawMessage(area);
        }

        static string CategoryLabel(ItemData item) => item.Category switch
        {
            ItemCategory.Consumable => "Food",
            ItemCategory.Seed => "Seeds",
            ItemCategory.Crop => "Crop",
            ItemCategory.Equipment => "Gear",
            ItemCategory.Quest => "Quest item",
            _ => "Material",
        };

        /// <summary>"Restores 30 health." / "... and 20 stamina." — only what the item actually restores.</summary>
        static string RestoresText(ConsumableData c)
        {
            string health = c.Heal > 0f ? $"<color={UITheme.GoodOnInkHex}>{c.Heal:0} health</color>" : null;
            string stamina = c.Stamina > 0f ? $"<color={UITheme.GoodOnInkHex}>{c.Stamina:0} stamina</color>" : null;
            if (health == null && stamina == null) return string.Empty;
            return $"Restores {(health != null && stamina != null ? $"{health} and {stamina}" : health ?? stamina)}.";
        }

        static string Effects(EquipmentData gear)
        {
            string text = CharacterScreen.Describe(gear.Modifiers);
            return string.IsNullOrEmpty(text) ? $"<color={UITheme.MutedOnInkHex}>No stat bonuses</color>" : $"<color={UITheme.GoodOnInkHex}>{text}</color>";
        }

        void UsePrimary(ItemData item)
        {
            if (item is ConsumableData consumable && itemUser != null)
                Report(itemUser.TryUse(consumable), $"Used {item.DisplayName}.", "Can't use that right now.");
            else if (item is EquipmentData gear && equipment != null)
                Report(equipment.Equip(gear), $"Equipped {item.DisplayName}.", "Can't equip that.");
        }

        void Report(bool success, string ok, string failure)
        {
            message = success ? ok : failure;
            messageIsError = !success;
            messageTime = Time.unscaledTime;
        }

        void DrawMessage(Rect area)
        {
            if (message == null || Time.unscaledTime - messageTime > 3f) return;
            GUI.Label(new Rect(area.x + 26f, area.yMax - 118f, area.width - 52f, 24f),
                $"<color={(messageIsError ? UITheme.VermilionHex : UITheme.GoodOnInkHex)}>{message}</color>", metaStyle);
        }

        void EnsureStyles()
        {
            if (nameStyle != null && nameStyle.font == UITheme.InkHeader.font) return;
            nameStyle = new GUIStyle(UITheme.InkHeader) { alignment = TextAnchor.MiddleCenter, normal = { textColor = UITheme.OffWhite } };
            metaStyle = new GUIStyle(UITheme.InkSmall) { alignment = TextAnchor.MiddleCenter, normal = { textColor = UITheme.MutedOnInk } };
            bodyStyle = new GUIStyle(UITheme.InkBody);
            headerRight = new GUIStyle(UITheme.PaperMuted) { alignment = TextAnchor.MiddleRight, wordWrap = false };
        }
    }
}
