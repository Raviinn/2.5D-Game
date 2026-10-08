using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum AnimalKind
    {
        Hen,
        Cow,
    }

    /// <summary>
    /// Milestone 45: a hen or a cow in the homestead pen. It wanders the pen; F pets it (once a day) or collects what
    /// it's made (an egg, milk). Fed from the trough (FeedTrough) today → tomorrow morning there's something to
    /// collect. Petting on fed days slowly builds affection; a content cow (affection 7+) gives two pails.
    /// Two hungry days in a row and it sulks (affection drops). Saved per animal.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FarmAnimal : Interactable, ISaveable, ICharacterAnimationSource
    {
        public const int MaxAffection = 10;
        public const int ContentAffection = 7;

        /// <summary>Every animal in the world (the trough feeds them all).</summary>
        public static readonly List<FarmAnimal> All = new();

        [SerializeField] AnimalKind kind = AnimalKind.Hen;
        [SerializeField] string displayName = "Hen";
        [SerializeField] ItemData product;
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "animal.hen";
        [SerializeField] float range = 1.8f;

        [Header("Wandering")]
        [SerializeField, Tooltip("The pen: it stays inside this box (world centre, half size).")] Vector3 penCentre;
        [SerializeField] Vector2 penHalfSize = new(2.5f, 2.5f);
        [SerializeField] float walkSpeed = 1f;
        [SerializeField, Tooltip("Degrees per second.")] float turnSpeed = 300f;

        bool fed, petted, productReady;
        int affection = 3, hungryDays;
        Vector3 wanderTarget;
        float nextWander;
        bool moving;
        Transform player;

        [Serializable]
        sealed class State
        {
            public bool fed, petted, productReady;
            public int affection, hungryDays;
        }

        public string SaveId => saveId;
        public AnimalKind Kind => kind;
        public string DisplayName => displayName;
        public ItemData Product => product;
        public bool Fed => fed;
        public bool Petted => petted;
        public bool ProductReady => productReady;
        public int Affection => affection;
        public bool Content => affection >= ContentAffection;

        public CharacterAnim CurrentAnim => moving ? CharacterAnim.Run : CharacterAnim.Idle;
        public AttackExecutor Attacks => null;
        public float AnimationSpeed => moving ? 0.6f : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        protected override void OnEnable()
        {
            base.OnEnable();
            All.Add(this);
            EventBus<DayPassedEvent>.Subscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            All.Remove(this);
            EventBus<DayPassedEvent>.Unsubscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
            if (penCentre == Vector3.zero) penCentre = transform.position;
            wanderTarget = transform.position;
            nextWander = Time.time + UnityEngine.Random.Range(0.5f, 3f);
        }

        // ---------- Wandering ----------

        void Update()
        {
            float dt = Time.deltaTime;
            moving = false;
            if (dt <= 0f) return;
            // Stand still for the player (so you can pet it), otherwise amble between random spots in the pen.
            if (player != null && Flat(player.position - transform.position).sqrMagnitude < 2.2f * 2.2f)
            {
                Face(Flat(player.position - transform.position), dt);
                return;
            }
            if (Time.time >= nextWander)
            {
                nextWander = Time.time + UnityEngine.Random.Range(3f, 7f);
                wanderTarget = penCentre + new Vector3(UnityEngine.Random.Range(-penHalfSize.x, penHalfSize.x), 0f,
                    UnityEngine.Random.Range(-penHalfSize.y, penHalfSize.y));
            }
            Vector3 to = Flat(wanderTarget - transform.position);
            if (to.magnitude < 0.15f) return;
            moving = true;
            Face(to, dt);
            Vector3 step = to.normalized * Mathf.Min(walkSpeed * dt, to.magnitude);
            Vector3 next = transform.position + step;
            // Never leave the pen (if moved by hand outside it, walk back in).
            next.x = Mathf.Clamp(next.x, penCentre.x - penHalfSize.x - 0.5f, penCentre.x + penHalfSize.x + 0.5f);
            next.z = Mathf.Clamp(next.z, penCentre.z - penHalfSize.y - 0.5f, penCentre.z + penHalfSize.y + 0.5f);
            transform.position = next;
        }

        void Face(Vector3 direction, float dt)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * dt);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        /// <summary>The pen it keeps to (set by the setup).</summary>
        public void SetPen(Vector3 centre, Vector2 halfSize)
        {
            penCentre = centre;
            penHalfSize = halfSize;
        }

        // ---------- Interaction ----------

        string Mood => hungryDays >= 1 && !fed ? "hungry" : affection >= ContentAffection ? "content" : affection >= 3 ? "calm" : "wary";

        string CollectVerb => kind == AnimalKind.Cow ? $"Milk {displayName}" : "Collect the egg";

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;
            prompt.Distance = distance;
            if (productReady)
            {
                prompt.Text = CollectVerb;
                prompt.CanInteract = true;
            }
            else if (!petted)
            {
                prompt.Text = $"Pet {displayName}";
                prompt.CanInteract = true;
            }
            else
            {
                prompt.Text = $"{displayName} · {Mood}{(fed ? ", fed today" : ", hungry: fill the trough")}";
                prompt.CanInteract = false;
            }
            return true;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (productReady) Collect(interactor.Inventory);
            else if (!petted) Pet();
        }

        /// <summary>Takes what it's made into the bag (or onto the ground if the bag is full).</summary>
        public int Collect(Inventory inventory)
        {
            if (!productReady || product == null) return 0;
            int count = kind == AnimalKind.Cow && Content ? 2 : 1;
            productReady = false;
            int leftover = inventory != null ? inventory.Add(product, count) : count;
            if (leftover > 0) ItemPickup.SpawnItem(transform.position + Vector3.up * 0.3f, product, leftover, 0.5f);
            var audio = GameAudio.Instance;
            if (audio != null && audio.Library != null) audio.Play(audio.Library.Harvest, transform.position);
            var progression = inventory != null ? inventory.GetComponent<PlayerProgression>() : null;
            if (progression != null) progression.AddXp(Discipline.Farming, 4 * count);
            return count;
        }

        public void Pet()
        {
            if (petted) return;
            petted = true;
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent(affection >= ContentAffection
                ? $"{displayName} leans into your hand."
                : $"{displayName} seems to like that."));
        }

        /// <summary>The trough was filled: this animal has eaten today.</summary>
        public void Feed() => fed = true;

        void OnDayPassed(DayPassedEvent evt)
        {
            if (fed)
            {
                productReady = true; // one waiting at most; uncollected eggs don't pile up
                hungryDays = 0;
                if (petted) affection = Mathf.Min(MaxAffection, affection + 1);
            }
            else
            {
                hungryDays++;
                if (hungryDays >= 2) affection = Mathf.Max(0, affection - 1);
            }
            fed = false;
            petted = false;
        }

        // ---------- Save ----------

        public string CaptureState() => JsonUtility.ToJson(new State
        {
            fed = fed, petted = petted, productReady = productReady, affection = affection, hungryDays = hungryDays,
        });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            if (state == null) return;
            fed = state.fed;
            petted = state.petted;
            productReady = state.productReady;
            affection = Mathf.Clamp(state.affection, 0, MaxAffection);
            hungryDays = state.hungryDays;
        }
    }
}
