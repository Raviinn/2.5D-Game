using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Picks the closest Interactable each frame and triggers it on Interact (the HUD shows its prompt).
    /// Holding Interact repeats for interactables that allow it, so a row of farm tiles can be worked while walking.
    /// A hold only repeats the kind of action it started with (hold to harvest = harvest only, never replant).
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, Tooltip("Real-time seconds between repeats while Interact is held.")] float repeatInterval = 0.3f;

        PlayerCombat combat;
        GameStateService gameState;
        InputAction interactAction;
        float nextRepeatTime;
        bool holdIsValid;
        string holdAction;

        public Inventory Inventory { get; private set; }
        public Interactable Focus { get; private set; }
        /// <summary>Set while something else owns the Interact key (fishing): nothing is focused or used.</summary>
        public bool Busy { get; set; }
        public InteractionPrompt FocusPrompt { get; private set; }

        void Awake()
        {
            Inventory = GetComponent<Inventory>();
            combat = GetComponent<PlayerCombat>();
        }

        void Start()
        {
            interactAction = Services.Get<InputService>().Interact;
            gameState = Services.Get<GameStateService>();
        }

        void OnDisable() => SetFocus(null, default);

        void Update()
        {
            // Only during gameplay, and not mid-attack, mid-dodge, staggered or dead.
            if (Busy || gameState.Current != GameState.Playing || (combat != null && !combat.CanUseItems))
            {
                SetFocus(null, default);
                holdIsValid = false;
                return;
            }

            Interactable best = null;
            var bestPrompt = default(InteractionPrompt);
            foreach (var candidate in Interactable.Active)
            {
                if (candidate.Unavailable || !candidate.TryGetPrompt(this, out var prompt)) continue;
                if (best != null && prompt.Distance >= bestPrompt.Distance) continue;
                best = candidate;
                bestPrompt = prompt;
            }
            SetFocus(best, bestPrompt);

            // Holding only repeats a press that began during gameplay (not one left over from closing a menu).
            if (interactAction.WasPressedThisFrame()) holdIsValid = true;
            else if (!interactAction.IsPressed()) holdIsValid = false;

            if (Focus == null || !FocusPrompt.CanInteract) return;
            bool pressed = interactAction.WasPressedThisFrame();
            if (pressed) holdAction = FocusPrompt.ActionKey;
            bool repeat = Focus.RepeatsWhileHeld && holdIsValid && interactAction.IsPressed() && Time.unscaledTime >= nextRepeatTime &&
                          FocusPrompt.ActionKey == holdAction;
            if (pressed || repeat)
            {
                Focus.Interact(this);
                nextRepeatTime = Time.unscaledTime + repeatInterval;
            }
        }

        void SetFocus(Interactable target, InteractionPrompt prompt)
        {
            if (Focus != target)
            {
                if (Focus != null) Focus.IsFocused = false;
                Focus = target;
                if (Focus != null) Focus.IsFocused = true;
            }
            FocusPrompt = prompt;
        }
    }
}
