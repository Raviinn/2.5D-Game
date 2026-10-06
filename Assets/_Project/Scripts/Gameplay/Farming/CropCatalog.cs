using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Seed → crop lookup, built once from the GameDatabase.</summary>
    public static class CropCatalog
    {
        static Dictionary<ItemData, CropData> bySeed;
        static HashSet<ItemData> produce;

        public static CropData ForSeed(ItemData seed)
        {
            if (seed == null) return null;
            Build();
            return bySeed.TryGetValue(seed, out var crop) ? crop : null;
        }

        public static bool IsPlantable(ItemData item) => ForSeed(item) != null;

        /// <summary>True if some crop produces this item (e.g. Turnip) — i.e. you get it from the field.</summary>
        public static bool IsProduce(ItemData item)
        {
            if (item == null) return false;
            Build();
            return produce.Contains(item);
        }

        static void Build()
        {
            if (bySeed != null) return;
            bySeed = new Dictionary<ItemData, CropData>();
            produce = new HashSet<ItemData>();
            if (!Services.TryGet(out GameDatabase database)) return;

            foreach (var data in database.All)
            {
                if (data is not CropData crop) continue;
                if (crop.Seed != null) bySeed[crop.Seed] = crop;
                if (crop.Produce != null) produce.Add(crop.Produce);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            bySeed = null;
            produce = null;
        }
    }
}
