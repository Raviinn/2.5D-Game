using System;
using UnityEngine;

namespace Beast.Core
{
    /// <summary>
    /// In-game time. Advances only while Playing and raises HourChanged / DayPassed.
    /// Every crossed hour and day is raised individually, so skipping time (sleeping) still ticks farms per day.
    /// </summary>
    public sealed class WorldClock : MonoBehaviour, ISaveable, ISaveSummarySource
    {
        public const int MinutesPerDay = 24 * 60;

        double totalMinutes;
        double startMinutes;
        float minutesPerRealSecond;
        GameStateService state;

        public string SaveId => "world.clock";
        public double TotalMinutes => totalMinutes;
        public int Day => (int)(totalMinutes / MinutesPerDay) + 1;
        public int Hour => (int)(totalMinutes / 60 % 24);
        public int Minute => (int)(totalMinutes % 60);
        /// <summary>0 at midnight, 0.5 at noon. Useful for sun rotation and lighting.</summary>
        public float DayProgress01 => (float)(totalMinutes % MinutesPerDay / MinutesPerDay);

        [Serializable]
        struct State
        {
            public double totalMinutes;
        }

        public void Initialize(GameConfig config, GameStateService state, SaveService save)
        {
            this.state = state;
            minutesPerRealSecond = MinutesPerDay / config.RealSecondsPerGameDay;
            startMinutes = config.StartHour * 60;
            totalMinutes = startMinutes;
            save.Register(this);
            EventBus<NewGameEvent>.Subscribe(OnNewGame);
        }

        void OnDestroy() => EventBus<NewGameEvent>.Unsubscribe(OnNewGame);

        void OnNewGame(NewGameEvent evt) => totalMinutes = startMinutes;

        void Update()
        {
            if (state.Current != GameState.Playing) return;
            AdvanceMinutes(Time.deltaTime * minutesPerRealSecond);
        }

        public void AdvanceMinutes(double minutes)
        {
            if (minutes <= 0) return;

            long previousHour = (long)(totalMinutes / 60);
            totalMinutes += minutes;
            long currentHour = (long)(totalMinutes / 60);

            for (long h = previousHour + 1; h <= currentHour; h++)
            {
                int day = (int)(h / 24) + 1;
                int hour = (int)(h % 24);
                EventBus<HourChangedEvent>.Raise(new HourChangedEvent(day, hour));
                if (hour == 0) EventBus<DayPassedEvent>.Raise(new DayPassedEvent(day));
            }
        }

        /// <summary>Skips ahead to the next time the clock reads this hour (e.g. sleeping until 6:00). Raises every crossed hour/day.</summary>
        public void AdvanceToHour(int hour)
        {
            double dayStart = Math.Floor(totalMinutes / MinutesPerDay) * MinutesPerDay;
            double target = dayStart + Mathf.Clamp(hour, 0, 23) * 60;
            if (target <= totalMinutes) target += MinutesPerDay;
            AdvanceMinutes(target - totalMinutes);
        }

        public void Describe(SaveSummary summary)
        {
            summary.day = Day;
            summary.hour = Hour;
            summary.minute = Minute;
        }

        public string CaptureState() => JsonUtility.ToJson(new State { totalMinutes = totalMinutes });

        // Restoring sets time directly; it does not replay missed hour/day events.
        public void RestoreState(string json) => totalMinutes = JsonUtility.FromJson<State>(json).totalMinutes;
    }
}
