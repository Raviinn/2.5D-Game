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

        [Header("Testing")]
        [Tooltip("Seconds until respawn after death. 0 = stay dead.")]
        public float RespawnDelay = 6f;
    }
}
