using Beast.Core;

namespace Beast.Gameplay
{
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter,
    }

    public enum Festival
    {
        None,
        /// <summary>Spring 8: seeds are half price.</summary>
        PlantingFestival,
        /// <summary>The last day of autumn: merchants pay half as much again for crops.</summary>
        HarvestFair,
    }

    /// <summary>Raised the morning a new season starts.</summary>
    public readonly struct SeasonChangedEvent : IEvent
    {
        public readonly Season Previous;
        public readonly Season Current;
        public SeasonChangedEvent(Season previous, Season current) { Previous = previous; Current = current; }
    }

    /// <summary>
    /// Milestone 46: the year. Four seasons of 14 days (day 1 is Spring 1), then it starts over. Pure arithmetic on
    /// WorldClock's day, so saves need nothing extra. SeasonKeeper (in the world) announces changes and festivals.
    /// </summary>
    public static class Calendar
    {
        public const int DaysPerSeason = 14;
        public const int DaysPerYear = DaysPerSeason * 4;
        public const int PlantingFestivalDay = 8;

        public static Season SeasonOf(int day) => (Season)((System.Math.Max(1, day) - 1) / DaysPerSeason % 4);
        public static int DayOfSeason(int day) => (System.Math.Max(1, day) - 1) % DaysPerSeason + 1;
        public static int YearOf(int day) => (System.Math.Max(1, day) - 1) / DaysPerYear + 1;

        public static Festival FestivalOn(int day)
        {
            var season = SeasonOf(day);
            int date = DayOfSeason(day);
            if (season == Season.Spring && date == PlantingFestivalDay) return Festival.PlantingFestival;
            if (season == Season.Autumn && date == DaysPerSeason) return Festival.HarvestFair;
            return Festival.None;
        }

        /// <summary>Today, from the WorldClock (Spring 1 if there's no clock).</summary>
        public static int Today => Services.TryGet(out WorldClock clock) ? clock.Day : 1;
        public static Season Current => SeasonOf(Today);
        public static Festival FestivalToday => FestivalOn(Today);

        public static string ShortName(Season season) => season switch
        {
            Season.Spring => "Spr",
            Season.Summer => "Sum",
            Season.Autumn => "Aut",
            _ => "Win",
        };

        public static string FestivalName(Festival festival) => festival switch
        {
            Festival.PlantingFestival => "Planting Festival",
            Festival.HarvestFair => "Harvest Fair",
            _ => string.Empty,
        };

        /// <summary>"Spring 3" / "Winter 14".</summary>
        public static string DateText(int day) => $"{SeasonOf(day)} {DayOfSeason(day)}";
    }
}
