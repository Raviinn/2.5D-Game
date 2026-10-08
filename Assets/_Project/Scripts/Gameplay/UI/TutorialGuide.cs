using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Milestone 53: the first-day guide. A small card at the top left walks a new game through the basics, one step
    /// at a time (move, till, plant, water, talk to Oswin, practise on the dummy, sleep); each step ticks itself off
    /// when you do it. Afterwards, one-off tips appear the first time something comes up (low health, a second kind of
    /// seed, a fishing rod, animal feed). Key names follow your bindings and your controller. Progress is saved per
    /// slot; Settings → Interface → Tutorial hints turns it all off.
    /// </summary>
    public sealed class TutorialGuide : MonoBehaviour, ISaveable
    {
        public enum Step { Move, Till, Plant, Water, TalkToOswin, Practise, Sleep, Done }

        const float TipSeconds = 8f;

        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "tutorial";

        Step step;
        readonly HashSet<string> tipsShown = new();
        string tip;
        float tipUntil;
        float stepShownAt;
        Vector3 moveStart;
        bool moveStartSet;
        int dummyHits;
        bool restored;
        bool oldSaveChecked;
        float startedAt;

        Transform player;
        Combatant playerCombatant;
        Inventory inventory;
        PlayerFarmer farmer;
        DialogueRunner dialogue;
        GameStateService state;
        GUIStyle titleStyle, bodyStyle;

        [Serializable]
        sealed class State
        {
            public int step;
            public string[] tips;
        }

        public string SaveId => saveId;
        public Step Current => step;
        /// <summary>The one-off tip showing now (null when none).</summary>
        public string Tip => Time.unscaledTime < tipUntil ? tip : null;
        public bool Enabled => !Services.TryGet(out SettingsService settings) || settings.Current.tutorialHints;

        void OnEnable()
        {
            EventBus<FarmActionEvent>.Subscribe(OnFarmAction);
            EventBus<DamageDealtEvent>.Subscribe(OnDamage);
            EventBus<DayPassedEvent>.Subscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            EventBus<FarmActionEvent>.Unsubscribe(OnFarmAction);
            EventBus<DamageDealtEvent>.Unsubscribe(OnDamage);
            EventBus<DayPassedEvent>.Unsubscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                playerCombatant = playerObject.GetComponent<Combatant>();
                inventory = playerObject.GetComponent<Inventory>();
                farmer = playerObject.GetComponent<PlayerFarmer>();
            }
            Services.TryGet(out dialogue);
            state = Services.Get<GameStateService>();
            startedAt = Time.unscaledTime;
            stepShownAt = Time.unscaledTime;
        }

        // ---------- Progress ----------

        void Advance(Step from)
        {
            if (step != from) return;
            step = from + 1;
            stepShownAt = Time.unscaledTime;
            if (step == Step.Done) ShowTip("done", "That's the basics. Esc → How to play has the rest. Good luck out there.");
        }

        /// <summary>Tests: jump to a step.</summary>
        public void SetStep(Step value)
        {
            step = value;
            stepShownAt = Time.unscaledTime;
        }

        void OnFarmAction(FarmActionEvent evt)
        {
            if (evt.Action == FarmAction.Till) Advance(Step.Till);
            else if (evt.Action == FarmAction.Plant) Advance(Step.Plant);
            else if (evt.Action == FarmAction.Water) Advance(Step.Water);
        }

        void OnDamage(DamageDealtEvent evt)
        {
            if (step != Step.Practise || evt.Attacker == null || evt.Attacker != playerCombatant || evt.Target == null) return;
            if (!evt.Target.TryGetComponent(out EnemyController enemy) || enemy.Data == null || !enemy.Data.IsTrainingDummy) return;
            if (++dummyHits >= 3) Advance(Step.Practise);
        }

        void OnDayPassed(DayPassedEvent evt) => Advance(Step.Sleep);

        void Update()
        {
            if (player == null) return;
            // A save from before the guide existed: a game well under way doesn't need it.
            if (!oldSaveChecked && Time.unscaledTime - startedAt > 2f)
            {
                oldSaveChecked = true;
                if (!restored && step == Step.Move && Services.TryGet(out WorldClock clock) && clock.Day > 2) step = Step.Done;
            }

            if (step == Step.Move)
            {
                if (!moveStartSet) { moveStart = player.position; moveStartSet = true; }
                else if ((player.position - moveStart).magnitude > 6f) Advance(Step.Move);
            }
            if (step == Step.TalkToOswin && dialogue != null && dialogue.IsActive && dialogue.ConversationPartner != null &&
                dialogue.ConversationPartner.name == "Merchant")
                Advance(Step.TalkToOswin);

            CheckTips();
        }

        /// <summary>One-off tips the first time something comes up.</summary>
        void CheckTips()
        {
            if (state == null || state.Current != GameState.Playing) return;
            if (playerCombatant != null && playerCombatant.MaxHealth > 0f && playerCombatant.Health < playerCombatant.MaxHealth * 0.35f && !playerCombatant.IsDead)
                ShowTip("lowhealth", $"Low on health: press [{UITheme.KeyLabel("R")}] to eat, or let Maren the healer patch you up.");
            if (farmer != null && farmer.HasSeedChoice)
                ShowTip("seeds", $"You have more than one kind of seed: [{UITheme.KeyLabel("V")}] switches which one you plant.");
            if (inventory != null && HasItem("item_fishingrod"))
                ShowTip("rod", $"Fishing: stand at the pond's edge and press [{UITheme.KeyLabel("F")}]. Press again on the bite, then on the gold.");
            if (inventory != null && HasItem("item_animalfeed"))
                ShowTip("feed", $"Animal Feed goes in the trough in the pen. Fed animals give eggs and milk the next morning.");
        }

        bool HasItem(string id) => Services.TryGet(out GameDatabase database) && database.TryGet(id, out ItemData item) && inventory.CountOf(item) > 0;

        void ShowTip(string id, string text)
        {
            if (id != "done" && Tip != null) return; // one at a time: this one waits until the current tip has gone
            if (!tipsShown.Add(id)) return;
            if (step != Step.Done && id != "done" && id != "lowhealth") { tipsShown.Remove(id); return; } // after the first-day steps
            tip = text;
            tipUntil = Time.unscaledTime + TipSeconds;
        }

        string StepText => step switch
        {
            Step.Move => "Walk with [WASD] and look around with the mouse.",
            Step.Till => $"Your field is south-east of the square. Face a grass tile there and press [{UITheme.KeyLabel("F")}] to till it.",
            Step.Plant => $"Press [{UITheme.KeyLabel("F")}] on the tilled soil to plant Turnip Seeds.",
            Step.Water => $"Press [{UITheme.KeyLabel("F")}] again to water it. Watered crops grow overnight.",
            Step.TalkToOswin => $"Oswin the merchant has a \"!\" over his head. Walk up and press [{UITheme.KeyLabel("F")}] to talk.",
            Step.Practise => $"Practise on the training dummy: [{UITheme.KeyLabel("LMB")}] attacks, [{UITheme.KeyLabel("RMB")}] blocks, [{UITheme.KeyLabel("Shift")}] dodges.",
            Step.Sleep => $"When you're done for the day, sleep in your bed ([{UITheme.KeyLabel("F")}]). It saves, and your crops grow.",
            _ => null,
        };

        public string StepTextNow => StepText;

        // ---------- On screen ----------

        void OnGUI()
        {
            if (!Enabled || state == null || state.Current != GameState.Playing) return;
            string text = Tip ?? StepText;
            if (text == null) return;
            UITheme.Begin();
            EnsureStyles();
            float fade = Mathf.Clamp01((Time.unscaledTime - (Tip != null ? tipUntil - TipSeconds : stepShownAt)) * 3f);
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fade);
            const float width = 400f;
            float height = bodyStyle.CalcHeight(new GUIContent(text), width - 36f) + 50f;
            var card = new Rect(24f, 96f, width, height);
            UITheme.HudPanel(card);
            UITheme.Fill(new Rect(card.x, card.y, 3f, card.height), UITheme.InkGold);
            string title = Tip != null ? "TIP" : $"FIRST DAY  ·  {(int)step + 1}/{(int)Step.Done}";
            GUI.Label(new Rect(card.x + 18f, card.y + 8f, width - 36f, 22f), title, titleStyle);
            GUI.Label(new Rect(card.x + 18f, card.y + 32f, width - 36f, height - 40f), text, bodyStyle);
            GUI.color = old;
        }

        void EnsureStyles()
        {
            if (titleStyle != null && titleStyle.font == UITheme.Small.font) return;
            titleStyle = new GUIStyle(UITheme.Small) { normal = { textColor = UITheme.InkGold }, fontStyle = FontStyle.Bold };
            bodyStyle = new GUIStyle(UITheme.Body) { wordWrap = true, richText = true, normal = { textColor = UITheme.Text } };
        }

        // ---------- Save ----------

        public string CaptureState() => JsonUtility.ToJson(new State { step = (int)step, tips = new List<string>(tipsShown).ToArray() });

        public void RestoreState(string json)
        {
            var saved = JsonUtility.FromJson<State>(json);
            if (saved == null) return;
            step = (Step)Mathf.Clamp(saved.step, 0, (int)Step.Done);
            tipsShown.Clear();
            if (saved.tips != null) foreach (var id in saved.tips) tipsShown.Add(id);
            restored = true;
            stepShownAt = Time.unscaledTime;
        }
    }
}
