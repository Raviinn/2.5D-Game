using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>What the player would do by pressing Interact, shown as an on-screen prompt.</summary>
    public struct InteractionPrompt
    {
        public string Text;
        /// <summary>False = informational only (e.g. "Growing: day 2/4"); pressing Interact does nothing.</summary>
        public bool CanInteract;
        /// <summary>Used to pick the closest interactable when several are in range.</summary>
        public float Distance;
        /// <summary>
        /// What kind of action this is ("till", "harvest"...). Holding Interact only repeats the kind of action the
        /// press started with, so holding F to harvest a row doesn't also replant and water each tile.
        /// </summary>
        public string ActionKey;
    }

    /// <summary>
    /// Base for anything the player can interact with (farm tiles, beds, later shops, NPCs, doors).
    /// Uses a registry and distance checks instead of colliders, so no physics layers are needed.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> Active = new();

        /// <summary>Set by the PlayerInteractor while this is the current target.</summary>
        public bool IsFocused { get; internal set; }

        /// <summary>
        /// True while this can't be used or seen at all (an NPC asleep indoors): skipped by the interactor, quest markers
        /// and the minimap. Unlike disabling the component, it keeps saving and listening to events.
        /// </summary>
        public bool Unavailable { get; set; }

        /// <summary>
        /// True if holding Interact repeats the action (working a row of farm tiles). One-shot actions
        /// (sleeping, talking, opening menus) must not repeat, or holding F would e.g. sleep through several nights.
        /// </summary>
        public virtual bool RepeatsWhileHeld => false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        protected virtual void OnEnable() => Active.Add(this);

        protected virtual void OnDisable()
        {
            Active.Remove(this);
            IsFocused = false;
        }

        /// <summary>Return false when this can't be targeted by the interactor right now (e.g. out of range).</summary>
        public abstract bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt);

        public abstract void Interact(PlayerInteractor interactor);
    }
}
