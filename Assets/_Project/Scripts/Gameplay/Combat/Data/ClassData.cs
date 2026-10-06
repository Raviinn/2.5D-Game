using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>A playable class: base stats plus the combat styles it can switch between.</summary>
    [CreateAssetMenu(menuName = "Beast/Combat/Class", fileName = "Class")]
    public sealed class ClassData : GameData
    {
        public string DisplayName = "New Class";
        [TextArea] public string Description;

        [Header("Stats")]
        public float MaxHealth = 100f;
        public float MaxPoise = 40f;
        public float MaxStamina = 100f;
        public float StaminaRegen = 35f;
        public float StaminaRegenDelay = 0.7f;

        [Header("Combat")]
        [Tooltip("Styles the player can switch between. The first is the default.")]
        public MovesetData[] Movesets;
    }
}
