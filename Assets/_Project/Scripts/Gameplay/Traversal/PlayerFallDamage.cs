using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Raised when the player lands after a drop of at least a metre (sounds, tests).</summary>
    public readonly struct PlayerLandedEvent : IEvent
    {
        /// <summary>Metres from the highest point of the fall to the landing.</summary>
        public readonly float Drop;
        public readonly float Damage;
        public PlayerLandedEvent(float drop, float damage) { Drop = drop; Damage = damage; }
    }

    /// <summary>
    /// Fall damage: landing from higher than the safe height hurts, scaled by height, and stuns you briefly.
    /// The fall is measured from its highest point, so a jump's arc counts; grabbing a ledge or wall on the way down
    /// (or any climbing) starts the measurement again.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(Combatant))]
    public sealed class PlayerFallDamage : MonoBehaviour
    {
        [SerializeField, Tooltip("Drops up to this height (m) are harmless.")] float safeHeight = 6f;
        [SerializeField, Tooltip("A drop this high (m) takes all your health.")] float lethalHeight = 18f;
        [SerializeField, Tooltip("Seconds stunned by a landing just over the safe height / a near-lethal one.")]
        Vector2 stunRange = new(0.35f, 1f);
        [SerializeField, Tooltip("A move longer than this (m) in one frame is a teleport (respawn, load), not a fall.")]
        float teleportDistance = 4f;

        const float ReportDrop = 1f;

        PlayerMotor motor;
        Combatant combatant;
        bool airborne;
        float peakY;
        Vector3 lastPosition;

        /// <summary>Height of the current fall so far (0 on the ground).</summary>
        public float CurrentDrop => airborne ? Mathf.Max(0f, peakY - transform.position.y) : 0f;
        public float SafeHeight => safeHeight;
        public float LethalHeight => lethalHeight;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            combatant = GetComponent<Combatant>();
        }

        void OnEnable()
        {
            EventBus<GameLoadedEvent>.Subscribe(OnGameLoaded);
            ResetFall();
        }

        void OnDisable() => EventBus<GameLoadedEvent>.Unsubscribe(OnGameLoaded);

        void OnGameLoaded(GameLoadedEvent evt) => ResetFall();

        void ResetFall()
        {
            airborne = false;
            lastPosition = transform.position;
            peakY = lastPosition.y;
        }

        /// <summary>Damage for a drop of this height (0 up to the safe height, all your health at the lethal height).</summary>
        public float DamageFor(float drop) =>
            combatant.MaxHealth * Mathf.Clamp01((drop - safeHeight) / Mathf.Max(0.01f, lethalHeight - safeHeight));

        // After PlayerMotor has moved the character this frame.
        void LateUpdate()
        {
            Vector3 position = transform.position;
            bool teleported = (position - lastPosition).sqrMagnitude > teleportDistance * teleportDistance;
            lastPosition = position;

            if (teleported || motor.Suspended || combatant.IsDead)
            {
                airborne = false;
                peakY = position.y;
                return;
            }

            if (!motor.IsGrounded)
            {
                if (!airborne) { airborne = true; peakY = position.y; }
                peakY = Mathf.Max(peakY, position.y);
                return;
            }

            if (!airborne) return;
            airborne = false;
            Land(peakY - position.y);
        }

        void Land(float drop)
        {
            if (drop < ReportDrop) return;
            float damage = DamageFor(drop);
            if (damage > 0f)
            {
                combatant.ReceiveWorldDamage(damage);
                if (!combatant.IsDead)
                {
                    float t = Mathf.InverseLerp(safeHeight, lethalHeight, drop);
                    combatant.Stagger(Mathf.Lerp(stunRange.x, stunRange.y, t));
                }
                EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent(Mathf.Lerp(0.3f, 0.7f, Mathf.InverseLerp(safeHeight, lethalHeight, drop)), 0.25f));
            }
            EventBus<PlayerLandedEvent>.Raise(new PlayerLandedEvent(drop, damage));
        }
    }
}
