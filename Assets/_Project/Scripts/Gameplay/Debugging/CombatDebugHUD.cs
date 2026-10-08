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
            // Hits on you show where they came from (Milestone 51), numbers or not.
            if (evt.Target != null && evt.Target == playerCombatant && evt.Attacker != null &&
                evt.Result is HitResult.Hit or HitResult.Blocked or HitResult.GuardBroken or HitResult.Killed)
                RememberDamageFrom(evt.Attacker);
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
            DrawLockOnBar();
            DrawDamageDirections();
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
                // Above the head, whatever the size (wolves are low, brutes tall): measured from the feet.
                Vector3 platePoint = Feet(enemy) + Vector3.up * (enemy.Data != null ? enemy.Data.PlateHeight : 2.2f);
                if (!UITheme.WorldToGui(cam, platePoint, out var point)) continue;

                // Hidden behind walls (Milestone 51), fading rather than popping; the locked target always shows.
                float seen = PlateVisibility(enemy, platePoint, locked);
                if (seen <= 0.01f) continue;
                // Fade plates out toward the edge of the range so they don't pop.
                float alpha = (locked ? 1f : Mathf.Clamp01((PlateRange + 4f - distance) / 4f)) * seen;
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

        // ---------- Milestone 51: plates behind walls, lock-on bar, damage direction ----------

        readonly System.Collections.Generic.Dictionary<EnemyController, float> plateAlpha = new();
        static int occluderMask = -1;

        static Vector3 Feet(EnemyController enemy)
        {
            var body = enemy.GetComponent<CharacterController>();
            var p = enemy.transform.position;
            return body != null ? new Vector3(p.x, p.y + body.center.y - body.height * 0.5f, p.z) : p;
        }

        /// <summary>1 when the plate's spot can be seen from the camera, easing to 0 behind walls, roofs or rocks.</summary>
        float PlateVisibility(EnemyController enemy, Vector3 platePoint, bool locked)
        {
            if (occluderMask == -1)
            {
                occluderMask = Physics.DefaultRaycastLayers & ~(CombatLayers.PlayerMask | CombatLayers.EnemyMask) & ~(1 << 2);
                int npc = LayerMask.NameToLayer("NPC");
                if (npc >= 0) occluderMask &= ~(1 << npc);
                int billboards = LayerMask.NameToLayer(EnvironmentLayers.Billboards);
                if (billboards >= 0) occluderMask &= ~(1 << billboards);
            }
            // Check both the plate and the chest: a plate peeking over a low wall still counts as hidden if the body is.
            Vector3 eye = cam.transform.position;
            bool blocked = Physics.Linecast(eye, platePoint, occluderMask, QueryTriggerInteraction.Ignore) &&
                           Physics.Linecast(eye, Feet(enemy) + Vector3.up * 1f, occluderMask, QueryTriggerInteraction.Ignore);
            plateAlpha.TryGetValue(enemy, out float alpha);
            float target = blocked && !locked ? 0f : 1f;
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * 6f);
            plateAlpha[enemy] = alpha;
            return alpha;
        }

        /// <summary>Public for tests: the plate's current fade (0 hidden … 1 shown).</summary>
        public float PlateAlpha(EnemyController enemy) => plateAlpha.TryGetValue(enemy, out float a) ? a : 0f;

        /// <summary>Locked on: a wide bar at the top of the screen with the target's name, health, poise and guard.</summary>
        void DrawLockOnBar()
        {
            var target = lockOn != null ? lockOn.Target : null;
            if (target == null || target.IsDead) return;
            var enemy = target.GetComponent<EnemyController>();
            string name = enemy != null && enemy.Data != null ? enemy.Data.DisplayName : target.name;
            float width = Mathf.Min(640f, UITheme.Width - 520f);
            float x = (UITheme.Width - width) * 0.5f;
            const float top = 34f;
            UITheme.ShadowLabel(new Rect(x, top, width, 26f), UITheme.Spaced(name), UITheme.BodyCenter, enemy != null && enemy.Emboldened ? UITheme.Bad : UITheme.Text);
            UITheme.Fill(new Rect(x - 2f, top + 30f, width + 4f, 16f), new Color(0f, 0f, 0f, 0.6f));
            UITheme.Bar(new Rect(x, top + 32f, width, 12f), target.MaxHealth > 0f ? target.Health / target.MaxHealth : 0f, UITheme.Health);
            UITheme.Bar(new Rect(x, top + 48f, width, 5f), target.MaxPoise > 0f ? target.Poise / target.MaxPoise : 0f, UITheme.PoiseColor);
            if (enemy != null && enemy.Data != null && enemy.Data.Shield)
                UITheme.Bar(new Rect(x, top + 55f, width * 0.5f, 4f), enemy.GuardFraction, UITheme.Info);
            UITheme.ShadowLabel(new Rect(x, top + 30f, width, 16f), $"{Mathf.CeilToInt(target.Health)} / {Mathf.CeilToInt(target.MaxHealth)}", UITheme.SmallCenter, UITheme.Text);
        }

        struct DamageMark
        {
            public Vector3 From;
            public float Time;
        }

        const float DamageMarkLifetime = 1.2f;
        readonly DamageMark[] damageMarks = new DamageMark[4];
        int nextDamageMark;

        /// <summary>Public for tests: how many damage-direction marks are showing.</summary>
        public int DamageMarksShowing
        {
            get
            {
                int n = 0;
                foreach (var m in damageMarks) if (m.Time > 0f && Time.unscaledTime - m.Time < DamageMarkLifetime) n++;
                return n;
            }
        }

        void RememberDamageFrom(Combatant attacker)
        {
            damageMarks[nextDamageMark] = new DamageMark { From = attacker.transform.position, Time = Time.unscaledTime };
            nextDamageMark = (nextDamageMark + 1) % damageMarks.Length;
        }

        /// <summary>Hit from off-screen? A red chevron near the middle of the screen points where the blow came from.</summary>
        void DrawDamageDirections()
        {
            if (playerCombatant == null) return;
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            var centre = new Vector2(UITheme.Width * 0.5f, UITheme.Height * 0.5f);
            foreach (var mark in damageMarks)
            {
                float age = Time.unscaledTime - mark.Time;
                if (mark.Time <= 0f || age > DamageMarkLifetime) continue;
                Vector3 to = mark.From - playerCombatant.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f) continue;
                float angle = Vector3.SignedAngle(forward, to, Vector3.up); // 0 = straight ahead, 90 = right
                float radius = Mathf.Min(UITheme.Height * 0.3f, 260f);
                float radians = angle * Mathf.Deg2Rad;
                var at = centre + new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)) * radius;
                var matrix = GUI.matrix;
                UITheme.RotateAround(angle, at);
                float alpha = 1f - age / DamageMarkLifetime;
                UITheme.DrawIcon(new Rect(at.x - 31f, at.y - 29f, 64f, 64f), UITheme.ArrowIcon, new Color(0f, 0f, 0f, 0.5f * alpha));
                UITheme.DrawIcon(new Rect(at.x - 32f, at.y - 32f, 64f, 64f), UITheme.ArrowIcon, new Color(UITheme.Health.r, UITheme.Health.g, UITheme.Health.b, 0.95f * alpha));
                GUI.matrix = matrix;
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
