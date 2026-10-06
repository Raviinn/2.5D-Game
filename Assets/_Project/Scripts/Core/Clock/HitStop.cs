using UnityEngine;

namespace Beast.Core
{
    /// <summary>
    /// Briefly slows time on impact to sell weight. Only acts while Playing,
    /// and yields to GameStateService (pausing mid-hit-stop is safe).
    /// </summary>
    public sealed class HitStop : MonoBehaviour
    {
        [SerializeField, Tooltip("Time scale during a hit-stop. Near zero = a freeze frame.")]
        float slowTimeScale = 0.02f;

        GameStateService state;
        float remaining;

        public void Initialize(GameStateService state)
        {
            this.state = state;
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        }

        void OnDestroy() => EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);

        /// <param name="duration">Real-time seconds.</param>
        public void Trigger(float duration)
        {
            if (duration <= 0f || state.Current != GameState.Playing) return;
            remaining = Mathf.Max(remaining, duration);
            Time.timeScale = slowTimeScale;
        }

        void Update()
        {
            if (remaining <= 0f) return;

            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f && state.Current == GameState.Playing)
                Time.timeScale = 1f;
        }

        // GameStateService has already set the correct time scale for the new state.
        void OnGameStateChanged(GameStateChangedEvent _) => remaining = 0f;
    }
}
