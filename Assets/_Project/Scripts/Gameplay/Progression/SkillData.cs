using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum SkillKind
    {
        /// <summary>Always-on stat bonus once unlocked.</summary>
        Passive,
        /// <summary>Equipped to a skill slot (E / Q) and used on a cooldown.</summary>
        Active,
    }

    /// <summary>
    /// One node in a Discipline's skill tree. Actives perform a special attack (and may grant a timed buff);
    /// passives add stat modifiers. Unlocking costs points from the skill's own Discipline.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Progression/Skill", fileName = "Skill")]
    public sealed class SkillData : GameData
    {
        public string DisplayName = "New Skill";
        [TextArea] public string Description;
        public SkillKind Kind;

        [Header("Tree")]
        public Discipline Discipline;
        [Tooltip("Only this class can learn it. Empty = any class.")]
        public ClassData Class;
        [Tooltip("Row in the tree (UI order).")]
        [Min(1)] public int Tier = 1;
        [Min(1)] public int RequiredLevel = 1;
        [Min(0)] public int Cost = 1;
        public SkillData[] Prerequisites;

        [Header("Passive")]
        public StatModifier[] PassiveModifiers;

        [Header("Active")]
        [Min(0f)] public float Cooldown = 8f;
        [Tooltip("Only usable with this combat style (weapon). Empty = any.")]
        public MovesetData RequiredMoveset;
        [Tooltip("The special attack this skill performs.")]
        public AttackData Attack;
        [Tooltip("Optional timed buff granted on use.")]
        public StatModifier[] Buff;
        [Min(0f)] public float BuffDuration;
    }
}
