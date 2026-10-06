namespace Beast.Gameplay
{
    /// <summary>
    /// Animation states every character sprite sheet can provide. Append new ones at the end (sheets store them by index).
    /// Sheets without a clip for a state show their first clip (usually Idle).
    /// </summary>
    public enum CharacterAnim
    {
        Idle,
        Run,
        Attack,
        Block,
        Dodge,
        Hurt,
        Dead,
        /// <summary>Hanging from a ledge (shimmying plays it faster).</summary>
        Hang,
        /// <summary>Climbing a wall or pulling up over a ledge.</summary>
        Climb,
    }

    /// <summary>
    /// Implemented by anything that drives a character's sprite (player, enemies, later NPCs).
    /// The renderer reads it every frame; the source never talks to the renderer.
    /// </summary>
    public interface ICharacterAnimationSource
    {
        CharacterAnim CurrentAnim { get; }

        /// <summary>Optional. When attacking, attack clips are synced to its Startup/Active/Recovery timings.</summary>
        AttackExecutor Attacks { get; }

        /// <summary>Playback speed multiplier, e.g. faster run cycle while sprinting.</summary>
        float AnimationSpeed { get; }
    }
}
