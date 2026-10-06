using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Logs core events to the Console to prove the EventBus works. Safe to delete later.</summary>
    public sealed class EventProbe : MonoBehaviour
    {
        void OnEnable()
        {
            EventBus<DayPassedEvent>.Subscribe(OnDayPassed);
            EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
            EventBus<GameSavedEvent>.Subscribe(OnSaved);
            EventBus<GameLoadedEvent>.Subscribe(OnLoaded);
            EventBus<CombatantDiedEvent>.Subscribe(OnDied);
        }

        void OnDisable()
        {
            EventBus<DayPassedEvent>.Unsubscribe(OnDayPassed);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
            EventBus<GameSavedEvent>.Unsubscribe(OnSaved);
            EventBus<GameLoadedEvent>.Unsubscribe(OnLoaded);
            EventBus<CombatantDiedEvent>.Unsubscribe(OnDied);
        }

        void OnDayPassed(DayPassedEvent evt) => Debug.Log($"[EventProbe] Day {evt.Day} has begun.");
        void OnStateChanged(GameStateChangedEvent evt) => Debug.Log($"[EventProbe] State {evt.Previous} → {evt.Current}");
        void OnSaved(GameSavedEvent evt) => Debug.Log($"[EventProbe] Saved slot {evt.Slot}");
        void OnLoaded(GameLoadedEvent evt) => Debug.Log($"[EventProbe] Loaded slot {evt.Slot}");
        void OnDied(CombatantDiedEvent evt) => Debug.Log($"[EventProbe] {evt.Combatant.name} died.");
    }
}
