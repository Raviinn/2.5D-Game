using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    [CreateAssetMenu(menuName = "Beast/Combat/Enemy", fileName = "Enemy")]
    public sealed class EnemyData : GameData
    {
        public string DisplayName = "New Enemy";

        [Header("Stats")]
        public float MaxHealth = 60f;
        public float MaxPoise = 30f;

        [Header("Behaviour")]
        [Tooltip("Dummies never move or attack.")]
        public bool IsTrainingDummy;
        public float MoveSpeed = 3.4f;
        [Tooltip("Degrees per second.")]
        public float TurnSpeed = 540f;
        public float AggroRange = 12f;
        public float AttackRange = 2f;
        public float AttackCooldown = 1.1f;
        [Tooltip("Attacks are used in order, looping.")]
        public AttackData[] Attacks;

        [Header("Tactics")]
        [Tooltip("While another enemy has the attack turn, wait on a ring this far from the player.")]
        public float HoldDistance = 3.5f;
        [Tooltip("Give up and walk home after this many seconds without a way to reach the player (e.g. they climbed out of reach).")]
        public float GiveUpAfter = 5f;
        [Tooltip("Never chase further than this from home; then walk back and heal.")]
        public float LeashRange = 22f;

        [Header("Ranged (archers)")]
        [Tooltip("Shoots arrows instead of using melee attacks.")]
        public bool Ranged;
        [Tooltip("Shoots from up to this far (m), with a clear line of sight.")]
        public float ShootRange = 16f;
        [Tooltip("Tries to stay about this far away (m): backs off when you close in.")]
        public float PreferredDistance = 9f;
        [Tooltip("Seconds spent drawing the bow before loosing (the warning).")]
        public float DrawTime = 0.9f;
        [Tooltip("Seconds between shots.")]
        public float ShotCooldown = 2.6f;
        public float ArrowSpeed = 22f;
        public float ArrowDamage = 14f;
        public float ArrowPoiseDamage = 12f;

        [Header("Shield (shieldbearers)")]
        [Tooltip("Raises a shield while closing in: hits from the front are blocked until its guard runs out.")]
        public bool Shield;
        [Tooltip("Guard points; blocked damage × GuardCostPerDamage is taken off. At zero the guard breaks (staggered).")]
        public float GuardMax = 60f;
        public float GuardCostPerDamage = 1.4f;
        [Tooltip("Guard regained per second, after a short pause.")]
        public float GuardRegen = 16f;
        [Range(0f, 1f)] public float GuardDamageReduction = 0.9f;
        public float GuardBreakStagger = 1.8f;
        [Tooltip("Move speed while the shield is up (share of MoveSpeed).")]
        [Range(0.2f, 1f)] public float GuardMoveShare = 0.6f;

        [Header("Pack (wolves)")]
        [Tooltip("Waits for its turn behind or beside you instead of in front: packs flank.")]
        public bool PackHunter;
        [Tooltip("Seconds spent darting away after each attack (0 = none).")]
        public float RetreatAfterAttack;

        [Header("Brutes")]
        [Tooltip("Can't be staggered or knocked back while attacking.")]
        public bool SuperArmor;

        [Header("Night (bolder after dark)")]
        [Tooltip("Extra damage at night (0.3 = +30%).")]
        public float NightDamageBonus = 0.3f;
        [Tooltip("Extra aggro range at night (0.4 = +40%): they spot you from further away.")]
        public float NightAggroBonus = 0.4f;
        [Tooltip("Extra move speed at night.")]
        public float NightSpeedBonus = 0.15f;
        [Tooltip("Extra rolls of the loot table when killed at night.")]
        [Min(0)] public int NightExtraLootRolls = 1;
        [Tooltip("Extra gold when killed at night (0.5 = +50%).")]
        public float NightGoldBonus = 0.5f;

        [Header("Rewards")]
        [Tooltip("Combat XP for killing this enemy.")]
        [Min(0)] public int XpReward = 10;
        [Tooltip("Standing with the Free Hollows for defeating this enemy (protecting the region). Capped per day.")]
        [Min(0)] public int StandingReward;
        [Tooltip("Rolled on death by the LootDropper component.")]
        public LootTable Loot;

        [Header("HUD")]
        [Tooltip("Height of the name plate above the feet (m): lower for wolves, higher for brutes.")]
        public float PlateHeight = 2.2f;

        [Header("Testing")]
        [Tooltip("Seconds until respawn after death. 0 = stay dead.")]
        public float RespawnDelay = 6f;
    }
}
