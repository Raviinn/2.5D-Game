using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// World-space combat UI: enemy name plates with health/poise (shown when nearby, hurt or locked on),
    /// the lock-on reticle and floating damage numbers. Player vitals live in GameHud.
    /// Dev keys: F6 / F7 grant Combat / Farming XP, F8 grants 50 standing.
    /// </summary>
    public sealed class CombatDebugHUD : MonoBehaviour
    {
        const int MaxPopups = 24;
        const float PopupLifetime = 0.9f;
        const float PlateRange = 14f;

        struct Popup
        {
            public Vector3 WorldPosition;
            public string Text;
            public Color Color;
            public float Time;
            public bool Big;
        }

        Combatant playerCombatant;
        LockOnController lockOn;
        PlayerProgression progression;
        GameStateService state;
        Camera cam;
        GUIStyle popupStyle;
        GUIStyle bigPopupStyle;
        readonly Popup[] popups = new Popup[MaxPopups];
        int nextPopup;

        void OnEnable() => EventBus<DamageDealtEvent>.Subscribe(OnDamage);
        void OnDisable() => EventBus<DamageDealtEvent>.Unsubscribe(OnDamage);

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerCombatant = player.GetComponent<Combatant>();
                lockOn = player.GetComponent<LockOnController>();
                progression = player.GetComponent<PlayerProgression>();
            }
            cam = Camera.main;
            state = Services.Get<GameStateService>();
        }

        void Update()
        {
            // Dev shortcuts for testing progression without grinding.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || progression == null) return;
            if (keyboard.f6Key.wasPressedThisFrame) progression.AddXp(Discipline.Combat, 100);
            if (keyboard.f7Key.wasPressedThisFrame) progression.AddXp(Discipline.Farming, 100);
            if (keyboard.f8Key.wasPressedThisFrame && Services.TryGet(out Reputation reputation)) reputation.Change(50, StandingSource.Debug);
        }

        void OnDamage(DamageDealtEvent evt)
        {
            // Settings → Interface → Damage numbers: off hides the numbers, PARRY! / GUARD BREAK still show.
            bool numbers = !Services.TryGet(out SettingsService settings) || settings.Current.damageNumbers;
            if (!numbers && evt.Result is not (HitResult.Parried or HitResult.GuardBroken)) return;
            string text = evt.Result switch
            {
                HitResult.Parried => "PARRY!",
                HitResult.GuardBroken => "GUARD BREAK",
                HitResult.Blocked => $"({evt.Damage:0})",
                _ => evt.Damage.ToString("0"),
            };
            Color color = evt.Result switch
            {
                HitResult.Parried => new Color(1f, 0.88f, 0.3f),
                HitResult.GuardBroken => UITheme.PoiseColor,
                HitResult.Blocked => UITheme.Info,
                HitResult.Killed => UITheme.Gold,
                _ => evt.Target.Team == Team.Player ? UITheme.Bad : Color.white,
            };

            popups[nextPopup] = new Popup
            {
                WorldPosition = evt.Target.transform.position + Vector3.up * 1.8f + Random.insideUnitSphere * 0.3f,
                Text = text,
                Color = color,
                Time = Time.unscaledTime,
                Big = evt.Result is HitResult.Parried or HitResult.GuardBroken or HitResult.Killed,
            };
            nextPopup = (nextPopup + 1) % MaxPopups;
        }

        void OnGUI()
        {
            if (cam == null || state == null || state.Current != GameState.Playing) return;
            UITheme.Begin();
            popupStyle ??= new GUIStyle(UITheme.Header) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            bigPopupStyle ??= new GUIStyle(popupStyle) { fontSize = 28 };

            DrawEnemyPlates();
            DrawPopups();
        }

        void DrawEnemyPlates()
        {
            Vector3 playerPosition = playerCombatant != null ? playerCombatant.transform.position : cam.transform.position;
            foreach (var enemy in EnemyController.Active)
            {
                var c = enemy.Combatant;
                if (c.IsDead) continue;

                bool locked = lockOn != null && lockOn.Target == c;
                bool hurt = c.Health < c.MaxHealth - 0.01f;
                float distance = Vector3.Distance(playerPosition, enemy.transform.position);
                if (!locked && !hurt && distance > PlateRange) continue;
                if (!UITheme.WorldToGui(cam, enemy.transform.position + Vector3.up * 1.6f, out var point)) continue;

                // Fade plates out toward the edge of the range so they don't pop.
                float alpha = locked ? 1f : Mathf.Clamp01((PlateRange + 4f - distance) / 4f);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                const float width = 96f;
                string name = enemy.Data != null ? enemy.Data.DisplayName : enemy.name;
                // Bolder at night: a red name with a moon beside it.
                var nameColor = locked ? UITheme.Gold : enemy.Emboldened ? UITheme.Bad : UITheme.Text;
                UITheme.ShadowLabel(new Rect(point.x - 80f, point.y - 22f, 160f, 18f), name, UITheme.SmallCenter, nameColor);
                if (enemy.Emboldened)
                {
                    float nameWidth = UITheme.SmallCenter.CalcSize(new GUIContent(name)).x;
                    UITheme.DrawIcon(new Rect(point.x - nameWidth * 0.5f - 18f, point.y - 20f, 14f, 14f), UITheme.MoonIcon, UITheme.Bad);
                }
                UITheme.Bar(new Rect(point.x - width * 0.5f, point.y, width, 9f), c.MaxHealth > 0f ? c.Health / c.MaxHealth : 0f, UITheme.Health);
                UITheme.Bar(new Rect(point.x - width * 0.5f, point.y + 11f, width, 4f), c.MaxPoise > 0f ? c.Poise / c.MaxPoise : 0f, UITheme.PoiseColor);
                GUI.color = old;

                if (locked && UITheme.WorldToGui(cam, enemy.transform.position + Vector3.up * 0.9f, out var body))
                {
                    float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 8f);
                    float size = 44f * pulse;
                    UITheme.DrawIcon(new Rect(body.x - size * 0.5f, body.y - size * 0.5f, size, size), UITheme.RingIcon, UITheme.Gold);
                }
            }
        }

        void DrawPopups()
        {
            for (int i = 0; i < MaxPopups; i++)
            {
                ref var popup = ref popups[i];
                if (popup.Text == null) continue;

                float age = Time.unscaledTime - popup.Time;
                if (age > PopupLifetime) { popup.Text = null; continue; }
                if (!UITheme.WorldToGui(cam, popup.WorldPosition + Vector3.up * age * 0.8f, out var point)) continue;

                var color = new Color(popup.Color.r, popup.Color.g, popup.Color.b, 1f - age / PopupLifetime);
                UITheme.ShadowLabel(new Rect(point.x - 80f, point.y - 16f, 160f, 32f), popup.Text, popup.Big ? bigPopupStyle : popupStyle, color);
            }
        }
    }
}
