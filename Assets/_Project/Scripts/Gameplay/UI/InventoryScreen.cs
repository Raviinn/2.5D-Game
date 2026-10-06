using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Prototype inventory (IMGUI, Tab / I, pauses the game): slot grid on the left, details of the
    /// selected item on the right with its actions (Use / Equip). Right-click uses or equips directly.
    /// HUD elements (gold, quick item, notifications) live in GameHud.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        const int Columns = 6;
        const float SlotSize = 72f;
        const float Gap = 8f;

        Inventory inventory;
        QuickItemUser itemUser;
        PlayerEquipment equipment;
        GameStateService state;
        InputAction openAction;
        bool isOpen; // other menus (e.g. shops) share the InGameMenu state
        int selected = -1;
        string message;
        bool messageIsError;
        float messageTime;

        void OnEnable() => EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        void OnDisable() => EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                inventory = player.GetComponent<Inventory>();
                itemUser = player.GetComponent<QuickItemUser>();
                equipment = player.GetComponent<PlayerEquipment>();
            }
            state = Services.Get<GameStateService>();
            openAction = Services.Get<InputService>().Inventory;
        }

        void Update()
        {
            // Closing is handled by the UI map's Resume action (Tab / I / Esc / Select).
            if (inventory != null && openAction.WasPressedThisFrame() && state.Current == GameState.Playing)
            {
                state.SetState(GameState.InGameMenu);
                isOpen = true;
                message = null;
                if (selected >= inventory.SlotCount || (selected >= 0 && inventory[selected].IsEmpty)) selected = -1;
            }
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) isOpen = false;
        }

        void OnGUI()
        {
            if (inventory == null || !isOpen || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();

            int used = 0;
            for (int i = 0; i < inventory.SlotCount; i++) if (!inventory[i].IsEmpty) used++;

            int rows = Mathf.CeilToInt(inventory.SlotCount / (float)Columns);
            float gridWidth = Columns * (SlotSize + Gap) - Gap;
            float gridHeight = rows * (SlotSize + Gap) - Gap;
            const float detailsWidth = 360f;

            var area = UITheme.Window(gridWidth + detailsWidth + 84f, gridHeight + 170f, "Inventory",
                $"<color={UITheme.GoldHex}>{inventory.Gold} gold</color>   <color={UITheme.MutedHex}>·   {used}/{inventory.SlotCount} slots</color>",
                $"<color={UITheme.MutedHex}>Click: select   ·   Right-click: use / equip   ·   Tab / Esc: close</color>");

            DrawGrid(new Rect(area.x + 4f, area.y + 6f, gridWidth, gridHeight));
            DrawDetails(new Rect(area.x + gridWidth + 28f, area.y + 6f, detailsWidth, area.height - 10f));
        }

        void DrawGrid(Rect area)
        {
            var mouse = Event.current.mousePosition;
            int hovered = -1;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var rect = new Rect(area.x + i % Columns * (SlotSize + Gap), area.y + i / Columns * (SlotSize + Gap), SlotSize, SlotSize);
                var stack = inventory[i];
                bool isHovered = rect.Contains(mouse);
                if (isHovered) hovered = i;

                UITheme.Slot(rect, i == selected && !stack.IsEmpty, isHovered && !stack.IsEmpty);
                if (!stack.IsEmpty)
                {
                    UITheme.ItemIcon(new Rect(rect.x + 12f, rect.y + 12f, SlotSize - 24f, SlotSize - 24f), stack.Item);
                    if (stack.Count > 1)
                    {
                        // Count badge in the corner, on its own dark chip so it reads over any icon colour.
                        string count = stack.Count.ToString();
                        float badgeWidth = UITheme.Small.CalcSize(new GUIContent(count)).x + 10f;
                        var badge = new Rect(rect.xMax - badgeWidth - 3f, rect.yMax - 21f, badgeWidth, 18f);
                        UITheme.Fill(badge, new Color(0f, 0f, 0f, 0.75f));
                        GUI.Label(badge, $"<b>{count}</b>", UITheme.SmallCenter);
                    }
                }

                if (Event.current.type == EventType.MouseDown && isHovered)
                {
                    if (Event.current.button == 1 && !stack.IsEmpty) UsePrimary(stack.Item);
                    else if (Event.current.button == 0) selected = stack.IsEmpty ? -1 : i;
                    Event.current.Use();
                }
            }

            if (hovered >= 0 && hovered != selected && !inventory[hovered].IsEmpty)
                UITheme.Tooltip($"<b>{inventory[hovered].Item.DisplayName}</b>\n<color={UITheme.MutedHex}>{inventory[hovered].Item.Category}</color>", 220f);
        }

        void DrawDetails(Rect area)
        {
            UITheme.Inset(area);
            float x = area.x + 18f;
            float width = area.width - 36f;
            float y = area.y + 18f;

            if (selected < 0 || selected >= inventory.SlotCount || inventory[selected].IsEmpty)
            {
                selected = -1;
                GUI.Label(new Rect(x, y, width, 60f), $"<color={UITheme.MutedHex}>Select an item to see its details.</color>", UITheme.Body);
                DrawMessage(area);
                return;
            }

            var stack = inventory[selected];
            var item = stack.Item;
            UITheme.ItemIcon(new Rect(x, y, 64f, 64f), item);
            GUI.Label(new Rect(x + 78f, y + 2f, width - 78f, 28f), item.DisplayName, UITheme.Header);
            GUI.Label(new Rect(x + 78f, y + 32f, width - 78f, 22f),
                $"<color={UITheme.MutedHex}>{item.Category}  ·  ×{stack.Count}  ·  worth {item.BaseValue}g each</color>", UITheme.Small);
            y += 84f;

            string body = string.IsNullOrEmpty(item.Description) ? string.Empty : $"<i>{item.Description}</i>";
            string effects = item switch
            {
                ConsumableData c => RestoresText(c),
                EquipmentData e => $"<b>{e.Slot}</b>  {Effects(e)}",
                _ when CropCatalog.IsPlantable(item) => "Plant it on tilled soil (F at your field).",
                _ => string.Empty,
            };
            if (effects.Length > 0) body += (body.Length > 0 ? "\n\n" : string.Empty) + effects;
            float bodyHeight = UITheme.Body.CalcHeight(new GUIContent(body), width);
            GUI.Label(new Rect(x, y, width, bodyHeight), body, UITheme.Body);

            // Actions sit at the bottom of the panel, where the eye ends up after reading.
            var button = new Rect(x, area.yMax - 76f, width, 44f);
            switch (item)
            {
                case ConsumableData consumable:
                    if (UITheme.Button(button, "Use", itemUser != null, primary: true))
                        Report(itemUser.TryUse(consumable), $"Used {item.DisplayName}.", "Can't use that right now.");
                    GUI.Label(new Rect(x, button.yMax + 2f, width, 20f), $"<color={UITheme.MutedHex}>Tip: R eats the first food in your bag.</color>", UITheme.Small);
                    break;
                case EquipmentData gear:
                    if (UITheme.Button(button, "Equip", equipment != null, primary: true))
                        Report(equipment.Equip(gear), $"Equipped {item.DisplayName}.", "Can't equip that.");
                    break;
            }
            DrawMessage(area);
        }

        /// <summary>"Restores 30 health." / "... and 20 stamina." — only what the item actually restores.</summary>
        static string RestoresText(ConsumableData c)
        {
            string health = c.Heal > 0f ? $"<color={UITheme.GoodHex}>{c.Heal:0} health</color>" : null;
            string stamina = c.Stamina > 0f ? $"<color={UITheme.GoodHex}>{c.Stamina:0} stamina</color>" : null;
            if (health == null && stamina == null) return string.Empty;
            return $"Restores {(health != null && stamina != null ? $"{health} and {stamina}" : health ?? stamina)}.";
        }

        static string Effects(EquipmentData gear)
        {
            string text = CharacterScreen.Describe(gear.Modifiers);
            return string.IsNullOrEmpty(text) ? $"<color={UITheme.MutedHex}>No stat bonuses</color>" : $"<color={UITheme.GoodHex}>{text}</color>";
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
            GUI.Label(new Rect(area.x + 18f, area.yMax - 104f, area.width - 36f, 22f),
                $"<color={(messageIsError ? UITheme.BadHex : UITheme.GoodHex)}>{message}</color>", UITheme.Small);
        }
    }
}
