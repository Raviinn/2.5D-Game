using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Milestone 47: the fishing game. Cast → wait for a bite (the float bobs) → "!" : press Interact within a
    /// second to hook it → reel: a needle sweeps a bar; press Interact while it's over the gold mark. Three good
    /// presses land the fish; two misses and it gets away. Harder fish sweep faster at a smaller mark.
    /// Walking, attacking or opening a menu reels the line in.
    /// </summary>
    [RequireComponent(typeof(PlayerInteractor))]
    public sealed class PlayerFishing : MonoBehaviour
    {
        public enum Phase { Idle, Waiting, Bite, Reeling }

        const float HookWindow = 1.0f;
        const int HitsToLand = 3;
        const int MissesToLose = 2;

        Phase phase;
        FishingSpot spot;
        FishingSpot.Catch fish;
        float biteAt, biteEnds;
        float needle, needleDirection = 1f, needleSpeed;
        float markCentre, markHalf;
        int hits, misses;
        float pressedAt = -1f;
        string lastResult;
        float lastResultTime;

        PlayerInteractor interactor;
        PlayerMotor motor;
        PlayerCombat combat;
        InputAction interact;
        GameStateService state;
        DayNightCycle dayNight;
        Transform bobber;
        LineRenderer line;
        Vector3 castPoint;

        public Phase Current => phase;
        public bool IsFishing => phase != Phase.Idle;
        /// <summary>Reeling: where the needle is (0..1) and the gold mark (centre, half width).</summary>
        public float Needle => needle;
        public float MarkCentre => markCentre;
        public float MarkHalf => markHalf;
        public string LastResult => lastResult;

        void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
        }

        void Start()
        {
            interact = Services.Get<InputService>().Interact;
            state = Services.Get<GameStateService>();
        }

        /// <summary>Starts fishing at this spot (from its prompt).</summary>
        public void Cast(FishingSpot at)
        {
            if (IsFishing || at == null) return;
            spot = at;
            phase = Phase.Waiting;
            biteAt = Time.time + Random.Range(2f, 5.5f);
            interactor.Busy = true;
            motor.InputMovementEnabled = false;
            castPoint = at.CastPoint(transform.position);
            Vector3 face = castPoint - transform.position;
            face.y = 0f;
            motor.SnapRotation(face);
            ShowLine(true);
            pressedAt = Time.unscaledTime;
        }

        /// <summary>Reels in without a catch.</summary>
        public void Stop(string result = null)
        {
            if (!IsFishing) return;
            phase = Phase.Idle;
            interactor.Busy = false;
            motor.InputMovementEnabled = true;
            ShowLine(false);
            if (result != null)
            {
                lastResult = result;
                lastResultTime = Time.unscaledTime;
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent(result));
            }
        }

        /// <summary>Tests: press Interact now.</summary>
        public void Press() => OnPress();

        void Update()
        {
            if (!IsFishing) return;
            if (state.Current != GameState.Playing) { Stop(); return; }
            if (combat != null && !combat.CanUseItems) { Stop("You drop the line."); return; }
            if (motor.RawInput.sqrMagnitude > 0.25f) { Stop("You reel in your line."); return; }

            if (interact.WasPressedThisFrame() && Time.unscaledTime - pressedAt > 0.15f) OnPress();
            UpdateBobber();

            switch (phase)
            {
                case Phase.Waiting:
                    if (Time.time >= biteAt) Bite();
                    break;
                case Phase.Bite:
                    if (Time.time > biteEnds) Stop("It got away before you could hook it.");
                    break;
                case Phase.Reeling:
                    needle += needleDirection * needleSpeed * Time.deltaTime;
                    if (needle > 1f) { needle = 2f - needle; needleDirection = -1f; }
                    if (needle < 0f) { needle = -needle; needleDirection = 1f; }
                    break;
            }
        }

        void Bite()
        {
            if (dayNight == null) Services.TryGet(out dayNight);
            if (!spot.PickCatch(out fish, dayNight != null && dayNight.IsNight))
            {
                Stop("Nothing's biting here right now.");
                return;
            }
            phase = Phase.Bite;
            biteEnds = Time.time + HookWindow;
        }

        void OnPress()
        {
            pressedAt = Time.unscaledTime;
            switch (phase)
            {
                case Phase.Waiting:
                    Stop("Too soon: nothing on the line yet.");
                    break;
                case Phase.Bite:
                    phase = Phase.Reeling;
                    hits = misses = 0;
                    needle = 0f;
                    needleDirection = 1f;
                    needleSpeed = Mathf.Lerp(0.7f, 1.6f, fish.Difficulty);
                    markHalf = Mathf.Lerp(0.14f, 0.07f, fish.Difficulty);
                    MoveMark();
                    break;
                case Phase.Reeling:
                    if (Mathf.Abs(needle - markCentre) <= markHalf)
                    {
                        hits++;
                        needleSpeed *= 1.12f;
                        if (hits >= HitsToLand) { Land(); return; }
                        MoveMark();
                    }
                    else if (++misses >= MissesToLose)
                    {
                        Stop($"The {fish.Fish.DisplayName} slipped the hook.");
                    }
                    break;
            }
        }

        void MoveMark() => markCentre = Random.Range(markHalf + 0.05f, 1f - markHalf - 0.05f);

        void Land()
        {
            var inventory = interactor.Inventory;
            int leftover = inventory.Add(fish.Fish, 1);
            if (leftover > 0) ItemPickup.SpawnItem(transform.position, fish.Fish, leftover, 0.5f);
            if (TryGetComponent(out PlayerProgression progression)) progression.AddXp(Discipline.Farming, Mathf.RoundToInt(6 + 10 * fish.Difficulty));
            var audio = GameAudio.Instance;
            if (audio != null && audio.Library != null) audio.Play(audio.Library.Pickup, transform.position);
            Stop($"Caught a {fish.Fish.DisplayName}!");
        }

        // ---------- The float and the line ----------

        void ShowLine(bool show)
        {
            if (show && bobber == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Fishing Float";
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * 0.14f;
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.85f, 0.15f, 0.12f) };
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                bobber = go.transform;
                line = go.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.startWidth = line.endWidth = 0.012f;
                line.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.9f, 0.9f, 0.85f, 0.8f) };
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (bobber != null) bobber.gameObject.SetActive(show);
        }

        void UpdateBobber()
        {
            if (bobber == null) return;
            float dip = phase == Phase.Bite ? -0.08f + Mathf.Sin(Time.time * 30f) * 0.03f : Mathf.Sin(Time.time * 2.2f) * 0.015f;
            bobber.position = castPoint + Vector3.up * dip;
            line.SetPosition(0, transform.position + Vector3.up * 0.8f + transform.forward * 0.5f);
            line.SetPosition(1, bobber.position);
        }

        void OnDestroy()
        {
            if (bobber != null) Destroy(bobber.gameObject);
        }

        // ---------- On screen ----------

        void OnGUI()
        {
            if (!IsFishing || state == null || state.Current != GameState.Playing) return;
            UITheme.Begin();
            float width = 520f;
            var panel = new Rect((UITheme.Width - width) * 0.5f, UITheme.Height - 260f, width, 96f);
            UITheme.HudPanel(panel);
            string key = UITheme.KeyLabel("F");
            switch (phase)
            {
                case Phase.Waiting:
                    UITheme.ShadowLabel(new Rect(panel.x, panel.y + 14f, width, 30f), "Waiting for a bite…", UITheme.BodyCenter, UITheme.Text);
                    UITheme.ShadowLabel(new Rect(panel.x, panel.y + 52f, width, 26f), "Walk away to reel in", UITheme.SmallCenter, UITheme.Muted);
                    break;
                case Phase.Bite:
                    UITheme.ShadowLabel(new Rect(panel.x, panel.y + 8f, width, 50f), "<b>!</b>", UITheme.Huge, UITheme.Gold);
                    UITheme.ShadowLabel(new Rect(panel.x, panel.y + 60f, width, 26f), $"[{key}] Hook it!", UITheme.SmallCenter, UITheme.Text);
                    break;
                case Phase.Reeling:
                    var bar = new Rect(panel.x + 30f, panel.y + 18f, width - 60f, 22f);
                    UITheme.Fill(bar, new Color(0f, 0f, 0f, 0.6f));
                    UITheme.Fill(new Rect(bar.x + (markCentre - markHalf) * bar.width, bar.y, markHalf * 2f * bar.width, bar.height), UITheme.Gold);
                    UITheme.Fill(new Rect(bar.x + needle * bar.width - 2f, bar.y - 6f, 4f, bar.height + 12f), Color.white);
                    UITheme.ShadowLabel(new Rect(panel.x, panel.y + 52f, width, 26f),
                        $"[{key}] on the gold   ·   {hits}/{HitsToLand}   ·   misses {misses}/{MissesToLose}", UITheme.SmallCenter, UITheme.Text);
                    break;
            }
        }
    }
}
