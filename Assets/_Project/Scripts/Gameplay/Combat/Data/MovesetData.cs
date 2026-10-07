using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A combat style (e.g. Sword &amp; Shield, Greatsword): attacks plus dodge and block tuning.
    /// New styles are new assets, not new code.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Combat/Moveset", fileName = "Moveset")]
    public sealed class MovesetData : GameData
    {
        public string DisplayName = "New Style";
        [Tooltip("How the player is drawn while using this style.")]
        public WeaponLook Look;

        /// <summary>Look, except that an older asset left on the default whose name says Greatsword counts as one.</summary>
        public WeaponLook ResolvedLook =>
            Look == WeaponLook.SwordAndShield && DisplayName != null && DisplayName.Contains("Greatsword") ? WeaponLook.Greatsword : Look;

        [Header("Attacks")]
        public AttackData[] LightCombo;
        public AttackData Heavy;
        [Tooltip("Seconds after an attack ends during which the next light attack continues the combo.")]
        public float ComboResetTime = 0.5f;

        [Header("Dodge")]
        public float DodgeDistance = 4.5f;
        public float DodgeDuration = 0.32f;
        public float IFrameStart = 0.02f;
        public float IFrameEnd = 0.26f;
        public float DodgeStaminaCost = 20f;

        [Header("Block & Parry")]
        [Range(0f, 1f)] public float BlockDamageReduction = 0.85f;
        [Tooltip("Blocking within this many seconds of an incoming hit parries it.")]
        public float ParryWindow = 0.18f;
        [Range(0f, 1f)] public float BlockMoveSpeedMultiplier = 0.45f;
        [Tooltip("Stamina lost per point of blocked damage. Running out breaks your guard.")]
        public float BlockStaminaPerDamage = 1.2f;
        public float ParryStaggerDuration = 1.5f;
        public float GuardBreakStaggerDuration = 1f;
    }
}
