using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>The body plan a placeholder sheet is drawn with.</summary>
    public enum Creature
    {
        Humanoid,
        Wolf,
        Hen,
        Cow,
    }

    public enum HairStyle
    {
        Short,
        Long,
        Ponytail,
        Bun,
        Shaved,
    }

    /// <summary>
    /// Draws placeholder pixel-art character sheets in the grid layout real art will use
    /// (rows = directions, columns = frames). Used by the editor setups for NPCs and enemies, and at runtime
    /// for the player, whose sheet is redrawn from their chosen look and current weapon.
    /// Direction cues: eyes and chest emblem shift with facing, and disappear from behind.
    /// </summary>
    public static partial class CharacterSpriteBuilder
    {
        public const int CellWidth = 48;
        public const int CellHeight = 64;
        public const int Rows = 5; // front, front-right, right, back-right, back (left side is mirrored at runtime)
        public const float PixelsPerUnit = 32f;
        /// <summary>Feet sit 2px above the bottom of each cell.</summary>
        public const float PivotY = 2f / CellHeight;
        public const int Columns = 27;

        public struct ClipLayout
        {
            public CharacterAnim Anim;
            public int Start;
            public int Count;
            public float Fps;
            public bool Loop;
            public int StartupFrames;
            public int ActiveFrames;
        }

        public static readonly ClipLayout[] Layout =
        {
            new() { Anim = CharacterAnim.Idle, Start = 0, Count = 4, Fps = 5f, Loop = true },
            new() { Anim = CharacterAnim.Run, Start = 4, Count = 6, Fps = 12f, Loop = true },
            new() { Anim = CharacterAnim.Attack, Start = 10, Count = 5, Fps = 10f, Loop = false, StartupFrames = 2, ActiveFrames = 1 },
            new() { Anim = CharacterAnim.Block, Start = 15, Count = 1, Fps = 1f, Loop = true },
            new() { Anim = CharacterAnim.Dodge, Start = 16, Count = 3, Fps = 10f, Loop = false },
            new() { Anim = CharacterAnim.Hurt, Start = 19, Count = 1, Fps = 1f, Loop = true },
            new() { Anim = CharacterAnim.Dead, Start = 20, Count = 1, Fps = 1f, Loop = true },
            new() { Anim = CharacterAnim.Hang, Start = 21, Count = 2, Fps = 3f, Loop = true },
            new() { Anim = CharacterAnim.Climb, Start = 23, Count = 4, Fps = 6f, Loop = true },
        };

        public struct Palette
        {
            public Color32 Body, Trim, Skin, Hair, Weapon, Outline, ShieldFace;
            public bool HasWeapon, HasShield;
            public HairStyle HairStyle;
            /// <summary>A long two-handed blade instead of a one-handed sword.</summary>
            public bool Greatsword;
            /// <summary>A bow instead of a blade (archers). Attack frames draw and loose an arrow.</summary>
            public bool Bow;
            /// <summary>What's drawn: a person (default) or a beast (CharacterSpriteBuilder.Beasts.cs).</summary>
            public Creature Creature;
        }

        static readonly Color32 TrailColor = new(255, 250, 220, 255);

        struct Pose
        {
            public int Bob;            // upper-body drop, px
            public int Lean;           // upper-body shift toward facing, px
            public int Crouch;         // legs shortened, px
            public float LegSwing;     // -1..1
            public float WeaponAngle;  // screen degrees: 0 = forward, 90 = up
            public bool Trail;
            public float TrailFrom;
            public bool Blocking;
            public bool Lying;
            public bool MotionLines;
            public bool ArmsUp;        // hanging / climbing: both hands above the head, gear stowed
            public int Reach;          // climbing: which hand is higher (+1 left, -1 right), px
            public bool Carry;         // idle / run: a greatsword rests on the shoulder instead of pointing down
            public bool Aim;           // attack: a bow is held out level
            public bool Drawn;         // attack wind-up: the bowstring is pulled back with an arrow nocked
        }

        /// <summary>A new point-filtered texture with the whole sheet. The caller owns (and destroys) it.</summary>
        public static Texture2D Build(Palette palette)
        {
            var texture = new Texture2D(Columns * CellWidth, Rows * CellHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Character (generated)",
            };
            texture.SetPixels32(Draw(palette));
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>The sheet's pixels, bottom row first (Texture2D order).</summary>
        public static Color32[] Draw(Palette palette)
        {
            int width = Columns * CellWidth;
            int height = Rows * CellHeight;
            var pixels = new Color32[width * height];
            var canvas = new Canvas(pixels, width, height);

            for (int row = 0; row < Rows; row++)
            {
                foreach (var clip in Layout)
                {
                    for (int frame = 0; frame < clip.Count; frame++)
                    {
                        canvas.SelectCell(clip.Start + frame, row);
                        DrawCell(canvas, row, GetPose(clip.Anim, frame, clip.Count), palette);
                    }
                }
            }
            AddOutlines(pixels, width, height, palette.Outline);
            return pixels;
        }

        static Pose GetPose(CharacterAnim anim, int frame, int count)
        {
            var pose = new Pose { WeaponAngle = -65f };
            switch (anim)
            {
                case CharacterAnim.Idle:
                    pose.Carry = true;
                    pose.Bob = frame is 1 or 2 ? 1 : 0;
                    break;
                case CharacterAnim.Run:
                    float phase = frame / (float)count * Mathf.PI * 2f;
                    pose.LegSwing = Mathf.Sin(phase);
                    pose.Bob = Mathf.Abs(pose.LegSwing) > 0.7f ? 1 : 0;
                    pose.Lean = 1;
                    pose.WeaponAngle = -40f;
                    pose.Carry = true;
                    break;
                case CharacterAnim.Attack:
                    pose.Aim = true;
                    pose.Drawn = frame < 2;
                    switch (frame)
                    {
                        case 0: pose.WeaponAngle = 110f; pose.Lean = -1; break;           // startup: raise
                        case 1: pose.WeaponAngle = 140f; pose.Lean = -2; break;           // startup: wound up
                        case 2: pose.WeaponAngle = -15f; pose.Lean = 3; pose.Trail = true; pose.TrailFrom = 140f; break; // active: strike
                        case 3: pose.WeaponAngle = -45f; pose.Lean = 2; break;            // recovery
                        default: pose.WeaponAngle = -60f; pose.Lean = 1; break;
                    }
                    break;
                case CharacterAnim.Block:
                    pose.Blocking = true;
                    pose.WeaponAngle = 80f;
                    pose.Lean = -1;
                    break;
                case CharacterAnim.Dodge:
                    pose.Crouch = frame == 1 ? 8 : 5;
                    pose.Lean = 4;
                    pose.MotionLines = true;
                    pose.WeaponAngle = -20f;
                    break;
                case CharacterAnim.Hurt:
                    pose.Lean = -3;
                    pose.WeaponAngle = -100f;
                    break;
                case CharacterAnim.Dead:
                    pose.Lying = true;
                    break;
                case CharacterAnim.Hang:
                    pose.ArmsUp = true;
                    pose.Lean = frame == 1 ? 1 : 0; // slow sway
                    break;
                case CharacterAnim.Climb:
                    pose.ArmsUp = true;
                    pose.Reach = frame % 2 == 0 ? 3 : -3;
                    pose.LegSwing = frame % 2 == 0 ? -0.8f : 0.8f;
                    pose.Bob = frame is 1 or 3 ? 1 : 0;
                    break;
            }
            return pose;
        }

        static void DrawCell(Canvas c, int row, Pose pose, Palette p)
        {
            switch (p.Creature)
            {
                case Creature.Wolf: DrawWolfCell(c, row, pose, p); return;
                case Creature.Hen: DrawHenCell(c, row, pose, p); return;
                case Creature.Cow: DrawCowCell(c, row, pose, p); return;
            }
            if (pose.Lying)
            {
                FillRect(c, 2, 3, 8, 5, p.Trim);
                FillRect(c, 9, 2, 26, 8, p.Body);
                if (p.HairStyle is HairStyle.Long or HairStyle.Ponytail) FillRect(c, 40, 2, 7, 8, p.Hair);
                FillCircle(c, 39, 6, 5, p.Skin);
                if (p.HairStyle != HairStyle.Shaved) FillRect(c, 37, 9, 6, 2, p.Hair);
                return;
            }

            float side = row switch { 1 => 0.6f, 2 => 1f, 3 => 0.6f, _ => 0f };
            bool back = row >= 3;
            // Screen-x direction that counts as "forward" for this view (sheets face screen-right).
            int forward = row == 0 ? -1 : 1;

            const int cx = 24;
            int legTop = 16 - pose.Crouch;
            int upper = legTop - pose.Bob;
            int bx = cx + Mathf.RoundToInt(pose.Lean * Mathf.Max(side, 0.3f));

            int handX = row switch { 0 => -9, 1 => 3, 2 => 6, 3 => 7, _ => 9 };  // weapon hand (character's right)
            int offX = row switch { 0 => 9, 1 => -6, 2 => -3, 3 => -7, _ => -9 };  // shield hand
            var hand = new Vector2Int(bx + handX, upper + 9);
            var offHand = new Vector2Int(bx + offX, upper + 10);

            bool weaponBehind = back;
            bool shieldBehind = row >= 2;
            if (!pose.ArmsUp)
            {
                if (weaponBehind) DrawWeapon(c, hand, forward, pose, p, bx + Mathf.RoundToInt(side * 2f));
                if (shieldBehind) DrawShield(c, offHand, forward, pose, p);
            }

            int hx = bx + Mathf.RoundToInt(side * 2f);
            int hy = upper + 23;
            if (!back) DrawHairBehind(c, row, hx, hy, upper, p); // long hair and tails show past the shoulders

            // Legs
            int stride = Mathf.RoundToInt(pose.LegSwing * 3f * Mathf.Max(side, 0.3f));
            int lift = Mathf.RoundToInt(Mathf.Abs(pose.LegSwing) * 2f);
            FillRect(c, cx - 5 + stride, 2 + (pose.LegSwing > 0f ? lift : 0), 4, legTop - 2, p.Trim);
            FillRect(c, cx + 1 - stride, 2 + (pose.LegSwing < 0f ? lift : 0), 4, legTop - 2, p.Trim);

            // Torso, belt, arms
            FillRect(c, bx - 7, upper, 14, 16, p.Body);
            FillRect(c, bx - 7, upper + 1, 14, 2, p.Trim);
            if (!back) FillRect(c, bx - 2 + Mathf.RoundToInt(side * 3f), upper + 7, 4, 4, p.Trim); // chest emblem: front only
            if (pose.ArmsUp)
            {
                // Arms reaching above the head, hands gripping.
                int left = 18 + Mathf.Max(0, pose.Reach), right = 18 + Mathf.Max(0, -pose.Reach);
                FillRect(c, bx - 9, upper + 12, 3, left, p.Body);
                FillRect(c, bx + 6, upper + 12, 3, right, p.Body);
                FillRect(c, bx - 10, upper + 11 + left, 4, 3, p.Skin);
                FillRect(c, bx + 6, upper + 11 + right, 4, 3, p.Skin);
            }
            else
            {
                FillRect(c, bx - 9, upper + 4, 3, 11, p.Body);
                FillRect(c, bx + 6, upper + 4, 3, 11, p.Body);
            }

            // Head: hair everywhere (skin when shaved), face only when visible
            var scalp = p.HairStyle == HairStyle.Shaved ? p.Skin : p.Hair;
            FillCircle(c, hx, hy, 7, scalp);
            if (p.HairStyle == HairStyle.Bun) FillCircle(c, hx - Mathf.RoundToInt(side * 3f), hy + 7, 3, p.Hair);
            if (!back)
            {
                int faceX = hx + Mathf.RoundToInt(side * 2f);
                FillCircle(c, faceX, hy - 2, 6, p.Skin);
                switch (row)
                {
                    case 0: Dot(c, hx - 3, hy - 2, p.Outline); Dot(c, hx + 2, hy - 2, p.Outline); break;
                    case 1: Dot(c, faceX - 1, hy - 2, p.Outline); Dot(c, faceX + 3, hy - 2, p.Outline); break;
                    default: Dot(c, faceX + 3, hy - 2, p.Outline); c.Set(faceX + 7, hy - 4, p.Skin); break; // profile: one eye + nose
                }
                if (p.HairStyle == HairStyle.Long && row <= 1)
                {
                    // Locks framing the face.
                    FillRect(c, hx - 8, upper + 13, 3, hy - upper - 11, p.Hair);
                    FillRect(c, hx + 5 + (row == 1 ? 1 : 0), upper + 13, 3, hy - upper - 11, p.Hair);
                }
            }
            else
            {
                if (row == 3) FillCircle(c, hx + 5, hy - 3, 2, p.Skin); // sliver of cheek from behind
                DrawHairFromBehind(c, hx, hy, upper, p);
            }

            if (!pose.ArmsUp)
            {
                if (!weaponBehind) DrawWeapon(c, hand, forward, pose, p, bx + Mathf.RoundToInt(side * 2f));
                if (!shieldBehind) DrawShield(c, offHand, forward, pose, p);
            }

            if (pose.MotionLines)
                for (int i = 0; i < 3; i++)
                    FillRect(c, bx - 20, upper + 2 + i * 6, 8 - i * 2, 1, TrailColor);
        }

        /// <summary>Front and side views: hair that hangs behind the head (drawn before the body covers it).</summary>
        static void DrawHairBehind(Canvas c, int row, int hx, int hy, int upper, Palette p)
        {
            switch (p.HairStyle)
            {
                case HairStyle.Long:
                    if (row == 2) FillRect(c, hx - 9, upper + 11, 7, hy - upper - 9, p.Hair);
                    break;
                case HairStyle.Ponytail:
                    if (row == 1) FillRect(c, hx - 10, hy - 9, 4, 9, p.Hair);
                    if (row == 2)
                    {
                        FillRect(c, hx - 10, hy - 1, 4, 3, p.Hair);
                        FillRect(c, hx - 12, hy - 11, 4, 11, p.Hair);
                    }
                    break;
            }
        }

        /// <summary>Back views: long hair and tails hang over the back.</summary>
        static void DrawHairFromBehind(Canvas c, int hx, int hy, int upper, Palette p)
        {
            switch (p.HairStyle)
            {
                case HairStyle.Long:
                    FillRect(c, hx - 7, upper + 9, 14, hy - upper - 9, p.Hair);
                    break;
                case HairStyle.Ponytail:
                    FillRect(c, hx - 2, upper + 6, 4, hy - upper - 6, p.Hair);
                    FillRect(c, hx - 3, hy - 4, 6, 3, p.Hair);
                    break;
            }
        }

        static void DrawWeapon(Canvas c, Vector2Int hand, int forward, Pose pose, Palette p, int headX)
        {
            FillRect(c, hand.x - 1, hand.y - 1, 3, 3, p.Skin);
            if (!p.HasWeapon) return;
            if (p.Bow)
            {
                DrawBow(c, hand, forward, pose, p);
                return;
            }

            float length = p.Greatsword ? 21f : 16f;
            if (pose.Trail)
            {
                for (float a = pose.TrailFrom; a > pose.WeaponAngle; a -= 8f)
                {
                    var point = hand + Direction(a, forward) * (length + 1f);
                    int size = p.Greatsword ? 3 : 2;
                    FillRect(c, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), size, size, TrailColor);
                }
            }

            var dir = Direction(pose.WeaponAngle, forward);
            // At rest a greatsword is held upright, leaning away from the head so it never covers the face.
            if (p.Greatsword && pose.Carry) dir = new Vector2(hand.x >= headX ? 0.55f : -0.55f, 1f).normalized;
            var tip = hand + dir * length;
            var tipPixel = new Vector2Int(Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y));
            Line(c, hand, tipPixel, p.Weapon);
            if (p.Greatsword)
            {
                // Broad blade, wide crossguard, and the off hand on the grip below the weapon hand.
                var side = new Vector2(-dir.y, dir.x);
                var offset = new Vector2Int(Mathf.RoundToInt(side.x), Mathf.RoundToInt(side.y));
                Line(c, hand + dir.ToInt(3f) + offset, tipPixel + offset, p.Weapon);
                var guard = new Vector2(hand.x, hand.y) + dir * 3f;
                Line(c, (guard + side * 4f).ToInt(), (guard - side * 4f).ToInt(), p.Trim);
                FillRect(c, hand.x - 1, hand.y - 1, 3, 3, p.Trim);
                var grip = (new Vector2(hand.x, hand.y) - dir * 3f).ToInt();
                FillRect(c, grip.x - 1, grip.y - 1, 3, 3, p.Skin);
                return;
            }
            FillRect(c, hand.x - 1, hand.y - 1, 3, 3, p.Trim); // hilt
        }

        /// <summary>A curved bow centred on the hand: upright at rest, held out level (and drawn) when attacking.</summary>
        static void DrawBow(Canvas c, Vector2Int hand, int forward, Pose pose, Palette p)
        {
            var aim = Direction(0f, forward); // the bow stands upright either way; at rest it hangs at your side
            var across = new Vector2(-aim.y, aim.x);
            var grip = pose.Aim ? new Vector2(hand.x, hand.y) + aim * 5f : new Vector2(hand.x + aim.x, hand.y - 6f); // at rest: low, clear of the face
            float half = pose.Aim ? 10f : 9f, bulge = 3f;
            Vector2Int previous = default;
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f * 2f - 1f;                       // -1..1 along the bow
                var point = grip + across * (t * half) + aim * (bulge * (1f - t * t));
                var pixel = point.ToInt();
                if (i > 0) Line(c, previous, pixel, p.Trim);
                previous = pixel;
            }
            var top = (grip + across * half).ToInt();
            var bottom = (grip - across * half).ToInt();
            if (pose.Drawn)
            {
                var pull = (grip - aim * 6f).ToInt();
                Line(c, top, pull, p.Weapon);
                Line(c, pull, bottom, p.Weapon);
                Line(c, pull, (grip + aim * 6f).ToInt(), p.Weapon); // the nocked arrow
                FillRect(c, pull.x - 1, pull.y - 1, 3, 3, p.Skin);  // drawing hand
            }
            else
            {
                Line(c, top, bottom, p.Weapon);
            }
            FillRect(c, Mathf.RoundToInt(grip.x) - 1, Mathf.RoundToInt(grip.y) - 1, 3, 3, p.Skin);
        }

        static void DrawShield(Canvas c, Vector2Int offHand, int forward, Pose pose, Palette p)
        {
            if (!p.HasShield) return;
            var center = offHand;
            if (pose.Blocking) center += new Vector2Int(forward * 6, 3);
            FillRect(c, center.x - 4, center.y - 6, 9, 12, p.Trim);
            FillRect(c, center.x - 3, center.y - 5, 7, 10, p.ShieldFace);
            FillRect(c, center.x - 1, center.y - 1, 3, 3, p.Trim); // boss
        }

        static Vector2 Direction(float degrees, int forward)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians) * forward, Mathf.Sin(radians));
        }

        static Vector2Int ToInt(this Vector2 v, float scale = 1f) => new(Mathf.RoundToInt(v.x * scale), Mathf.RoundToInt(v.y * scale));

        // ---------- Pixel helpers ----------

        sealed class Canvas
        {
            readonly Color32[] pixels;
            readonly int width;
            readonly int height;
            int originX;
            int originY;

            public Canvas(Color32[] pixels, int width, int height)
            {
                this.pixels = pixels;
                this.width = width;
                this.height = height;
            }

            /// <summary>Row 0 is the top of the image (textures are stored bottom-up).</summary>
            public void SelectCell(int column, int row)
            {
                originX = column * CellWidth;
                originY = height - (row + 1) * CellHeight;
            }

            public void Set(int x, int y, Color32 color)
            {
                if ((uint)x >= CellWidth || (uint)y >= CellHeight) return; // never bleed into neighbouring cells
                pixels[(originY + y) * width + originX + x] = color;
            }
        }

        static void FillRect(Canvas c, int x, int y, int w, int h, Color32 color)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    c.Set(x + i, y + j, color);
        }

        static void FillCircle(Canvas c, int cx, int cy, int r, Color32 color)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r + r) c.Set(cx + x, cy + y, color);
        }

        static void Dot(Canvas c, int x, int y, Color32 color) => FillRect(c, x, y, 2, 2, color);

        static void Line(Canvas c, Vector2Int from, Vector2Int to, Color32 color)
        {
            int dx = Mathf.Abs(to.x - from.x), sx = from.x < to.x ? 1 : -1;
            int dy = -Mathf.Abs(to.y - from.y), sy = from.y < to.y ? 1 : -1;
            int error = dx + dy;
            int x = from.x, y = from.y;
            while (true)
            {
                FillRect(c, x, y, 2, 2, color);
                if (x == to.x && y == to.y) break;
                int e2 = 2 * error;
                if (e2 >= dy) { error += dy; x += sx; }
                if (e2 <= dx) { error += dx; y += sy; }
            }
        }

        /// <summary>1px dark outline around every shape, kept inside each cell.</summary>
        static void AddOutlines(Color32[] pixels, int width, int height, Color32 outline)
        {
            var toOutline = new List<int>();
            for (int y = 0; y < height; y++)
            {
                int cellY0 = y / CellHeight * CellHeight;
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (pixels[index].a != 0) continue;

                    int cellX0 = x / CellWidth * CellWidth;
                    if (IsOpaque(x - 1, y) || IsOpaque(x + 1, y) || IsOpaque(x, y - 1) || IsOpaque(x, y + 1))
                        toOutline.Add(index);

                    bool IsOpaque(int nx, int ny) =>
                        nx >= cellX0 && nx < cellX0 + CellWidth && ny >= cellY0 && ny < cellY0 + CellHeight &&
                        pixels[ny * width + nx].a != 0;
                }
            }
            foreach (int index in toOutline) pixels[index] = outline;
        }
    }
}
