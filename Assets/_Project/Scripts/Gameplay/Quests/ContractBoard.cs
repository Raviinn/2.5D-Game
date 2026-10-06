using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>A notice board of repeatable contracts (each once per day). Opens the contracts screen.</summary>
    public sealed class ContractBoard : Interactable
    {
        [SerializeField] string boardName = "Contracts Board";
        [SerializeField] QuestData[] contracts;
        [SerializeField] float range = 2.5f;

        public string BoardName => boardName;
        public QuestData[] Contracts => contracts;
        public bool Posts(QuestData quest) => contracts != null && System.Array.IndexOf(contracts, quest) >= 0;

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;

            prompt.Text = $"Read the {boardName}";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            Services.Get<GameStateService>().SetState(GameState.InGameMenu);
            EventBus<ContractBoardOpenedEvent>.Raise(new ContractBoardOpenedEvent(this));
        }
    }

    public readonly struct ContractBoardOpenedEvent : IEvent
    {
        public readonly ContractBoard Board;
        public ContractBoardOpenedEvent(ContractBoard board) { Board = board; }
    }
}
