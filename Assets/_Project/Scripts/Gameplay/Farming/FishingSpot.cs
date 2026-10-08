using System;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Milestone 47: water you can fish (the pond). Standing at its edge with a Fishing Rod, F casts a line and
    /// PlayerFishing runs the bite-and-reel game. Which fish bite depends on the season and the time of day.
    /// </summary>
    public sealed class FishingSpot : Interactable
    {
        [Serializable]
        public struct Catch
        {
            public ItemData Fish;
            [Tooltip("Seasons it bites in. Empty = all year.")] public Season[] Seasons;
            [Tooltip("Only bites from dusk to dawn.")] public bool NightOnly;
            [Tooltip("Relative chance against the other fish that can bite now.")] public float Weight;
            [Tooltip("0 easy … 1 hard: a faster needle and a smaller target.")] [Range(0f, 1f)] public float Difficulty;
        }

        [SerializeField] Catch[] catches;
        [SerializeField] ItemData rod;
        [SerializeField, Tooltip("Water radius: you can fish standing within this + 1.6 m of the centre, outside the water.")] float radius = 3f;

        public ItemData Rod => rod;
        public float Radius => radius;

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            Vector3 offset = interactor.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance > radius + 1.6f || distance < radius - 0.5f) return false;
            prompt.Distance = Mathf.Max(0f, distance - radius);
            bool hasRod = rod == null || (interactor.Inventory != null && interactor.Inventory.CountOf(rod) > 0);
            if (hasRod)
            {
                prompt.Text = "Cast a line";
                prompt.CanInteract = true;
            }
            else
            {
                prompt.Text = $"You need a {rod.DisplayName} to fish (Oswin sells them)";
            }
            return true;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (interactor.TryGetComponent(out PlayerFishing fishing)) fishing.Cast(this);
        }

        /// <summary>A fish that bites here now (season, time of day), picked by weight. False if none can.</summary>
        public bool PickCatch(out Catch picked, bool night)
        {
            picked = default;
            if (catches == null) return false;
            var season = Calendar.Current;
            float total = 0f;
            foreach (var c in catches) if (Bites(c, season, night)) total += Mathf.Max(0.01f, c.Weight);
            if (total <= 0f) return false;
            float roll = UnityEngine.Random.value * total;
            foreach (var c in catches)
            {
                if (!Bites(c, season, night)) continue;
                roll -= Mathf.Max(0.01f, c.Weight);
                if (roll > 0f) continue;
                picked = c;
                return true;
            }
            return false;
        }

        static bool Bites(in Catch c, Season season, bool night) =>
            c.Fish != null && (!c.NightOnly || night) && (c.Seasons == null || c.Seasons.Length == 0 || Array.IndexOf(c.Seasons, season) >= 0);

        /// <summary>Where the line lands: a little way out from the bank toward the centre.</summary>
        public Vector3 CastPoint(Vector3 from)
        {
            Vector3 toCentre = transform.position - from;
            toCentre.y = 0f;
            Vector3 point = from + toCentre.normalized * Mathf.Min(toCentre.magnitude, 2.2f);
            point.y = transform.position.y + 0.05f;
            return point;
        }
    }
}
