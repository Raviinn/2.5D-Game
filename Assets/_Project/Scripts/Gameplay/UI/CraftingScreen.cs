using System.Collections.Generic;
using System.Text;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The workbench window, on parchment: Alchemy / Cooking / Smithing cards on the left, that kind's recipes on the
    /// right with what each needs (what you have in red when it's short), what it does, and Craft / ×5 buttons.
    /// Opened by a Workbench; the game is paused (InGameMenu) while it's open. Esc leaves.
    /// Smithing also lists worn gear you own, to mend with Iron Scrap (Milestone 44).
    /// </summary>
    public sealed class CraftingScreen : MonoBehaviour
    {
        const float RowHeight = 104f;
        const float MessageLifetime = 3f;

        static readonly (CraftKind kind, string title, string subtitle)[] Kinds =
        {
            (CraftKind.Alchemy, "Alchemy", "Draughts and tonics"),
            (CraftKind.Cooking, "Cooking", "Meals that heal and fortify"),
            (CraftKind.Smithing, "Smithing", "Better gear from scrap"),
        };

        Workbench bench;
        Inventory inventory;
        PlayerEquipment equipment;
        GearCondition condition;
        ItemData scrap;
        GameStateService state;
        CraftKind kind;
        Vector2 scroll;
        readonly List<RecipeData> shown = new();
        readonly List<EquipmentData> worn = new();
        readonly StringBuilder text = new();
        string message;
        bool messageIsError;
        float messageTime;
        GUIStyle rowName, rowDetail, footerStyle;

        /// <summary>The bench whose window is open (null when closed).</summary>
        public Workbench Bench => bench;
        public CraftKind Kind => kind;

        void OnEnable()
        {
            EventBus<WorkbenchOpenedEvent>.Subscribe(OnOpened);
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus<WorkbenchOpenedEvent>.Unsubscribe(OnOpened);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                inventory = player.GetComponent<Inventory>();
                equipment = player.GetComponent<PlayerEquipment>();
                condition = player.GetComponent<GearCondition>();
            }
            state = Services.Get<GameStateService>();
            if (Services.TryGet(out GameDatabase database)) database.TryGet("item_ironscrap", out scrap);
        }

        void OnOpened(WorkbenchOpenedEvent evt)
        {
            bench = evt.Bench;
            message = null;
            scroll = Vector2.zero;
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) bench = null;
        }

        /// <summary>Shows one kind of recipe (the cards on the left).</summary>
        public void Select(CraftKind value)
        {
            kind = value;
            scroll = Vector2.zero;
        }

        void OnGUI()
        {
            if (bench == null || inventory == null || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();
            EnsureStyles();

            var area = UITheme.ParchmentWindow(1220f, 780f, "Workbench", "Brew, cook and smith with what you've gathered");
            float footer = 56f;
            float cardsWidth = 300f;

            float y = area.y;
            foreach (var (k, title, subtitle) in Kinds)
            {
                int ready = 0, total = 0;
                foreach (var recipe in bench.Recipes.Recipes)
                    if (recipe != null && recipe.Kind == k)
                    {
                        total++;
                        if (Crafting.Problem(recipe, inventory, equipment, bench.Storage) == null) ready++;
                    }
                string count = $"{ready}/{total}";
                if (UITheme.FilterCard(new Rect(area.x, y, cardsWidth, 86f), title, count, subtitle, kind == k)) Select(k);
                y += 98f;
            }
            GUI.Label(new Rect(area.x, y + 6f, cardsWidth, 120f),
                bench.Storage != null
                    ? "The number shows how many you can make right now. Ingredients come from your bag, then the chest. Gear you're wearing can be used, and an upgrade stays equipped."
                    : "The number shows how many you can make right now. Gear you're wearing can be used, and an upgrade stays equipped.",
                UITheme.PaperMuted);

            DrawRecipes(new Rect(area.x + cardsWidth + 32f, area.y, area.width - cardsWidth - 32f, area.height - footer));

            string line = message != null && Time.unscaledTime - messageTime < MessageLifetime
                ? $"<color={(messageIsError ? UITheme.VermilionHex : UITheme.GoodOnPaperHex)}>{message}</color>"
                : "Healroot and turnips come from your field; scrap and cloth from bandits.";
            GUI.Label(new Rect(area.x, area.yMax - 34f, area.width - 220f, 30f), line, footerStyle);
            UITheme.KeyHints(area.xMax, area.yMax - 36f, false, ("Esc", "Leave"));
        }

        void DrawRecipes(Rect area)
        {
            shown.Clear();
            foreach (var recipe in bench.Recipes.Recipes)
                if (recipe != null && recipe.Output != null && recipe.Kind == kind) shown.Add(recipe);

            worn.Clear();
            if (kind == CraftKind.Smithing && condition != null && scrap != null) worn.AddRange(condition.WornItems());
            float repairHeight = worn.Count > 0 ? 40f + worn.Count * (RepairRowHeight + 8f) + 18f : 0f;

            var view = area;
            var content = new Rect(0f, 0f, view.width - 18f, repairHeight + Mathf.Max(1, shown.Count) * (RowHeight + 10f));
            scroll = UITheme.BeginScroll(view, scroll, content);
            if (worn.Count > 0) DrawRepairs(content.width);
            if (shown.Count == 0) GUI.Label(new Rect(4f, repairHeight + 4f, content.width - 8f, 48f), "No recipes of this kind yet.", UITheme.PaperMuted);

            for (int i = 0; i < shown.Count; i++)
            {
                var recipe = shown[i];
                var row = new Rect(0f, repairHeight + i * (RowHeight + 10f), content.width, RowHeight);
                string problem = Crafting.Problem(recipe, inventory, equipment, bench.Storage);

                UITheme.PaperSlot(row, false, row.Contains(Event.current.mousePosition));
                UITheme.ItemIcon(new Rect(row.x + 14f, row.y + 18f, 64f, 64f), recipe.Output);
                float textWidth = row.width - 330f;
                GUI.Label(new Rect(row.x + 92f, row.y + 8f, textWidth, 30f),
                    $"{recipe.DisplayName}  <color={UITheme.MutedOnPaperHex}>· have {inventory.CountOf(recipe.Output)}</color>", rowName);
                GUI.Label(new Rect(row.x + 92f, row.y + 40f, textWidth, 26f), Ingredients(recipe), rowDetail);
                GUI.Label(new Rect(row.x + 92f, row.y + 68f, textWidth, 26f), $"<i>{Effect(recipe.Output)}</i>", rowDetail);

                bool can = problem == null;
                bool batch = recipe.Output is not EquipmentData;
                if (UITheme.BrushButton(new Rect(row.xMax - (batch ? 216f : 140f), row.y + 28f, 128f, 48f), "Craft", can)) Make(recipe, 1);
                if (batch && UITheme.BrushButton(new Rect(row.xMax - 78f, row.y + 28f, 66f, 48f), "×5", can)) Make(recipe, 5);
            }
            UITheme.EndScroll(ref scroll, view);
        }

        const float RepairRowHeight = 74f;

        /// <summary>Mend: each worn item you own, its condition, and the Iron Scrap it takes to make it as good as new.</summary>
        void DrawRepairs(float width)
        {
            GUI.Label(new Rect(4f, 4f, width - 8f, 30f), UITheme.Spaced("Mend"), rowName);
            int have = inventory.CountOf(scrap) + (bench.Storage != null ? bench.Storage.CountOf(scrap) : 0);
            for (int i = 0; i < worn.Count; i++)
            {
                var gear = worn[i];
                var row = new Rect(0f, 40f + i * (RepairRowHeight + 8f), width, RepairRowHeight);
                UITheme.PaperSlot(row, false, row.Contains(Event.current.mousePosition));
                UITheme.ItemIcon(new Rect(row.x + 14f, row.y + 11f, 52f, 52f), gear);
                int needed = condition.RepairScrap(gear);
                string color = have >= needed ? UITheme.InkGoldDarkHex : UITheme.VermilionHex;
                GUI.Label(new Rect(row.x + 80f, row.y + 6f, row.width - 300f, 30f), $"{gear.DisplayName}  <color={UITheme.MutedOnPaperHex}>· {condition.ConditionText(gear)}</color>", rowName);
                GUI.Label(new Rect(row.x + 80f, row.y + 38f, row.width - 300f, 26f),
                    $"<color={color}>{Mathf.Min(have, needed)}/{needed}</color> {scrap.DisplayName}", rowDetail);
                if (UITheme.BrushButton(new Rect(row.xMax - 140f, row.y + 13f, 128f, 48f), "Mend", have >= needed))
                {
                    if (condition.RepairWithScrap(gear, scrap, bench.Storage)) Show($"Mended {gear.DisplayName}.", false);
                    else Show($"Not enough {scrap.DisplayName}.", true);
                }
            }
        }

        /// <summary>"2/2 Healroot · <red>0/1 Turnip</red>"; worn gear is marked "(worn)".</summary>
        string Ingredients(RecipeData recipe)
        {
            text.Clear();
            foreach (var input in recipe.Inputs)
            {
                if (input.Item == null) continue;
                if (text.Length > 0) text.Append("  ·  ");
                int have = Crafting.Available(inventory, equipment, input.Item, bench.Storage);
                int stored = bench.Storage != null ? bench.Storage.CountOf(input.Item) : 0;
                bool worn = input.Item is EquipmentData gear && equipment != null && equipment.EquippedCount(gear) > 0 && inventory.CountOf(input.Item) + stored < input.Count;
                string color = have >= input.Count ? UITheme.InkGoldDarkHex : UITheme.VermilionHex;
                text.Append($"<color={color}>{Mathf.Min(have, input.Count)}/{input.Count}</color> {input.Item.DisplayName}");
                if (worn) text.Append($" <color={UITheme.MutedOnPaperHex}>(equipped)</color>");
                else if (stored > 0 && inventory.CountOf(input.Item) < input.Count) text.Append($" <color={UITheme.MutedOnPaperHex}>(chest)</color>");
            }
            return text.ToString();
        }

        static string Effect(ItemData item) => item switch
        {
            ConsumableData food => food.EffectText(),
            EquipmentData gear => GearText(gear),
            _ => item.Description,
        };

        static string GearText(EquipmentData gear)
        {
            if (gear.Modifiers == null || gear.Modifiers.Length == 0) return gear.Description;
            var parts = new List<string>();
            foreach (var modifier in gear.Modifiers) parts.Add(modifier.ToString());
            return string.Join(" · ", parts);
        }

        void Make(RecipeData recipe, int times)
        {
            int made = 0;
            for (int n = 0; n < times; n++)
            {
                if (!Crafting.Craft(recipe, inventory, equipment, bench.Storage)) break;
                made++;
            }
            if (made > 0)
            {
                string upgraded = recipe.Output is EquipmentData gear && equipment != null && equipment.EquippedCount(gear) > 0 ? " (equipped)" : string.Empty;
                Show(made > 1 ? $"Made {made}× {recipe.DisplayName}." : $"Made {recipe.DisplayName}{upgraded}.", false);
            }
            else Show(Crafting.Problem(recipe, inventory, equipment, bench.Storage) ?? "Can't make that.", true);
        }

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
            footerStyle = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
        }
    }
}
