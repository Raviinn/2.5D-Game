using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A character's sprite sheet laid out as a grid: each ROW is a facing direction, each COLUMN a frame.
    /// Rows (top to bottom): front, front-right, right, back-right, back — then, if not mirrored,
    /// back-left, left, front-left. This matches a scripted Blender 8-direction render or an Aseprite export.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Visuals/Directional Sprite Sheet", fileName = "SpriteSheet")]
    public sealed class DirectionalSpriteSheet : GameData
    {
        [Serializable]
        public sealed class Clip
        {
            public CharacterAnim Anim;
            public int StartColumn;
            [Min(1)] public int FrameCount = 1;
            public float FramesPerSecond = 8f;
            public bool Loop = true;

            [Tooltip("Attack clips only: frames shown during Startup. They stretch to the attack's real timing.")]
            public int StartupFrames;
            [Tooltip("Attack clips only: frames shown during Active. The remaining frames play during Recovery.")]
            public int ActiveFrames;
        }

        public Texture2D Texture;
        public Vector2Int CellSize = new(48, 64);
        public float PixelsPerUnit = 32f;
        [Tooltip("Normalized pivot inside a cell. (0.5, 0) = bottom centre, where the feet touch the ground.")]
        public Vector2 Pivot = new(0.5f, 0f);
        [Tooltip("On: 5 rows are drawn and the left-facing views are mirrored. Off: all 8 rows are drawn.")]
        public bool MirrorLeft = true;
        public Clip[] Clips;

        public Vector2 WorldSize => new(CellSize.x / PixelsPerUnit, CellSize.y / PixelsPerUnit);

        /// <summary>Returns the clip for this animation, falling back to the first clip (usually Idle).</summary>
        public Clip Find(CharacterAnim anim)
        {
            if (Clips == null || Clips.Length == 0) return null;
            foreach (var clip in Clips)
                if (clip.Anim == anim) return clip;
            return Clips[0];
        }
    }
}
