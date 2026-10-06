using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Draws simple pixel-art placeholder characters in the same grid layout real art will use
    /// (rows = directions, columns = frames), so the sprite pipeline can be tested before any art exists.
    /// Direction cues: eyes and chest emblem shift with facing, and disappear from behind.
    /// </summary>
    public static class PlaceholderSpriteGenerator
    {
        public const int CellWidth = 48;
        public const int CellHeight = 64;
        public const int Rows = 5; // front, front-right, right, back-right, back (left side is mirrored at runtime)
        public const float PixelsPerUnit = 32f;
        /// <summary>Feet sit 2px above the bottom of each cell.</summary>
        public const float PivotY = 2f / CellHeight;

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

        public const int Columns = 27;
        /// <summary>Column count of sheets generated before the climbing clips existed (Milestone 3).</summary>
        public const int LegacyColumns = 21;

        public struct Palette
        {
            public Color32 Body, Trim, Skin, Hair, Weapon, Outline;
            public bool HasWeapon, HasShield;
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
        }

        /// <summary>Writes the PNG (if missing) with pixel-art import settings and returns the texture.</summary>
        public static Texture2D CreateSheetTexture(string assetPath, Palette palette) => CreateSheetTexture(assetPath, palette, false);

        /// <summary>As above; 'regenerate' redraws an existing placeholder PNG (to add new clips).</summary>
        public static Texture2D CreateSheetTexture(string assetPath, Palette palette, bool regenerate)
        {
            if (regenerate || !File.Exists(assetPath))
            {
                var texture = Generate(palette);
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(assetPath);
            }

            ApplyPixelArtImport(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// <summary>Crisp pixel art: point filtering, no mipmaps, no compression, any size.</summary>
        public static void ApplyPixelArtImport(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        static Texture2D Generate(Palette palette)
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

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static Pose GetPose(CharacterAnim anim, int frame, int count)
        {
            var pose = new Pose { WeaponAngle = -65f };
            switch (anim)
            {
                case CharacterAnim.Idle:
                    pose.Bob = frame is 1 or 2 ? 1 : 0;
                    break;
                case CharacterAnim.Run:
                    float phase = frame / (float)count * Mathf.PI * 2f;
                    pose.LegSwing = Mathf.Sin(phase);
                    pose.Bob = Mathf.Abs(pose.LegSwing) > 0.7f ? 1 : 0;
                    pose.Lean = 1;
                    pose.WeaponAngle = -40f;
                    break;
                case CharacterAnim.Attack:
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
            if (pose.Lying)
            {
                FillRect(c, 2, 3, 8, 5, p.Trim);
                FillRect(c, 9, 2, 26, 8, p.Body);
                FillCircle(c, 39, 6, 5, p.Skin);
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
                if (weaponBehind) DrawWeapon(c, hand, forward, pose, p);
                if (shieldBehind) DrawShield(c, offHand, forward, pose, p);
            }

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

            // Head: hair/helmet everywhere, face only when visible
            int hx = bx + Mathf.RoundToInt(side * 2f);
            int hy = upper + 23;
            FillCircle(c, hx, hy, 7, p.Hair);
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
            }
            else if (row == 3)
            {
                FillCircle(c, hx + 5, hy - 3, 2, p.Skin); // sliver of cheek from behind
            }

            if (!pose.ArmsUp)
            {
                if (!weaponBehind) DrawWeapon(c, hand, forward, pose, p);
                if (!shieldBehind) DrawShield(c, offHand, forward, pose, p);
            }

            if (pose.MotionLines)
                for (int i = 0; i < 3; i++)
                    FillRect(c, bx - 20, upper + 2 + i * 6, 8 - i * 2, 1, TrailColor);
        }

        static void DrawWeapon(Canvas c, Vector2Int hand, int forward, Pose pose, Palette p)
        {
            FillRect(c, hand.x - 1, hand.y - 1, 3, 3, p.Skin);
            if (!p.HasWeapon) return;

            if (pose.Trail)
            {
                for (float a = pose.TrailFrom; a > pose.WeaponAngle; a -= 8f)
                {
                    var point = hand + Direction(a, forward) * 17f;
                    FillRect(c, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), 2, 2, TrailColor);
                }
            }

            var tip = hand + Direction(pose.WeaponAngle, forward) * 16f;
            Line(c, hand, new Vector2Int(Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y)), p.Weapon);
            FillRect(c, hand.x - 1, hand.y - 1, 3, 3, p.Trim); // hilt
        }

        static void DrawShield(Canvas c, Vector2Int offHand, int forward, Pose pose, Palette p)
        {
            if (!p.HasShield) return;
            var center = offHand;
            if (pose.Blocking) center += new Vector2Int(forward * 6, 3);
            FillRect(c, center.x - 4, center.y - 6, 9, 12, p.Trim);
            FillRect(c, center.x - 3, center.y - 5, 7, 10, p.Hair);
            FillRect(c, center.x - 1, center.y - 1, 3, 3, p.Trim); // boss
        }

        static Vector2 Direction(float degrees, int forward)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians) * forward, Mathf.Sin(radians));
        }

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
