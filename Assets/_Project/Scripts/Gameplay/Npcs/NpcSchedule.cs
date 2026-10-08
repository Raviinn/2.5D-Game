using System;
using Beast.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Beast.Gameplay
{
    /// <summary>
    /// A daily routine: at each entry's hour the NPC walks (along the navigation mesh) to that entry's place and faces
    /// the way the place faces. Indoor entries (asleep at home) hide the NPC once they reach the door: no sprite, no
    /// collider, no talking or trading, no markers. They stop walking while you stand next to them, so you can talk.
    /// After a jump in time (sleeping, loading, debug skips) they're placed straight at the current entry's spot.
    /// Not saved: where they are follows from the clock.
    /// </summary>
    [RequireComponent(typeof(NpcController))]
    public sealed class NpcSchedule : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Range(0f, 24f), Tooltip("Hour this part of the day starts (e.g. 8.5 = 08:30).")] public float StartHour;
            [Tooltip("Where to be: position and facing (an empty object in the scene).")] public Transform Place;
            [Tooltip("What they're doing, e.g. \"At the stall\" (shown in tests and tooltips).")] public string Activity;
            [Tooltip("Inside a building: hidden on arrival (the Place is the door).")] public bool Indoors;
        }

        const double JumpMinutes = 20.0;         // a bigger clock jump between frames snaps instead of walking
        const float ArriveDistance = 0.25f;

        [SerializeField] Entry[] entries;
        [SerializeField] float walkSpeed = 1.9f;
        [SerializeField, Tooltip("Stops walking while the player is this close.")] float pauseNearPlayer = 2f;

        NpcController controller;
        WorldClock clock;
        GameStateService state;
        WorldNavigation navigation;
        Transform player;
        Renderer[] renderers;
        Collider[] colliders;
        Interactable[] interactables;
        NavMeshPath path;
        readonly Vector3[] corners = new Vector3[32];
        int cornerCount, cornerIndex;
        int current = -1;
        double lastMinutes = double.NaN;
        float groundOffset;

        public string Activity => current >= 0 ? entries[current].Activity : null;
        public bool IsWalking { get; private set; }
        public bool IsIndoors { get; private set; }
        public int EntryCount => entries?.Length ?? 0;
        public Entry EntryAt(int index) => entries[index];
        /// <summary>The entry that applies at this hour of the day (0–24).</summary>
        public int EntryIndexAt(float hour)
        {
            if (entries == null || entries.Length == 0) return -1;
            int best = -1;
            float bestStart = float.NegativeInfinity;
            int latest = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].StartHour <= hour && entries[i].StartHour > bestStart) { best = i; bestStart = entries[i].StartHour; }
                if (entries[i].StartHour > entries[latest].StartHour) latest = i;
            }
            return best >= 0 ? best : latest; // before the first entry: still in last night's
        }

        void Awake()
        {
            controller = GetComponent<NpcController>();
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            interactables = GetComponentsInChildren<Interactable>();
            path = new NavMeshPath();
            groundOffset = GroundOffset();
        }

        void Start()
        {
            Services.TryGet(out clock);
            Services.TryGet(out state);
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        void Update()
        {
            if (clock == null && !Services.TryGet(out clock)) return;
            if (state != null && state.Current != GameState.Playing) return;
            if (entries == null || entries.Length == 0) return;

            double minutes = clock.TotalMinutes;
            bool jumped = double.IsNaN(lastMinutes) || Math.Abs(minutes - lastMinutes) > JumpMinutes;
            lastMinutes = minutes;

            int index = EntryIndexAt((float)(minutes / 60.0 % 24.0));
            if (index != current || jumped)
            {
                bool changed = index != current;
                current = index;
                if (jumped) Snap();
                else if (changed) BeginWalk();
            }
            if (IsWalking) Walk(Time.deltaTime);
        }

        /// <summary>Puts the NPC at the current entry's place right away (also hidden, if it's indoors).</summary>
        public void Snap()
        {
            if (current < 0) return;
            var entry = entries[current];
            IsWalking = false;
            cornerCount = 0;
            if (entry.Place != null)
            {
                transform.position = entry.Place.position + Vector3.up * groundOffset;
                Face(entry.Place.forward);
            }
            SetIndoors(entry.Indoors);
        }

        void BeginWalk()
        {
            var entry = entries[current];
            if (entry.Place == null) return;
            SetIndoors(false); // out of the door
            IsWalking = true;
            cornerCount = 0;
            if (navigation != null || Services.TryGet(out navigation))
                if (navigation.TryGetPath(Feet(), entry.Place.position, path, out _)) cornerCount = path.GetCornersNonAlloc(corners);
            cornerIndex = 1;
        }

        void Walk(float dt)
        {
            var entry = entries[current];
            if (player != null && Flat(player.position - transform.position).sqrMagnitude < pauseNearPlayer * pauseNearPlayer)
            {
                controller.Moving = false; // stop for a chat
                return;
            }

            Vector3 target = cornerCount > 1 && cornerIndex < cornerCount ? corners[cornerIndex] : entry.Place.position;
            Vector3 feet = Feet();
            Vector3 toTarget = Flat(target - feet);
            if (toTarget.magnitude < ArriveDistance)
            {
                if (cornerCount > 1 && cornerIndex < cornerCount - 1) { cornerIndex++; return; }
                Arrive(entry);
                return;
            }
            Vector3 step = toTarget.normalized * Mathf.Min(walkSpeed * dt, toTarget.magnitude);
            var next = feet + step;
            next.y = Mathf.MoveTowards(feet.y, target.y, walkSpeed * dt); // follow slopes and steps along the path
            transform.position = next + Vector3.up * groundOffset;
            Face(step);
            controller.Moving = true;
        }

        void Arrive(Entry entry)
        {
            IsWalking = false;
            controller.Moving = false;
            transform.position = entry.Place.position + Vector3.up * groundOffset;
            Face(entry.Place.forward);
            if (entry.Indoors) SetIndoors(true);
        }

        void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            controller.SetRestRotation(Quaternion.LookRotation(direction), immediate: IsWalking);
        }

        void SetIndoors(bool indoors)
        {
            IsIndoors = indoors;
            foreach (var r in renderers) if (r != null) r.enabled = !indoors;
            foreach (var c in colliders) if (c != null) c.enabled = !indoors;
            foreach (var i in interactables) if (i != null) i.Unavailable = indoors;
        }

        /// <summary>Height of the pivot above the feet (the NPC's capsule is centred on its pivot).</summary>
        float GroundOffset()
        {
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null) return capsule.height * 0.5f * transform.lossyScale.y - capsule.center.y;
            return 1f;
        }

        Vector3 Feet() => transform.position - Vector3.up * groundOffset;

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
