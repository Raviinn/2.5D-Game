using System.Collections.Generic;
using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Gameplay HUD for quests (IMGUI prototype):
    /// - tracked quest panel under the minimap and clock (T cycles which quest is tracked)
    /// - on-screen waypoint with distance to the tracked quest's current target (pinned to the edge when off-screen)
    /// - "QUEST ACCEPTED" / "OBJECTIVES COMPLETE" / "QUEST COMPLETE" / standing tier banners (queued, never overwritten)
    /// - "!" / "?" markers above NPCs
    /// The current target is computed once per frame here; the minimap reads it too.
    /// </summary>
    public sealed class QuestTracker : MonoBehaviour
    {
        const float BannerDuration = 3.2f;
        const float HurriedBannerDuration = 1.8f; // a banner shown while others wait
        const float MaxBannerWait = 5f;           // queued banners older than this are stale and skipped
        const int MaxOtherQuests = 3;
        const int MaxQueuedBanners = 4;
        const float PanelWidth = 300f;

        [SerializeField, Tooltip("Hide the on-screen waypoint when closer than this (metres).")] float hideWaypointWithin = 3f;

        QuestLog log;
        Transform player;
        Camera cam;
        GameStateService state;
        InputAction trackAction;
        readonly List<QuestData> others = new();

        string bannerTitle;
        string bannerSubtitle;
        Color bannerColor;
        float bannerStart = float.NegativeInfinity;
        float bannerLength = BannerDuration;
        readonly Queue<(string title, string subtitle, Color color, float queuedAt)> bannerQueue = new();

        public QuestLog Log => log;
        public bool HasTarget { get; private set; }
        public Vector3 TargetPosition { get; private set; }
        public string TargetLabel { get; private set; }

        void OnEnable()
        {
            EventBus<QuestStartedEvent>.Subscribe(OnQuestStarted);
            EventBus<QuestReadyEvent>.Subscribe(OnQuestReady);
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
            EventBus<ReputationChangedEvent>.Subscribe(OnReputationChanged);
        }

        void OnDisable()
        {
            EventBus<QuestStartedEvent>.Unsubscribe(OnQuestStarted);
            EventBus<QuestReadyEvent>.Unsubscribe(OnQuestReady);
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
            EventBus<ReputationChangedEvent>.Unsubscribe(OnReputationChanged);
        }

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                log = playerObject.GetComponent<QuestLog>();
            }
            cam = Camera.main;
            state = Services.Get<GameStateService>();
            trackAction = Services.Get<InputService>().TrackQuest;
        }

        void Update()
        {
            while (bannerQueue.Count > 0 && !BannerShowing)
            {
                var (title, subtitle, color, queuedAt) = bannerQueue.Dequeue();
                if (Time.unscaledTime - queuedAt <= MaxBannerWait) StartBanner(title, subtitle, color);
            }

            if (log == null || player == null) return;
            if (trackAction.WasPressedThisFrame() && state.Current == GameState.Playing) log.CycleFocus();

            HasTarget = QuestNavigator.TryGetTarget(log.FocusedQuest, log, player.position, out var position, out var label);
            TargetPosition = position;
            TargetLabel = label;
        }

        // ---------- Banners ----------

        void OnQuestStarted(QuestStartedEvent evt) => ShowBanner("QUEST ACCEPTED", evt.Quest.Title, UITheme.Gold);
        void OnQuestCompleted(QuestCompletedEvent evt) => ShowBanner("QUEST COMPLETE", $"{evt.Quest.Title}  ·  {QuestText.Rewards(evt.Quest)}", UITheme.Good);

        void OnQuestReady(QuestReadyEvent evt) =>
            ShowBanner("OBJECTIVES COMPLETE", $"{evt.Quest.Title}  ·  return to {evt.Quest.TurnInAt}", UITheme.Info);

        void OnReputationChanged(ReputationChangedEvent evt)
        {
            if (!evt.TierChanged || !Services.TryGet(out Reputation reputation) || !reputation.IsConfigured) return;
            var tier = reputation.Config.Tiers[evt.Tier];
            string subtitle = $"{reputation.FactionName}: {tier.Name}  ·  {tier.Perk}";
            if (evt.Tier > evt.PreviousTier) ShowBanner("STANDING RAISED", subtitle, UITheme.Good);
            else ShowBanner("STANDING LOWERED", subtitle, UITheme.Bad);
        }

        bool BannerShowing => bannerTitle != null && Time.unscaledTime - bannerStart < bannerLength;

        /// <summary>Shows a banner now, or after the current one finishes.</summary>
        void ShowBanner(string title, string subtitle, Color color)
        {
            if (BannerShowing || bannerQueue.Count > 0)
            {
                if (bannerQueue.Count < MaxQueuedBanners) bannerQueue.Enqueue((title, subtitle, color, Time.unscaledTime));
                HurryCurrentBanner();
                return;
            }
            StartBanner(title, subtitle, color);
        }

        void StartBanner(string title, string subtitle, Color color)
        {
            bannerTitle = title;
            bannerSubtitle = subtitle;
            bannerColor = color;
            bannerStart = Time.unscaledTime;
            bannerLength = BannerDuration;
            if (bannerQueue.Count > 0) HurryCurrentBanner();
        }

        /// <summary>Something is waiting: shorten the current banner (it still shows long enough to read).</summary>
        void HurryCurrentBanner()
        {
            float age = Time.unscaledTime - bannerStart;
            bannerLength = Mathf.Min(bannerLength, Mathf.Max(HurriedBannerDuration, age + 0.6f));
        }

        // ---------- Drawing ----------

        void OnGUI()
        {
            if (log == null || state == null) return;
            UITheme.Begin(-5); // above the menu screens, so banners show over them

            if (state.Current == GameState.Playing)
            {
                DrawNpcMarkers();
                DrawWaypoint();
                DrawPanel();
            }
            // Banners also show over menus (e.g. accepting a contract on the board).
            if (state.Current is GameState.Playing or GameState.InGameMenu) DrawBanner();
        }

        void DrawPanel()
        {
            var focused = log.FocusedQuest;
            if (focused == null) return;

            float x = UITheme.Width - PanelWidth - GameHud.MinimapMargin;
            float y = GameHud.TrackerTop;
            float textWidth = PanelWidth - 40f;

            string ready = log.IsReady(focused) ? $"\n<color={UITheme.GoodOnInkHex}>Return to {focused.TurnInAt}</color>" : string.Empty;
            string target = HasTarget
                ? $"\n<color={UITheme.InkGoldHex}>{TargetLabel}</color> <color={UITheme.MutedOnInkHex}>· {Vector3.Distance(player.position, TargetPosition):0} m</color>"
                : string.Empty;
            string body = $"<size=15>{QuestText.Objectives(focused, log, UITheme.GoodOnInkHex)}{ready}{target}</size>";
            string title = $"<color={QuestText.TypeColorOnInk(focused.Type)}>{focused.Title}</color>";
            float titleHeight = UITheme.Body.CalcHeight(new GUIContent(title), textWidth);
            float bodyHeight = UITheme.Body.CalcHeight(new GUIContent(body), textWidth);

            var box = new Rect(x, y, PanelWidth, titleHeight + bodyHeight + 22f);
            UITheme.HudPanel(box);
            UITheme.DrawIcon(new Rect(x + 10f, y + 12f, 14f, 14f), UITheme.DiamondIcon, UITheme.InkGold);
            GUI.Label(new Rect(x + 30f, y + 8f, textWidth, titleHeight), title, UITheme.Body);
            GUI.Label(new Rect(x + 30f, y + 12f + titleHeight, textWidth, bodyHeight), body, UITheme.Body);
            y += box.height + 6f;

            // Other active quests: titles only, so the tracked one stands out.
            others.Clear();
            foreach (var quest in log.SortedActive())
                if (quest != focused) others.Add(quest);
            for (int i = 0; i < others.Count && i < MaxOtherQuests; i++)
            {
                string status = log.IsReady(others[i]) ? $" <color={UITheme.GoodOnInkHex}>(ready)</color>" : string.Empty;
                UITheme.ShadowLabel(new Rect(x + 12f, y, PanelWidth - 20f, 20f), $"<color={UITheme.MutedOnInkHex}>{others[i].Title}</color>{status}", UITheme.Small, Color.white);
                y += 20f;
            }
            if (others.Count > MaxOtherQuests)
            {
                UITheme.ShadowLabel(new Rect(x + 12f, y, PanelWidth - 20f, 20f), $"<color={UITheme.MutedOnInkHex}>+{others.Count - MaxOtherQuests} more</color>", UITheme.Small, Color.white);
                y += 20f;
            }

            float hintX = x + 8f;
            y += 4f;
            if (others.Count > 0) hintX += UITheme.KeyHint(hintX, y, "T", "Switch", 0.8f);
            UITheme.KeyHint(hintX, y, "J", "Journal", 0.8f);
        }

        void DrawWaypoint()
        {
            if (!HasTarget || cam == null || player == null) return;
            float distance = Vector3.Distance(player.position, TargetPosition);
            if (distance < hideWaypointWithin) return;

            bool inFront = UITheme.WorldToGui(cam, TargetPosition + Vector3.up * 2.2f, out var point);
            const float margin = 48f;
            float width = UITheme.Width;
            float height = UITheme.Height;
            bool offScreen = !inFront || point.x < margin || point.x > width - margin || point.y < margin || point.y > height - margin;

            if (offScreen)
            {
                // Pin to the screen edge in the target's direction.
                var center = new Vector2(width * 0.5f, height * 0.5f);
                var direction = point - center;
                if (!inFront) direction = -direction;
                if (direction.sqrMagnitude < 0.01f) direction = Vector2.up;
                float scale = Mathf.Min((width * 0.5f - margin) / Mathf.Max(Mathf.Abs(direction.x), 0.01f),
                                        (height * 0.5f - margin) / Mathf.Max(Mathf.Abs(direction.y), 0.01f));
                point = center + direction * scale;
            }

            float pulse = offScreen ? 1f : 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f);
            float size = 26f * pulse;
            UITheme.DrawIcon(new Rect(point.x - size * 0.5f + 1.5f, point.y - size * 0.5f + 1.5f, size, size), UITheme.DiamondIcon, new Color(0f, 0f, 0f, 0.6f));
            UITheme.DrawIcon(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), UITheme.DiamondIcon, new Color(UITheme.InkGold.r, UITheme.InkGold.g, UITheme.InkGold.b, offScreen ? 0.85f : 1f));
            UITheme.ShadowLabel(new Rect(point.x - 80f, point.y + 14f, 160f, 20f), $"{distance:0} m", UITheme.SmallCenter, UITheme.Text);
        }

        void DrawNpcMarkers()
        {
            if (cam == null) return;
            foreach (var interactable in Interactable.Active)
            {
                if (interactable is not DialogueSpeaker speaker) continue;
                char marker = speaker.QuestMarker(log);
                if (marker == '\0') continue;
                if (!UITheme.WorldToGui(cam, speaker.transform.position + Vector3.up * 1.8f, out var point)) continue;

                float bob = Mathf.Sin(Time.unscaledTime * 2.5f) * 3f;
                var color = marker == '?' ? UITheme.Gold : new Color(1f, 0.93f, 0.65f);
                UITheme.ShadowLabel(new Rect(point.x - 30f, point.y - 46f + bob, 60f, 50f), marker.ToString(), UITheme.Big, color);
            }
        }

        GameMenu gameMenu;

        void DrawBanner()
        {
            float age = Time.unscaledTime - bannerStart;
            if (age > bannerLength || bannerTitle == null) return;

            float alpha = age < 0.2f ? age / 0.2f : age > bannerLength - 0.6f ? (bannerLength - age) / 0.6f : 1f;
            if (state.Current == GameState.InGameMenu)
            {
                // Over a menu: a slim one-line strip at the very top (under the game menu's tab bar), never over the content.
                if (gameMenu == null) gameMenu = FindFirstObjectByType<GameMenu>();
                float top = gameMenu != null && gameMenu.IsOpen ? 64f : 0f;
                var strip = new Rect(0f, top, UITheme.Width, 30f);
                UITheme.Fill(strip, new Color(0.07f, 0.07f, 0.07f, 0.85f * alpha));
                UITheme.Fill(new Rect(0f, strip.yMax - 2f, UITheme.Width, 2f), new Color(bannerColor.r, bannerColor.g, bannerColor.b, 0.7f * alpha));
                string hex = ColorUtility.ToHtmlStringRGB(bannerColor);
                UITheme.ShadowLabel(strip, $"<b><color=#{hex}>{bannerTitle}</color></b>   {bannerSubtitle}", UITheme.SmallCenter, new Color(1f, 1f, 1f, alpha));
                return;
            }
            var band = new Rect(0f, 96f, UITheme.Width, 92f);
            UITheme.Fill(band, new Color(0.07f, 0.07f, 0.07f, 0.6f * alpha));
            var line = new Color(bannerColor.r, bannerColor.g, bannerColor.b, 0.85f * alpha);
            float lineWidth = Mathf.Min(560f, UITheme.Width * 0.4f);
            UITheme.Fill(new Rect((UITheme.Width - lineWidth) * 0.5f, band.yMax - 4f, lineWidth, 2f), line);
            var titleStyle = UITheme.DisplayTitle;
            int size = titleStyle.fontSize;
            titleStyle.fontSize = 34;
            UITheme.ShadowLabel(new Rect(0f, band.y + 8f, UITheme.Width, 44f), UITheme.Spaced(bannerTitle), titleStyle,
                new Color(UITheme.OffWhite.r, UITheme.OffWhite.g, UITheme.OffWhite.b, alpha));
            titleStyle.fontSize = size;
            UITheme.ShadowLabel(new Rect(0f, band.y + 54f, UITheme.Width, 26f), bannerSubtitle, UITheme.BodyCenter,
                new Color(bannerColor.r, bannerColor.g, bannerColor.b, alpha));
        }
    }
}
