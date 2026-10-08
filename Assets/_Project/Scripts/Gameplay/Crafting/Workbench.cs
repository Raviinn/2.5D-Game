using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>The homestead workbench: walk up and press E to brew, cook and smith (the crafting screen).</summary>
    public sealed class Workbench : Interactable
    {
        [SerializeField] RecipeBook recipes;
        [SerializeField] float range = 2.2f;
        [SerializeField, Tooltip("Optional: a chest whose contents can be used as ingredients (the homestead chest).")]
        StorageChest storage;

        public RecipeBook Recipes => recipes;
        /// <summary>The linked chest's contents, or null.</summary>
        public Inventory Storage => storage != null ? storage.Contents : null;

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (recipes == null) return false;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;

            prompt.Text = "Use workbench";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor) => Open();

        public void Open()
        {
            if (recipes == null) return;
            Services.Get<GameStateService>().SetState(GameState.InGameMenu);
            EventBus<WorkbenchOpenedEvent>.Raise(new WorkbenchOpenedEvent(this));
        }
    }
}
