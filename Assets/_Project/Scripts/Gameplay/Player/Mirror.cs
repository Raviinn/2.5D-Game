using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public readonly struct MirrorOpenedEvent : IEvent
    {
        public readonly Mirror Mirror;
        public MirrorOpenedEvent(Mirror mirror) { Mirror = mirror; }
    }

    /// <summary>The homestead mirror: walk up and press E to change your look and name (the creator's controls).</summary>
    public sealed class Mirror : Interactable
    {
        [SerializeField] float range = 2f;

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;
            prompt.Text = "Change your look";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor) => Open();

        public void Open()
        {
            Services.Get<GameStateService>().SetState(GameState.InGameMenu);
            EventBus<MirrorOpenedEvent>.Raise(new MirrorOpenedEvent(this));
        }
    }
}
