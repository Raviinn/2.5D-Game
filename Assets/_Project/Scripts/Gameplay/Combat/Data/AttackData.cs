using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// One attack: timings, hitbox, damage and feel. Shared by player and enemies.
    /// Timings are in seconds; later, sprite animations are authored to match them.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Combat/Attack", fileName = "Attack")]
    public sealed class AttackData : GameData
    {
        [Header("Damage")]
        public float Damage = 10f;
        [Tooltip("Breaks the target's poise. At zero poise, the target is staggered.")]
        public float PoiseDamage = 10f;
        [Tooltip("Knockback speed applied to the target (m/s).")]
        public float Knockback = 3f;

        [Header("Timing (seconds)")]
        [Tooltip("Wind-up before the hitbox is live. For enemies, this is the telegraph.")]
        [Min(0f)] public float Startup = 0.12f;
        [Min(0.01f)] public float Active = 0.1f;
        [Min(0f)] public float Recovery = 0.25f;
        [Tooltip("Time after the active phase ends before a buffered follow-up attack may start.")]
        [Min(0f)] public float ComboCancelDelay = 0f;
        [Tooltip("Time from the start of the attack after which a dodge can cancel it. 0 = any time.")]
        [Min(0f)] public float DodgeCancelAfter = 0f;

        [Header("Hitbox (local space of the attacker)")]
        public Vector3 HitboxCenter = new(0f, 0f, 1.1f);
        public Vector3 HitboxSize = new(1.8f, 1.2f, 1.4f);

        [Header("Movement")]
        [Tooltip("Forward step during startup + active. Stops short of a soft-aimed target.")]
        [Min(0f)] public float Lunge = 0.6f;

        [Header("Feel")]
        [Tooltip("Real-time freeze on hit, in seconds.")]
        [Min(0f)] public float HitStop = 0.05f;
        [Min(0f)] public float CameraShake = 0.1f;

        public float ActiveEnd => Startup + Active;
        public float TotalDuration => Startup + Active + Recovery;
    }
}
