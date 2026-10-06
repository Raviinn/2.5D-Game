using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>A bed: sleep until morning, restore health and stamina, and autosave (to the active save slot).</summary>
    public sealed class SleepSpot : Interactable
    {
        [SerializeField, Range(0, 23)] int wakeHour = 6;
        [SerializeField] float range = 2f;

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;

            prompt.Text = $"Sleep until {wakeHour:00}:00";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            var clock = Services.Get<WorldClock>();
            clock.AdvanceToHour(wakeHour);

            if (interactor.TryGetComponent(out Combatant combatant)) combatant.Heal(combatant.MaxHealth);
            if (interactor.TryGetComponent(out Stamina stamina)) stamina.Refill();

            bool saved = Services.Get<SaveService>().Save();
            EventBus<SleptEvent>.Raise(new SleptEvent(clock.Day, clock.Hour, saved));
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent(saved ? "You feel rested. Game saved." : "You feel rested."));
        }
    }
}
