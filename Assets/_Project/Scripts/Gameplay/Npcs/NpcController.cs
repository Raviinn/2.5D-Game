using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Minimal non-combat NPC: idles and turns to face the player when they come close.
    /// Drives the NPC's sprite. Schedules and dialogue come later.
    /// </summary>
    public sealed class NpcController : MonoBehaviour, ICharacterAnimationSource
    {
        [SerializeField] float lookAtPlayerRange = 5f;
        [SerializeField, Tooltip("Degrees per second.")] float turnSpeed = 360f;

        Transform player;
        Quaternion restRotation;

        public CharacterAnim CurrentAnim => CharacterAnim.Idle;
        public AttackExecutor Attacks => null;
        public float AnimationSpeed => 1f;

        void Awake() => restRotation = transform.rotation;

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        void Update()
        {
            var target = restRotation;
            if (player != null)
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
