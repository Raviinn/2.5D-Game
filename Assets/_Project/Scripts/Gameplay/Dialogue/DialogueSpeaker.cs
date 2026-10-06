using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Makes an NPC talkable: interacting starts its Ink knot. Also tells the quest UI which quests
    /// this NPC gives (shows "!") and takes back (shows "?").
    /// </summary>
    public sealed class DialogueSpeaker : Interactable
    {
        [SerializeField] SpeakerData speaker;
        [SerializeField, Tooltip("Ink knot this NPC's conversation starts at, e.g. \"oswin\".")] string knot;
        [SerializeField] float range = 2.5f;

        [Header("Quest markers")]
        [SerializeField] QuestData[] questsOffered;
        [SerializeField] QuestData[] questsTurnedIn;

        public string DisplayName => speaker != null ? speaker.DisplayName : name;

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;

            prompt.Text = $"Talk to {DisplayName}";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (Services.TryGet(out DialogueRunner runner)) runner.StartConversation(knot, this);
            else Debug.LogWarning("[Dialogue] No DialogueRunner in the scene.");
        }

        /// <summary>True if this NPC is where the quest is handed in (also used to point quest waypoints here).</summary>
        public bool TurnsIn(QuestData quest) => questsTurnedIn != null && System.Array.IndexOf(questsTurnedIn, quest) >= 0;

        /// <summary>'?' = a quest is ready to hand in here, '!' = a new quest is available, '\0' = nothing.</summary>
        public char QuestMarker(QuestLog log)
        {
            if (log == null) return '\0';
            if (questsTurnedIn != null)
                foreach (var quest in questsTurnedIn)
                    if (quest != null && log.StatusOf(quest) == QuestStatus.Ready) return '?';
            if (questsOffered != null)
                foreach (var quest in questsOffered)
                    if (log.CanStart(quest)) return '!';
            return '\0';
        }
    }
}
