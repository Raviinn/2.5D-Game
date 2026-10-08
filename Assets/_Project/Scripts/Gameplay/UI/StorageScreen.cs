using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The storage chest window, on parchment: your bag on the left (Store / All), the chest on the right (Take / All),
    /// and one button to store every crop, seed and material at once. Opened by a StorageChest; the game is paused
    /// (InGameMenu) while it's open. Esc leaves.
    /// </summary>
    public sealed class StorageScreen : MonoBehaviour
    {
        const float RowHeight = 78f;
        const float MessageLifetime = 3f;

        StorageChest chest;
        Inventory bag;
        GameStateService state;
        Vector2 bagScroll, chestScroll;
        readonly List<ItemData> items = new();
        string message;
        bool messageIsError;
        float messageTime;
        GUIStyle rowName, rowDetail, footerStyle;

        /// <summary>The chest whose window is open (null when closed).</summary>
        public StorageChest Chest => chest;

        void OnEnable()
        {
            EventBus<StorageOpenedEvent>.Subscribe(OnOpened);
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus<StorageOpenedEvent>.Unsubscribe(OnOpened);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) bag = player.GetComponent<Inventory>();
            state = Services.Get<GameStateService>();
        }

        void OnOpened(StorageOpenedEvent evt)
        {
            chest = evt.Chest;
            message = null;
            bagScroll = chestScroll = Vector2.zero;
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) chest = null;
        }

        void OnGUI()
        {
            if (chest == null || bag == null || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();
            EnsureStyles();

            var contents = chest.Contents;
            var area = UITheme.ParchmentWindow(1160f, 760f, chest.DisplayName,
                $"Bag {bag.UsedSlots}/{bag.SlotCount} slots   ·   Chest {contents.UsedSlots}/{contents.SlotCount} slots");

            float footer = 70f;
            float columnWidth = (area.width - 32f) * 0.5f;
            DrawColumn(new Rect(area.x, area.y, columnWidth, area.height - footer), "Bag", bag, contents, true, ref bagScroll);
            DrawColumn(new Rect(area.x + columnWidth + 32f, area.y, columnWidth, area.height - footer), "Chest", contents, bag, false, ref chestScroll);

            if (UITheme.BrushButton(new Rect(area.x, area.yMax - 52f, 420f, 48f), "Store crops, seeds & materials"))
            {
                int moved = chest.StoreGoods(bag);
                if (moved > 0) Show($"Stored {moved} item{(moved == 1 ? "" : "s")}.", false);
                else Show("Nothing to store (or the chest is full).", true);
            }
            string line = message != null && Time.unscaledTime - messageTime < MessageLifetime
                ? $"<color={(messageIsError ? UITheme.VermilionHex : UITheme.GoodOnPaperHex)}>{message}</color>"
                : "The workbench can use what's in here.";
            GUI.Label(new Rect(area.x + 440f, area.yMax - 48f, area.width - 660f, 40f), line, footerStyle);
            UITheme.KeyHints(area.xMax, area.yMax - 44f, false, ("Esc", "Leave"));
        }

        void DrawColumn(Rect area, string title, Inventory from, Inventory to, bool isBag, ref Vector2 scroll)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 34f), UITheme.Spaced(title), UITheme.InkHeader);

            // Distinct items, in slot order.
            items.Clear();
            for (int i = 0; i < from.SlotCount; i++)
            {
                var stack = from[i];
                if (!stack.IsEmpty && !items.Contains(stack.Item)) items.Add(stack.Item);
            }

            var view = new Rect(area.x, area.y + 44f, area.width, area.height - 44f);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(1, items.Count) * (RowHeight + 8f));
            scroll = UITheme.BeginScroll(view, scroll, content);
            if (items.Count == 0)
                GUI.Label(new Rect(4f, 4f, content.width - 8f, 48f), isBag ? "Your bag is empty." : "The chest is empty.", UITheme.PaperMuted);

            string one = isBag ? "Store" : "Take";
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = new Rect(0f, i * (RowHeight + 8f), content.width, RowHeight);
                int count = from.CountOf(item);
                UITheme.PaperSlot(row, false, row.Contains(Event.current.mousePosition));
                UITheme.ItemIcon(new Rect(row.x + 14f, row.y + 14f, 50f, 50f), item);
                GUI.Label(new Rect(row.x + 78f, row.y + 12f, row.width - 290f, 28f), $"{item.DisplayName}  <color={UITheme.MutedOnPaperHex}>×{count}</color>", rowName);
                GUI.Label(new Rect(row.x + 78f, row.y + 42f, row.width - 290f, 24f), CategoryName(item.Category), rowDetail);

                bool room = to.SpaceFor(item) > 0;
                if (UITheme.BrushButton(new Rect(row.xMax - 196f, row.y + 16f, 110f, 46f), one, room)) Move(from, to, item, 1, isBag);
                if (UITheme.BrushButton(new Rect(row.xMax - 78f, row.y + 16f, 66f, 46f), "All", room)) Move(from, to, item, count, isBag);
            }
            UITheme.EndScroll(ref scroll, view);
        }

        void Move(Inventory from, Inventory to, ItemData item, int count, bool storing)
        {
            int moved = StorageChest.Move(from, to, item, count);
            if (moved > 0) Show($"{(storing ? "Stored" : "Took")} {moved}× {item.DisplayName}.", false);
            else Show(storing ? "The chest is full." : "Your bag is full.", true);
        }

        static string CategoryName(ItemCategory category) => category switch
        {
            ItemCategory.Material => "Material",
            ItemCategory.Consumable => "Food & potions",
            ItemCategory.Seed => "Seeds",
            ItemCategory.Crop => "Crop",
            ItemCategory.Equipment => "Gear",
            _ => "Quest item",
        };

        void Show(string value, bool error)
        {
            message = value;
            messageIsError = error;
            messageTime = Time.unscaledTime;
        }

        void EnsureStyles()
        {
            if (rowName != null && rowName.font == UITheme.PaperBody.font) return;
            rowName = new GUIStyle(UITheme.PaperBody) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            rowDetail = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            footerStyle = new GUIStyle(UITheme.PaperMuted) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
        }
    }
}
