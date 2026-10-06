using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum Team
    {
        Player,
        Enemy,
    }

    public enum HitResult
    {
        Ignored,     // invulnerable (dodge i-frames) or already dead
        Hit,
        Blocked,
        Parried,
        GuardBroken, // blocked, but ran out of stamina
        Killed,
    }

    public struct DamageInfo
    {
        public float Damage;
        public float PoiseDamage;
        public Vector3 Knockback;   // velocity, m/s
        public Combatant Attacker;
    }

    // ---------- Events ----------

    public readonly struct DamageDealtEvent : IEvent
    {
        public readonly Combatant Target;
        public readonly Combatant Attacker;
        public readonly float Damage;
        public readonly HitResult Result;

        public DamageDealtEvent(Combatant target, Combatant attacker, float damage, HitResult result)
        {
            Target = target; Attacker = attacker; Damage = damage; Result = result;
        }
    }

    /// <summary>Raised when any combatant dies.</summary>
    public readonly struct CombatantDiedEvent : IEvent
    {
        public readonly Combatant Combatant;
        public CombatantDiedEvent(Combatant combatant) { Combatant = combatant; }
    }

    public readonly struct CameraShakeEvent : IEvent
    {
        public readonly float Strength;
        public readonly float Duration;
        public CameraShakeEvent(float strength, float duration) { Strength = strength; Duration = duration; }
    }

    public readonly struct LockOnChangedEvent : IEvent
    {
        public readonly Combatant Target; // null when lock-on is released
        public LockOnChangedEvent(Combatant target) { Target = target; }
    }
}
