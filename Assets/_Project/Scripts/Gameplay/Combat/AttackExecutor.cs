using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Runs one attack at a time through Startup → Active → Recovery, detects hits with a
    /// non-allocating box overlap, and returns the lunge movement for each frame.
    /// Used by both the player and enemies.
    /// </summary>
    public sealed class AttackExecutor
    {
        public enum Phase { None, Startup, Active, Recovery }

        static readonly Collider[] overlapBuffer = new Collider[32];

        readonly Combatant owner;
        readonly Transform transform;
        readonly int hitMask;
        readonly HashSet<Combatant> alreadyHit = new();

        float lungeRemaining;
        float lungeSpeed;

        public AttackData Current { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsAttacking => CurrentPhase != Phase.None;

        /// <summary>Raised for every target hit (not for Ignored).</summary>
        public event Action<HitResult, Combatant> Landed;

        public AttackExecutor(Combatant owner, int hitMask)
        {
            this.owner = owner;
            transform = owner.transform;
            this.hitMask = hitMask;
        }

        public void Begin(AttackData attack, float maxLunge = float.PositiveInfinity)
        {
            Current = attack;
            Elapsed = 0f;
            CurrentPhase = Phase.Startup;
            alreadyHit.Clear();

            lungeRemaining = Mathf.Clamp(attack.Lunge, 0f, maxLunge);
            lungeSpeed = attack.ActiveEnd > 0f ? attack.Lunge / attack.ActiveEnd : 0f;
        }

        public void Cancel()
        {
            Current = null;
            CurrentPhase = Phase.None;
            lungeRemaining = 0f;
        }

        /// <summary>Advances the attack. Returns this frame's lunge displacement.</summary>
        public Vector3 Tick(float dt)
        {
            if (!IsAttacking) return Vector3.zero;

            var attack = Current;
            float previous = Elapsed;
            Elapsed += dt;

            // Hit detection also runs if a long frame skipped over the whole active window.
            if (previous < attack.ActiveEnd && Elapsed >= attack.Startup)
            {
                DetectHits(attack);
                if (!IsAttacking) return Vector3.zero; // cancelled by a parry
            }

            Vector3 lunge = Vector3.zero;
            if (lungeRemaining > 0f && previous < attack.ActiveEnd)
            {
                float step = Mathf.Min(lungeRemaining, lungeSpeed * dt);
                lungeRemaining -= step;
                lunge = transform.forward * step;
            }

            if (Elapsed < attack.Startup) CurrentPhase = Phase.Startup;
            else if (Elapsed < attack.ActiveEnd) CurrentPhase = Phase.Active;
            else if (Elapsed < attack.TotalDuration) CurrentPhase = Phase.Recovery;
            else Cancel();

            return lunge;
        }

        public bool TryGetHitbox(out Vector3 center, out Quaternion rotation, out Vector3 size)
        {
            if (!IsAttacking)
            {
                center = default; rotation = default; size = default;
                return false;
            }
            rotation = transform.rotation;
            center = transform.position + rotation * Current.HitboxCenter;
            size = Current.HitboxSize;
            return true;
        }

        void DetectHits(AttackData attack)
        {
            if (attack.HitboxSize == Vector3.zero) return; // no melee hitbox (a bow shot: the arrow does the hitting)
            TryGetHitbox(out var center, out var rotation, out var size);
            int count = Physics.OverlapBoxNonAlloc(center, size * 0.5f, overlapBuffer, rotation, hitMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var target = overlapBuffer[i].GetComponentInParent<Combatant>();
                if (target == null || target == owner || target.Team == owner.Team || target.IsDead) continue;
                if (!alreadyHit.Add(target)) continue;

                Vector3 direction = target.transform.position - transform.position;
                direction.y = 0f;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

                var info = new DamageInfo
                {
                    Damage = attack.Damage * owner.DamageMultiplier,
                    PoiseDamage = attack.PoiseDamage * owner.PoiseDamageMultiplier,
                    Knockback = direction * attack.Knockback,
                    Attacker = owner,
                };

                var result = target.ReceiveHit(info);
                if (result != HitResult.Ignored) Landed?.Invoke(result, target);

                // A parry staggers us; stop swinging immediately.
                if (result == HitResult.Parried) { Cancel(); return; }
            }
        }
    }
}
