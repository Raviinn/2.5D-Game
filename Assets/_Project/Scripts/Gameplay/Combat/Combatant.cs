using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Health, poise, stagger, knockback and block/parry resolution for anything that fights.
    /// Controllers (player or AI) set Invulnerable / Telegraphing and call StartBlock / EndBlock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Combatant : MonoBehaviour
    {
        [SerializeField] Team team = Team.Enemy;

        [Header("Poise")]
        [SerializeField] float poiseRegenDelay = 2f;
        [SerializeField] float poiseRegenPerSecond = 25f;
        [SerializeField] float staggerDuration = 0.8f;

        [Header("Knockback")]
        [SerializeField, Tooltip("How fast knockback velocity decays (m/s per second).")]
        float knockbackDecay = 25f;

        [Header("Blocking")]
        [SerializeField, Tooltip("Hits from wider than this angle (degrees from facing) can't be blocked.")]
        float blockAngle = 80f;

        public Team Team => team;
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public float MaxPoise { get; private set; }
        public float Poise { get; private set; }
        public bool IsDead => Health <= 0f;
        public bool IsStaggered => staggerTimer > 0f;
        public bool IsBlocking => blockSettings != null;

        /// <summary>Set by controllers, e.g. during dodge i-frames.</summary>
        public bool Invulnerable { get; set; }
        /// <summary>Hits don't stagger or knock back (brutes mid-attack). Damage still lands.</summary>
        public bool SuperArmor { get; set; }
        /// <summary>Set by AI during attack wind-up so visuals can warn the player.</summary>
        public bool Telegraphing { get; set; }
        /// <summary>Optional; blocking costs stamina when present.</summary>
        public Stamina Stamina { get; set; }

        /// <summary>Scales damage this combatant deals (set by PlayerStats from Attack).</summary>
        public float DamageMultiplier { get; set; } = 1f;
        /// <summary>Scales poise damage this combatant deals.</summary>
        public float PoiseDamageMultiplier { get; set; } = 1f;
        /// <summary>Scales damage this combatant takes (set by PlayerStats from Defense).</summary>
        public float DamageTakenMultiplier { get; set; } = 1f;

        public event Action<HitResult> Hit;
        public event Action Died;
        public event Action Revived;

        MovesetData blockSettings;
        float blockStartTime;
        float staggerTimer;
        float lastPoiseDamageTime;
        Vector3 knockbackVelocity;

        public void Initialize(float maxHealth, float maxPoise)
        {
            MaxHealth = Health = maxHealth;
            MaxPoise = Poise = maxPoise;
        }

        /// <summary>Changes max health/poise (levels, gear) while keeping the current fill percentage.</summary>
        public void SetMaxStats(float maxHealth, float maxPoise)
        {
            if (!IsDead) Health = MaxHealth > 0f ? Mathf.Clamp(Health / MaxHealth * maxHealth, 1f, maxHealth) : maxHealth;
            Poise = MaxPoise > 0f ? Poise / MaxPoise * maxPoise : maxPoise;
            MaxHealth = maxHealth;
            MaxPoise = maxPoise;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (staggerTimer > 0f) staggerTimer -= dt;

            if (Poise < MaxPoise && Time.time - lastPoiseDamageTime > poiseRegenDelay)
                Poise = Mathf.Min(MaxPoise, Poise + poiseRegenPerSecond * dt);
        }

        public HitResult ReceiveHit(in DamageInfo info)
        {
            if (IsDead || Invulnerable) return HitResult.Ignored;

            float damage = info.Damage * DamageTakenMultiplier;

            if (IsBlocking && IsFacing(info))
            {
                var settings = blockSettings;
                if (Time.time - blockStartTime <= settings.ParryWindow)
                {
                    if (info.Attacker != null) info.Attacker.Stagger(settings.ParryStaggerDuration);
                    return Report(info, 0f, HitResult.Parried);
                }

                float chip = damage * (1f - settings.BlockDamageReduction);
                float staminaCost = damage * settings.BlockStaminaPerDamage;
                knockbackVelocity += info.Knockback * 0.5f;

                if (Stamina != null && !Stamina.TryConsume(staminaCost))
                {
                    EndBlock();
                    ApplyDamage(chip);
                    if (!IsDead) Stagger(settings.GuardBreakStaggerDuration);
                    return Report(info, chip, IsDead ? HitResult.Killed : HitResult.GuardBroken);
                }

                ApplyDamage(chip);
                return Report(info, chip, IsDead ? HitResult.Killed : HitResult.Blocked);
            }

            ApplyDamage(damage);
            if (SuperArmor) return Report(info, damage, IsDead ? HitResult.Killed : HitResult.Hit);
            knockbackVelocity += info.Knockback;

            Poise -= info.PoiseDamage;
            lastPoiseDamageTime = Time.time;
            if (Poise <= 0f && !IsDead)
            {
                Poise = MaxPoise;
                Stagger(staggerDuration);
            }

            return Report(info, damage, IsDead ? HitResult.Killed : HitResult.Hit);
        }

        /// <summary>
        /// Damage from the world (falls): can't be blocked or dodged, and Defense doesn't reduce it.
        /// Reported like a hit with no attacker, so damage numbers and sounds still show.
        /// </summary>
        public HitResult ReceiveWorldDamage(float amount)
        {
            if (IsDead || amount <= 0f) return HitResult.Ignored;
            ApplyDamage(amount);
            return Report(default, amount, IsDead ? HitResult.Killed : HitResult.Hit);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }

        public void Stagger(float duration)
        {
            if (IsDead) return;
            staggerTimer = Mathf.Max(staggerTimer, duration);
            EndBlock();
        }

        public void StartBlock(MovesetData settings)
        {
            blockSettings = settings;
            blockStartTime = Time.time;
        }

        public void EndBlock() => blockSettings = null;

        /// <summary>Returns this frame's knockback displacement and decays it. Call once per frame from the mover.</summary>
        public Vector3 ConsumeKnockback(float dt)
        {
            if (knockbackVelocity.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 displacement = knockbackVelocity * dt;
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDecay * dt);
            return displacement;
        }

        public void Revive()
        {
            Health = MaxHealth;
            Poise = MaxPoise;
            staggerTimer = 0f;
            knockbackVelocity = Vector3.zero;
            Invulnerable = false;
            Telegraphing = false;
            EndBlock();
            Revived?.Invoke();
        }

        void ApplyDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            if (!IsDead) return;

            staggerTimer = 0f;
            Telegraphing = false;
            EndBlock();
            Died?.Invoke();
            EventBus<CombatantDiedEvent>.Raise(new CombatantDiedEvent(this));
        }

        bool IsFacing(in DamageInfo info)
        {
            Vector3 toAttacker = info.Attacker != null
                ? info.Attacker.transform.position - transform.position
                : -info.Knockback;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(transform.forward, toAttacker) <= blockAngle;
        }

        HitResult Report(in DamageInfo info, float damage, HitResult result)
        {
            Hit?.Invoke(result);
            EventBus<DamageDealtEvent>.Raise(new DamageDealtEvent(this, info.Attacker, damage, result));
            return result;
        }
    }
}
