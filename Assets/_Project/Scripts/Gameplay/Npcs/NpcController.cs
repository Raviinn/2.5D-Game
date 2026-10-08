using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Non-combat NPC: idles and turns to face the player when they come close; walks (the run clip, slowed) while an
    /// NpcSchedule moves it. Drives the NPC's sprite.
    /// </summary>
    public sealed class NpcController : MonoBehaviour, ICharacterAnimationSource
    {
        [SerializeField] float lookAtPlayerRange = 5f;
        [SerializeField, Tooltip("Degrees per second.")] float turnSpeed = 360f;

        Transform player;
        Quaternion restRotation;

        /// <summary>Set by NpcSchedule while walking between places.</summary>
        public bool Moving { get; set; }

        public CharacterAnim CurrentAnim => Moving ? CharacterAnim.Run : CharacterAnim.Idle;
        public AttackExecutor Attacks => null;
        public float AnimationSpeed => Moving ? 0.55f : 1f;

        void Awake() => restRotation = transform.rotation;

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        /// <summary>The way to face when nobody's near (a schedule place's facing, or the walking direction).</summary>
        public void SetRestRotation(Quaternion rotation, bool immediate)
        {
            restRotation = rotation;
            if (immediate) transform.rotation = rotation;
        }

        void Update()
        {
            var target = restRotation;
            if (player != null && !Moving)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < lookAtPlayerRange * lookAtPlayerRange && toPlayer.sqrMagnitude > 0.01f)
                    target = Quaternion.LookRotation(toPlayer);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
