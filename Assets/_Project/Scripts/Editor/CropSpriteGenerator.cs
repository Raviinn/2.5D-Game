using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Draws placeholder crop sheets in the layout CropData expects (one row):
    /// growth stages..., ripe, wilted, dead.
    /// </summary>
    public static class CropSpriteGenerator
    {
        public const int FrameSize = 32;

        public struct Palette
        {
            public Color32 Leaf, LeafDark, Produce, ProduceDark, Outline;
            /// <summary>True: produce hangs among the leaves (berries). False: a root at the base (turnip).</summary>
            public bool ProduceOnTop;
        }

        static readonly Color32 Dry = new(150, 120, 60, 255);
        static readonly Color32 DryDark = new(110, 85, 40, 255);
        static readonly Color32 Dead = new(90, 70, 50, 255);

        /// <summary>Writes the PNG (if missing) with pixel-art import settings and returns the texture.</summary>
        public static Texture2D CreateSheet(string assetPath, int growthStages, Palette palette)
        {
            if (!File.Exists(assetPath))
            {
                int frames = growthStages + 3;
                int width = frames * FrameSize;
                var pixels = new Color32[width * FrameSize];

                for (int stage = 0; stage < growthStages; stage++)
                    DrawPlant(pixels, width, stage, Grow(stage, growthStages), palette.Leaf, palette.LeafDark, droop: 0);

                DrawPlant(pixels, width, growthStages, 1f, palette.Leaf, palette.LeafDark, droop: 0);
                DrawProduce(pixels, width, growthStages, palette);
                DrawPlant(pixels, width, growthStages + 1, 0.8f, Dry, DryDark, droop: 3);
                DrawDead(pixels, width, growthStages + 2);

                AddOutlines(pixels, width, palette.Outline);

                var texture = new Texture2D(width, FrameSize, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(assetPath);
            }

            PlaceholderSpriteGenerator.ApplyPixelArtImport(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// <summary>0.25 for the first stage up to ~0.85 for the last (ripe is 1).</summary>
        static float Grow(int stage, int stages) => 0.25f + 0.6f * stage / Mathf.Max(1, stages - 1);

        static void DrawPlant(Color32[] px, int width, int frame, float size, Color32 leaf, Color32 leafDark, int droop)
        {
            const int cx = 16;
            int height = Mathf.Max(3, Mathf.RoundToInt(22f * size));
            Line(px, width, frame, cx, 2, cx, 2 + height, leafDark);

            int pairs = Mathf.Max(1, Mathf.RoundToInt(4f * size));
            for (int i = 0; i < pairs; i++)
            {
                int y = 4 + (height - 4) * (i + 1) / (pairs + 1) + (i == pairs - 1 ? 2 : 0);
                int leafLength = 2 + Mathf.RoundToInt(6f * size * (1f - 0.15f * i));
                // Left and right leaves angle up (or down when drooping).
                Line(px, width, frame, cx, y, cx - leafLength, y + 3 - droop * 2, leaf);
                Line(px, width, frame, cx, y, cx + leafLength, y + 3 - droop * 2, leaf);
                Set(px, width, frame, cx - leafLength, y + 3 - droop * 2, leafDark);
                Set(px, width, frame, cx + leafLength, y + 3 - droop * 2, leafDark);
            }
            Circle(px, width, frame, cx, 2 + height, Mathf.Max(1, Mathf.RoundToInt(2f * size)), leaf);
        }

        static void DrawProduce(Color32[] px, int width, int frame, Palette p)
        {
            if (p.ProduceOnTop)
            {
                Circle(px, width, frame, 11, 18, 2, p.Produce);
                Circle(px, width, frame, 20, 21, 2, p.Produce);
                Circle(px, width, frame, 15, 25, 2, p.Produce);
                Set(px, width, frame, 11, 19, p.ProduceDark);
                Set(px, width, frame, 20, 22, p.ProduceDark);
            }
            else
            {
                Circle(px, width, frame, 16, 5, 5, p.Produce);
                for (int x = 11; x <= 21; x++) Set(px, width, frame, x, 1, p.ProduceDark); // soil line / root shade
                Set(px, width, frame, 16, 0, p.ProduceDark);
            }
        }

        static void DrawDead(Color32[] px, int width, int frame)
        {
            Line(px, width, frame, 16, 1, 15, 8, Dead);
            Line(px, width, frame, 15, 5, 11, 3, Dead);
            Line(px, width, frame, 16, 4, 20, 2, Dead);
        }

        // ---------- Pixel helpers (each frame is clipped to its own cell) ----------

        static void Set(Color32[] px, int width, int frame, int x, int y, Color32 color)
        {
            if ((uint)x >= FrameSize || (uint)y >= FrameSize) return;
            px[y * width + frame * FrameSize + x] = color;
        }

        static void Circle(Color32[] px, int width, int frame, int cx, int cy, int r, Color32 color)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r + r) Set(px, width, frame, cx + x, cy + y, color);
        }

        static void Line(Color32[] px, int width, int frame, int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                Set(px, width, frame, x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * error;
                if (e2 >= dy) { error += dy; x0 += sx; }
                if (e2 <= dx) { error += dx; y0 += sy; }
            }
        }

        static void AddOutlines(Color32[] px, int width, Color32 outline)
        {
            var toOutline = new List<int>();
            for (int y = 0; y < FrameSize; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (px[index].a != 0) continue;
                    int cellX0 = x / FrameSize * FrameSize;
                    if (IsOpaque(x - 1, y) || IsOpaque(x + 1, y) || IsOpaque(x, y - 1) || IsOpaque(x, y + 1))
                        toOutline.Add(index);

                    bool IsOpaque(int nx, int ny) =>
                        nx >= cellX0 && nx < cellX0 + FrameSize && ny >= 0 && ny < FrameSize && px[ny * width + nx].a != 0;
                }
            }
            foreach (int index in toOutline) px[index] = outline;
        }
    }
}
