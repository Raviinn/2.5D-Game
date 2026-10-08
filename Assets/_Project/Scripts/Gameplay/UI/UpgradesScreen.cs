using System.Text;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Milestone 49: the homestead plans window, on parchment. One card per improvement: what it does, what it costs
    /// (gold and materials; short ones in red, counting the chest), and a Build button, or "Built". Esc leaves.
    /// </summary>
    public sealed class UpgradesScreen : MonoBehaviour
    {
        const float RowHeight = 118f;

        HomesteadUpgrades plans;
        Inventory bag;
        GameStateService state;
        Vector2 scroll;
        string message;
        bool messageIsError;
        float messageTime;
        readonly StringBuilder text = new();
        GUIStyle titleStyle, bodyStyle, costStyle;

        public HomesteadUpgrades Plans => plans;

        void OnEnable()
        {
            EventBus<HomesteadPlansOpenedEvent>.Subscribe(OnOpened);
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus<HomesteadPlansOpenedEvent>.Unsubscribe(OnOpened);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) bag = player.GetComponent<Inventory>();
            state = Services.Get<GameStateService>();
        }

        void OnOpened(HomesteadPlansOpenedEvent evt)
        {
            plans = evt.Plans;
            message = null;
            scroll = Vector2.zero;
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) plans = null;
        }

        void OnGUI()
        {
            if (plans == null || bag == null || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();
            EnsureStyles();
            var area = UITheme.ParchmentWindow(980f, 720f, "Homestead Plans", $"{bag.Gold} gold");
            var view = new Rect(area.x, area.y, area.width, area.height - 56f);
            var upgrades = plans.All;
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(1, upgrades.Count) * (RowHeight + 12f));
            scroll = UITheme.BeginScroll(view, scroll, content);
            for (int i = 0; i < upgrades.Count; i++)
            {
                var upgrade = upgrades[i];
                var row = new Rect(0f, i * (RowHeight + 12f), content.width, RowHeight);
                bool built = plans.Has(upgrade.Kind);
                string problem = plans.Problem(upgrade, bag);
                UITheme.PaperSlot(row, false, row.Contains(Event.current.mousePosition));
                GUI.Label(new Rect(row.x + 20f, row.y + 10f, row.width - 220f, 32f), upgrade.Title, titleStyle);
                GUI.Label(new Rect(row.x + 20f, row.y + 42f, row.width - 220f, 44f), upgrade.Description, bodyStyle);
                GUI.Label(new Rect(row.x + 20f, row.y + 86f, row.width - 220f, 26f), built ? $"<color={UITheme.GoodOnPaperHex}>Built</color>" : CostText(upgrade), costStyle);
                if (built) continue;
                if (UITheme.BrushButton(new Rect(row.xMax - 168f, row.y + 34f, 150f, 50f), "Build", problem == null))
                {
                    bool ok = plans.Build(upgrade.Kind, bag);
                    message = ok ? $"Built the {upgrade.Title}." : problem ?? "Can't build that.";
                    messageIsError = !ok;
                    messageTime = Time.unscaledTime;
                }
            }
            UITheme.EndScroll(ref scroll, view);

            string line = message != null && Time.unscaledTime - messageTime < 3f
                ? $"<color={(messageIsError ? UITheme.VermilionHex : UITheme.GoodOnPaperHex)}>{message}</color>"
                : "Materials come from your bag first, then the storage chest.";
            GUI.Label(new Rect(area.x, area.yMax - 34f, area.width - 220f, 30f), line, costStyle);
            UITheme.KeyHints(area.xMax, area.yMax - 36f, false, ("Esc", "Leave"));
        }

        /// <summary>"<red>120</red> gold · 4/6 Iron Scrap"</summary>
        string CostText(HomesteadUpgrades.Upgrade upgrade)
        {
            text.Clear();
            string goldColor = bag.Gold >= upgrade.Gold ? UITheme.InkGoldDarkHex : UITheme.VermilionHex;
            text.Append($"<color={goldColor}>{upgrade.Gold} gold</color>");
            if (upgrade.Materials != null)
                foreach (var cost in upgrade.Materials)
                {
                    if (cost.Item == null) continue;
                    int have = plans.Available(bag, cost.Item);
                    string color = have >= cost.Count ? UITheme.InkGoldDarkHex : UITheme.VermilionHex;
                    text.Append($"  ·  <color={color}>{Mathf.Min(have, cost.Count)}/{cost.Count}</color> {cost.Item.DisplayName}");
                }
            return text.ToString();
        }

        void EnsureStyles()
        {
            if (titleStyle != null && titleStyle.font == UITheme.PaperBody.font) return;
            titleStyle = new GUIStyle(UITheme.PaperBody) { wordWrap = false, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true };
            bodyStyle = new GUIStyle(UITheme.PaperMuted) { wordWrap = true, richText = true };
            costStyle = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleLeft, richText = true };
        }
    }
}
