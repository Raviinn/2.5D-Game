using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A plantable crop. Grows one step per WATERED day; dries out on unwatered days.
    /// Sheet layout (one row): growth stages..., ripe, wilted, dead.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Farming/Crop", fileName = "Crop")]
    public sealed class CropData : GameData
    {
        public string DisplayName = "New Crop";

        [Header("Items")]
        public ItemData Seed;
        public ItemData Produce;
        [Min(1)] public int ProduceMin = 1;
        [Min(1)] public int ProduceMax = 1;
        [Tooltip("Farming XP per unit harvested.")]
        [Min(0)] public int HarvestXp = 5;

        [Header("Growth (in watered days)")]
        [Tooltip("Watered days spent in each growth stage before ripening. Length = number of growth stages.")]
        public int[] DaysPerStage = { 1, 1, 2 };
        [Tooltip("0 = single harvest (tile is cleared). Otherwise watered days until ripe again.")]
        [Min(0)] public int RegrowDays;

        [Header("Seasons (Milestone 46)")]
        [Tooltip("Seasons it grows in. Empty = all year. Out of season it can't be planted, and the change of season kills it.")]
        public Season[] Seasons;

        [Header("Neglect")]
        [Tooltip("Unwatered days in a row before the crop wilts (stops growing, recovers when watered).")]
        [Min(1)] public int WiltAfterDryDays = 2;
        [Tooltip("Unwatered days in a row before the crop dies and must be cleared.")]
        [Min(1)] public int DieAfterDryDays = 3;

        [Header("Visuals")]
        public Texture2D Sheet;
        public int FrameSize = 32;
        public float PixelsPerUnit = 40f;

        public int GrowthStageCount => DaysPerStage?.Length ?? 0;

        public bool GrowsIn(Season season) => Seasons == null || Seasons.Length == 0 || System.Array.IndexOf(Seasons, season) >= 0;

        /// <summary>"spring and summer" / "autumn" / "all year".</summary>
        public string SeasonsText
        {
            get
            {
                if (Seasons == null || Seasons.Length == 0 || Seasons.Length >= 4) return "all year";
                var names = System.Array.ConvertAll(Seasons, s => s.ToString().ToLowerInvariant());
                return names.Length == 1 ? names[0] : string.Join(", ", names, 0, names.Length - 1) + " and " + names[^1];
            }
        }
        public int RipeFrame => GrowthStageCount;
        public int WiltedFrame => GrowthStageCount + 1;
        public int DeadFrame => GrowthStageCount + 2;
        public float WorldSize => FrameSize / PixelsPerUnit;

        public int TotalGrowDays
        {
            get
            {
                int total = 0;
                if (DaysPerStage != null) foreach (int days in DaysPerStage) total += Mathf.Max(1, days);
                return total;
            }
        }

        /// <summary>Growth stage index for a number of watered days (clamped below ripe).</summary>
        public int StageFor(int growth)
        {
            int elapsed = 0;
            for (int i = 0; i < GrowthStageCount; i++)
            {
                elapsed += Mathf.Max(1, DaysPerStage[i]);
                if (growth < elapsed) return i;
            }
            return Mathf.Max(0, GrowthStageCount - 1);
        }
    }
}
