using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Procedural placeholder art for the environment, in the same chunky pixel-art style as the character sprites:
    /// seamless ground/wall/roof textures, billboard sprites (trees, bushes, grass, flowers) and simple meshes
    /// (roofs, rocks, ribbons for paths). Textures are written once as PNGs; edit or replace them freely and
    /// the setup will keep your versions. Everything is deterministic (fixed seeds).
    /// </summary>
    public static class EnvironmentArtGenerator
    {
        public const string ArtRoot = "Assets/_Project/Art/Environment";
        public const string TextureFolder = ArtRoot + "/Textures";
        public const string MeshFolder = ArtRoot + "/Meshes";

        // ---------- Texture catalogue ----------

        public sealed class TextureSpec
        {
            public string Name;
            public int Width, Height;
            public bool Tileable;
            public Action<Canvas> Paint;
        }

        public static readonly TextureSpec[] Textures =
        {
            new() { Name = "Ground_Grass", Width = 64, Height = 64, Tileable = true, Paint = PaintGrass },
            new() { Name = "Ground_Dirt", Width = 64, Height = 64, Tileable = true, Paint = PaintDirt },
            new() { Name = "Ground_Tilled", Width = 64, Height = 64, Tileable = true, Paint = PaintTilled },
            new() { Name = "Ground_Cobble", Width = 64, Height = 64, Tileable = true, Paint = PaintCobble },
            new() { Name = "Wall_Stone", Width = 64, Height = 64, Tileable = true, Paint = PaintStoneWall },
            new() { Name = "Wall_Timber", Width = 64, Height = 64, Tileable = true, Paint = PaintTimberPlaster },
            new() { Name = "Wall_Ivy", Width = 64, Height = 64, Tileable = true, Paint = PaintIvyWall },
            new() { Name = "Wood_Planks", Width = 64, Height = 64, Tileable = true, Paint = PaintPlanks },
            new() { Name = "Roof_Tiles", Width = 64, Height = 64, Tileable = true, Paint = PaintRoofTiles },
            new() { Name = "Rock", Width = 32, Height = 32, Tileable = true, Paint = PaintRock },
            new() { Name = "Door", Width = 32, Height = 48, Tileable = false, Paint = PaintDoor },
            new() { Name = "Window", Width = 32, Height = 32, Tileable = false, Paint = PaintWindow },
            new() { Name = "Notice", Width = 16, Height = 20, Tileable = false, Paint = PaintNotice },
            new() { Name = "Sprite_Oak", Width = 64, Height = 80, Tileable = false, Paint = PaintOak },
            new() { Name = "Sprite_Pine", Width = 48, Height = 96, Tileable = false, Paint = PaintPine },
            new() { Name = "Sprite_DeadTree", Width = 56, Height = 80, Tileable = false, Paint = PaintDeadTree },
            new() { Name = "Sprite_Bush", Width = 40, Height = 28, Tileable = false, Paint = PaintBush },
            new() { Name = "Sprite_GrassTuft", Width = 16, Height = 12, Tileable = false, Paint = PaintGrassTuft },
            new() { Name = "Sprite_Flowers", Width = 16, Height = 12, Tileable = false, Paint = PaintFlowers },
            new() { Name = "Map_Canopy", Width = 32, Height = 32, Tileable = false, Paint = PaintCanopyDisc },
        };

        /// <summary>Writes any missing texture PNG (existing ones are kept) and returns name → texture.</summary>
        public static Dictionary<string, Texture2D> EnsureTextures()
        {
            EnsureFolder(TextureFolder);
            var result = new Dictionary<string, Texture2D>();
            foreach (var spec in Textures)
            {
                string path = $"{TextureFolder}/{spec.Name}.png";
                if (!File.Exists(path))
                {
                    var canvas = new Canvas(spec.Width, spec.Height, StableHash(spec.Name));
                    spec.Paint(canvas);
                    File.WriteAllBytes(path, canvas.ToTexture().EncodeToPNG());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    ConfigureImport(path, spec.Tileable);
                }
                result[spec.Name] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return result;
        }

        /// <summary>FNV-1a: the same seed on every machine and run (string.GetHashCode isn't guaranteed to be).</summary>
        static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char ch in text) hash = (hash ^ ch) * 16777619;
                return (int)hash;
            }
        }

        static void ConfigureImport(string path, bool tileable)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = !tileable;
            importer.sRGBTexture = true;
            // Tiling ground/walls get mipmaps so distant surfaces don't shimmer; sprites stay crisp.
            importer.mipmapEnabled = tileable;
            importer.wrapMode = tileable ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // ---------- Canvas ----------

        public sealed class Canvas
        {
            public readonly int Width, Height;
            readonly Color32[] pixels;
            readonly System.Random random;

            public Canvas(int width, int height, int seed)
            {
                Width = width;
                Height = height;
                pixels = new Color32[width * height];
                random = new System.Random(seed);
            }

            public float Random01() => (float)random.NextDouble();
            public int Range(int min, int maxExclusive) => random.Next(min, maxExclusive);

            public void Set(int x, int y, Color32 c, bool wrap = false)
            {
                if (wrap) { x = Mod(x, Width); y = Mod(y, Height); }
                else if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                pixels[y * Width + x] = c;
            }

            public Color32 Get(int x, int y, bool wrap = false)
            {
                if (wrap) { x = Mod(x, Width); y = Mod(y, Height); }
                else if (x < 0 || y < 0 || x >= Width || y >= Height) return default;
                return pixels[y * Width + x];
            }

            public bool IsOpaque(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && pixels[y * Width + x].a > 0;

            public void FillRect(int x0, int y0, int w, int h, Color32 c)
            {
                for (int y = y0; y < y0 + h; y++)
                    for (int x = x0; x < x0 + w; x++) Set(x, y, c);
            }

            /// <summary>Thick line (for branches and blades).</summary>
            public void Line(float x0, float y0, float x1, float y1, float thickness, Color32 c)
            {
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2f) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float x = Mathf.Lerp(x0, x1, t), y = Mathf.Lerp(y0, y1, t);
                    float r = thickness * 0.5f;
                    for (int py = Mathf.FloorToInt(y - r); py <= Mathf.CeilToInt(y + r); py++)
                        for (int px = Mathf.FloorToInt(x - r); px <= Mathf.CeilToInt(x + r); px++)
                            if ((px + 0.5f - x) * (px + 0.5f - x) + (py + 0.5f - y) * (py + 0.5f - y) <= r * r + 0.25f) Set(px, py, c);
                }
            }

            /// <summary>Darkens every opaque pixel that touches transparency: the crisp outline pixel art relies on.</summary>
            public void Outline(Color32 color)
            {
                var edge = new List<int>();
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        if (IsOpaque(x, y) && (!IsOpaque(x - 1, y) || !IsOpaque(x + 1, y) || !IsOpaque(x, y - 1) || !IsOpaque(x, y + 1)))
                            edge.Add(y * Width + x);
                foreach (int i in edge) pixels[i] = color;
            }

            public Texture2D ToTexture()
            {
                var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                tex.SetPixels32(pixels);
                tex.Apply();
                return tex;
            }
        }

        // ---------- Noise & palettes ----------

        static readonly float[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        static int Mod(int a, int m) => (a % m + m) % m;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Value noise that wraps every 'period' cells, so tiles are seamless.</summary>
        static float TileNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Mod(x0, period), Mod(y0, period), seed);
            float b = Hash(Mod(x0 + 1, period), Mod(y0, period), seed);
            float c = Hash(Mod(x0, period), Mod(y0 + 1, period), seed);
            float d = Hash(Mod(x0 + 1, period), Mod(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Seamless fractal noise over a size×size tile (0..1).</summary>
        static float Fbm(int x, int y, int size, int seed, int baseCell = 16)
        {
            float sum = 0f, weight = 0f, amplitude = 1f;
            for (int cell = baseCell; cell >= 2; cell /= 2)
            {
                int period = Mathf.Max(1, size / cell);
                sum += TileNoise(x / (float)cell, y / (float)cell, period, seed + cell) * amplitude;
                weight += amplitude;
                amplitude *= 0.55f;
            }
            return sum / weight;
        }

        /// <summary>Picks a palette entry for t (0..1) with ordered dithering, the classic pixel-art gradient.</summary>
        static Color32 Shade(Color32[] palette, float t, int x, int y)
        {
            float dither = (Bayer4[(Mod(y, 4)) * 4 + Mod(x, 4)] / 16f - 0.5f) / palette.Length;
            int index = Mathf.Clamp(Mathf.RoundToInt((Mathf.Clamp01(t) + dither) * (palette.Length - 1)), 0, palette.Length - 1);
            return palette[index];
        }

        static Color32 C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static Color32[] P(params string[] hex)
        {
            var result = new Color32[hex.Length];
            for (int i = 0; i < hex.Length; i++) result[i] = C(hex[i]);
            return result;
        }

        // Muted, slightly desaturated palettes: grounded tone in a high-magic world.
        static readonly Color32[] GrassPalette = P("#2f4423", "#3c5629", "#4a6830", "#587a37", "#6a8c42");
        static readonly Color32[] DirtPalette = P("#4a3524", "#5a422c", "#6b5136", "#7d6141", "#8e724e");
        static readonly Color32[] StonePalette = P("#3d3d42", "#4f4f55", "#626268", "#76767b", "#8b8b8e");
        static readonly Color32[] WoodPalette = P("#3a2618", "#4c321f", "#5e4027", "#704e30", "#835d3a");
        static readonly Color32[] PlasterPalette = P("#a39478", "#b3a487", "#c2b396", "#cfc1a4");
        static readonly Color32[] RoofPalette = P("#3d1f1a", "#522822", "#68332a", "#7d3f32", "#8f4c3b");
        static readonly Color32[] LeafPalette = P("#1f3319", "#2c4721", "#3b5d2a", "#4f7533", "#6a8e40");
        static readonly Color32[] PinePalette = P("#16281e", "#1f3828", "#2a4a33", "#375e3e", "#46714a");
        static readonly Color32[] BarkPalette = P("#2b1d14", "#3d2a1c", "#523825", "#654530");
        static readonly Color32[] BlightPalette = P("#231e26", "#332a35", "#463947", "#5a4b58", "#6d5d68");

        // ---------- Ground ----------

        static void PaintGrass(Canvas c)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    c.Set(x, y, Shade(GrassPalette, 0.18f + Fbm(x, y, c.Width, 11) * 0.62f, x, y));

            // Short blades: vertical strokes, lighter tips, wrapping so the tile stays seamless.
            for (int i = 0; i < 170; i++)
            {
                int x = c.Range(0, c.Width), y = c.Range(0, c.Height), length = c.Range(2, 5);
                bool light = c.Random01() < 0.6f;
                for (int k = 0; k < length; k++)
                    c.Set(x, y + k, light ? GrassPalette[k == length - 1 ? 4 : 3] : GrassPalette[0], wrap: true);
            }
            // A few pale specks (seed heads).
            for (int i = 0; i < 10; i++) c.Set(c.Range(0, c.Width), c.Range(0, c.Height), C("#9aa65a"), wrap: true);
        }

        static void PaintDirt(Canvas c)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    c.Set(x, y, Shade(DirtPalette, 0.15f + Fbm(x, y, c.Width, 23, 8) * 0.7f, x, y));
            for (int i = 0; i < 38; i++)
            {
                int x = c.Range(0, c.Width), y = c.Range(0, c.Height);
                c.Set(x, y, DirtPalette[4], wrap: true);
                c.Set(x + 1, y, DirtPalette[3], wrap: true);
                c.Set(x, y - 1, DirtPalette[0], wrap: true); // pebble shadow
            }
        }

        static void PaintTilled(Canvas c)
        {
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    // Furrows every 8 px: lit ridge tops, dark troughs.
                    float ridge = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * (y + 2) / 8f);
                    float t = 0.1f + ridge * 0.55f + (Fbm(x, y, c.Width, 31, 8) - 0.5f) * 0.4f;
                    c.Set(x, y, Shade(DirtPalette, t, x, y));
                }
            }
        }

        static void PaintCobble(Canvas c)
        {
            // Worley cells on a wrapping 8×8 grid: each cell is a stone, the gaps between them are mortar.
            const int cells = 8;
            float cell = c.Width / (float)cells;
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    float best = float.MaxValue, second = float.MaxValue;
                    int bestId = 0;
                    Vector2 bestPoint = default;
                    int cx = Mathf.FloorToInt(x / cell), cy = Mathf.FloorToInt(y / cell);
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = cx + ox, gy = cy + oy;
                            int wx = Mod(gx, cells), wy = Mod(gy, cells);
                            var point = new Vector2((gx + 0.2f + 0.6f * Hash(wx, wy, 5)) * cell, (gy + 0.2f + 0.6f * Hash(wx, wy, 6)) * cell);
                            float d = Vector2.Distance(point, new Vector2(x + 0.5f, y + 0.5f));
                            if (d < best) { second = best; best = d; bestId = wy * cells + wx; bestPoint = point; }
                            else if (d < second) second = d;
                        }
                    }
                    if (second - best < 1.3f) { c.Set(x, y, StonePalette[0]); continue; }
                    // Light from the top-left: pixels toward that side of the stone are brighter.
                    var toPixel = new Vector2(x + 0.5f, y + 0.5f) - bestPoint;
                    float lit = Vector2.Dot(toPixel.normalized, new Vector2(-0.6f, 0.8f)) * Mathf.Clamp01(toPixel.magnitude / (cell * 0.5f));
                    float t = 0.35f + Hash(bestId, 0, 9) * 0.3f + lit * 0.25f + (Fbm(x, y, c.Width, 41, 4) - 0.5f) * 0.2f;
                    c.Set(x, y, Shade(StonePalette, t, x, y));
                }
            }
        }

        // ---------- Walls & roofs ----------

        static void PaintStoneWall(Canvas c)
        {
            int[] widths = { 20, 24, 20 };
            const int rowHeight = 16;
            for (int row = 0; row < c.Height / rowHeight; row++)
            {
                int offset = row % 2 == 0 ? 0 : 10;
                int x0 = -offset;
                for (int b = 0; x0 < c.Width; b++)
                {
                    int width = widths[b % widths.Length];
                    float tone = 0.3f + Hash(row, b, 3) * 0.35f;
                    for (int y = 0; y < rowHeight; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int px = x0 + x, py = row * rowHeight + y;
                            Color32 color;
                            if (x == 0 || y == 0) color = StonePalette[0];                         // mortar
                            else if (y == rowHeight - 1 || x == 1) color = StonePalette[4];          // lit top / left edge
                            else if (y == 1 || x == width - 1) color = StonePalette[1];              // shaded bottom / right edge
                            else color = Shade(StonePalette, tone + (Fbm(Mod(px, c.Width), py, c.Width, 51, 8) - 0.5f) * 0.35f, px, py);
                            c.Set(px, py, color, wrap: true);
                        }
                    }
                    x0 += width;
                }
            }
        }

        /// <summary>Stone wall overgrown with thick ivy: the visual cue for "this wall can be climbed".</summary>
        static void PaintIvyWall(Canvas c)
        {
            PaintStoneWall(c);

            // Vines: wavy vertical stems that wrap top-to-bottom (the wave period divides the tile height).
            int[] stems = { 6, 22, 38, 54 };
            for (int s = 0; s < stems.Length; s++)
            {
                float phase = Hash(s, 0, 81) * Mathf.PI * 2f;
                for (int y = 0; y < c.Height; y++)
                {
                    float wave = Mathf.Sin(y / (float)c.Height * Mathf.PI * 2f * 2f + phase) * 3f;
                    int x = stems[s] + Mathf.RoundToInt(wave);
                    c.Set(x, y, BarkPalette[1], wrap: true);
                    c.Set(x + 1, y, BarkPalette[2], wrap: true);
                }
            }

            // Leaves: dense clusters along the stems, lit from the top-left like everything else.
            for (int i = 0; i < 150; i++)
            {
                int s = i % stems.Length;
                int y = c.Range(0, c.Height);
                float phase = Hash(s, 0, 81) * Mathf.PI * 2f;
                int x = stems[s] + Mathf.RoundToInt(Mathf.Sin(y / (float)c.Height * Mathf.PI * 4f + phase) * 3f) + c.Range(-6, 7);
                float tone = 0.25f + c.Random01() * 0.6f;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -2; dx <= 1; dx++)
                    {
                        if (Mathf.Abs(dx + 0.5f) + Mathf.Abs(dy) > 2f) continue; // small diamond leaf
                        float shade = tone + (dy > 0 || dx < 0 ? 0.2f : -0.15f);
                        c.Set(x + dx, y + dy, Shade(LeafPalette, shade, x + dx, y + dy), wrap: true);
                    }
            }
        }

        static void PaintTimberPlaster(Canvas c)
        {
            // Plaster infill...
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    c.Set(x, y, Shade(PlasterPalette, 0.35f + (Fbm(x, y, c.Width, 61, 8) - 0.5f) * 0.6f, x, y));

            // ...framed by dark timbers: posts, a sill and a mid rail, plus one diagonal brace.
            void Timber(int px, int py)
            {
                float grain = Fbm(Mod(px * 3, c.Width), Mod(py, c.Height), c.Width, 71, 4);
                c.Set(px, py, Shade(WoodPalette, 0.05f + grain * 0.35f, px, py), wrap: true);
            }
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < 4; x++) { Timber(x, y); Timber(x + 32, y); }
            for (int x = 0; x < c.Width; x++)
                for (int y = 0; y < 4; y++) { Timber(x, y); Timber(x, y + 30); }
            for (int i = 0; i < 26; i++)
                for (int t = 0; t < 3; t++) Timber(36 + i, 4 + i + t);
        }

        static void PaintPlanks(Canvas c)
        {
            const int plank = 16;
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    int index = x / plank;
                    int local = x % plank;
                    int joint = 8 + Mathf.FloorToInt(Hash(index, 1, 13) * 48f);
                    if (local == 0 || y == joint) { c.Set(x, y, WoodPalette[0]); continue; }
                    // Grain: noise stretched along the plank.
                    float grain = TileNoise(x / 2f, y / 12f, 32, 17 + index) * 0.5f + TileNoise(x / 1f, y / 24f, 64, 19) * 0.3f;
                    float t = 0.25f + Hash(index, 0, 11) * 0.25f + grain * 0.45f + (local == 1 ? 0.15f : local == plank - 1 ? -0.15f : 0f);
                    c.Set(x, y, Shade(WoodPalette, t, x, y));
                }
            }
            for (int i = 0; i < c.Width / plank; i++)
            {
                c.Set(i * plank + 3, 4, StonePalette[3]);
                c.Set(i * plank + plank - 4, 4, StonePalette[3]); // nails
            }
        }

        static void PaintRoofTiles(Canvas c)
        {
            const int rowHeight = 8, tileWidth = 8;
            for (int y = 0; y < c.Height; y++)
            {
                int row = y / rowHeight;
                int local = y % rowHeight;
                for (int x = 0; x < c.Width; x++)
                {
                    int shifted = x + (row % 2) * (tileWidth / 2);
                    int column = Mod(shifted / tileWidth, c.Width / tileWidth);
                    int lx = Mod(shifted, tileWidth);
                    // Bottom of each tile is shaded and rounded (scalloped edge), top is lit.
                    bool gap = lx == 0 || (local == 0 && (lx == 1 || lx == tileWidth - 1));
                    float t = gap ? 0f
                        : local == 0 ? 0.15f
                        : 0.35f + Hash(column, row, 21) * 0.3f + local / (float)rowHeight * 0.3f;
                    c.Set(x, y, Shade(RoofPalette, t, x, y));
                }
            }
        }

        static void PaintRock(Canvas c)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    c.Set(x, y, Shade(StonePalette, 0.2f + Fbm(x, y, c.Width, 81, 8) * 0.7f, x, y));
            for (int i = 0; i < 4; i++)
            {
                int x = c.Range(0, c.Width), y = c.Range(0, c.Height);
                for (int k = 0; k < 6; k++) { c.Set(x, y, StonePalette[0], wrap: true); x += c.Range(-1, 2); y += 1; }
            }
            for (int i = 0; i < 14; i++) c.Set(c.Range(0, c.Width), c.Range(0, c.Height), C("#5d6b45"), wrap: true); // lichen
        }

        // ---------- Details ----------

        static void PaintDoor(Canvas c)
        {
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    int local = (x - 2) % 7;
                    float t = 0.3f + TileNoise(x / 2f, y / 10f, 64, 7) * 0.4f + (local == 0 ? -0.3f : 0f);
                    c.Set(x, y, Shade(WoodPalette, t, x, y));
                }
            }
            // Iron bands, frame and ring handle.
            c.FillRect(0, 9, c.Width, 3, StonePalette[1]);
            c.FillRect(0, 36, c.Width, 3, StonePalette[1]);
            for (int x = 0; x < c.Width; x += 5) { c.Set(x + 2, 10, StonePalette[4]); c.Set(x + 2, 37, StonePalette[4]); }
            c.FillRect(0, 0, 2, c.Height, WoodPalette[0]);
            c.FillRect(c.Width - 2, 0, 2, c.Height, WoodPalette[0]);
            c.FillRect(0, c.Height - 2, c.Width, 2, WoodPalette[0]);
            c.Set(24, 24, C("#c9a34e")); c.Set(25, 24, C("#c9a34e")); c.Set(24, 23, C("#8a6d2e"));
        }

        static void PaintWindow(Canvas c)
        {
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    bool frame = x < 3 || y < 3 || x >= c.Width - 3 || y >= c.Height - 3 || x is 15 or 16 || y is 15 or 16;
                    if (frame) { c.Set(x, y, Shade(WoodPalette, 0.2f + (x + y) % 3 * 0.1f, x, y)); continue; }
                    // Dim glass with a warm glow at the bottom (someone's home) and a diagonal glint.
                    float t = 0.35f + y / (float)c.Height * 0.4f;
                    var glass = Color32.Lerp(C("#8a6a3a"), C("#3c4a5a"), t);
                    if ((x + y) % 11 == 0 && y > 18) glass = C("#9fb1c1");
                    c.Set(x, y, glass);
                }
            }
        }

        static void PaintNotice(Canvas c)
        {
            var paper = P("#b9a983", "#cdbd96", "#ddcfab");
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    c.Set(x, y, Shade(paper, 0.4f + Hash(x, y, 3) * 0.3f, x, y));
            for (int line = 0; line < 5; line++)
            {
                int y = c.Height - 5 - line * 3;
                int length = c.Range(6, 12);
                for (int x = 3; x < 3 + length; x++) if (Hash(x, y, 4) > 0.2f) c.Set(x, y, C("#4b3b2a"));
            }
            c.Set(c.Width / 2, c.Height - 2, C("#7a2a22")); // red pin / seal
        }

        // ---------- Billboard sprites ----------

        /// <summary>Union of circles shaded as a soft ball lit from the top-left, quantized and dithered.</summary>
        static void Blobs(Canvas c, (float x, float y, float r)[] blobs, Color32[] palette, float brightness, int seed)
        {
            var light = new Vector2(-0.55f, 0.85f).normalized;
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    float bestDepth = float.MinValue;
                    Vector2 normal = default;
                    foreach (var (bx, by, br) in blobs)
                    {
                        var d = new Vector2(x + 0.5f - bx, y + 0.5f - by);
                        // Leafy, lumpy edge: radius wobbles with angle-ish noise.
                        float r = br * (0.88f + 0.24f * Hash(Mathf.FloorToInt(x / 3f), Mathf.FloorToInt(y / 3f), seed));
                        float depth = r - d.magnitude;
                        if (depth > bestDepth) { bestDepth = depth; normal = d / Mathf.Max(br, 0.001f); }
                    }
                    if (bestDepth < 0f) continue;
                    float lit = Vector2.Dot(normal, light) * 0.45f + 0.45f + brightness;
                    lit += (Hash(Mathf.FloorToInt(x / 2f), Mathf.FloorToInt(y / 2f), seed + 1) - 0.5f) * 0.25f; // leaf clusters
                    c.Set(x, y, Shade(palette, lit, x, y));
                }
            }
        }

        static void Trunk(Canvas c, int centerX, int top, int baseWidth, int topWidth)
        {
            for (int y = 0; y <= top; y++)
            {
                float t = y / (float)top;
                float half = Mathf.Lerp(baseWidth, topWidth, Mathf.Sqrt(t)) * 0.5f;
                if (y < 3) half += 3 - y; // root flare
                for (int x = Mathf.RoundToInt(centerX - half); x < Mathf.RoundToInt(centerX + half); x++)
                {
                    float across = (x + 0.5f - (centerX - half)) / (2f * half); // 0 left … 1 right
                    float shade = 0.75f - across * 0.6f + (Hash(x, y / 3, 91) - 0.5f) * 0.2f;
                    c.Set(x, y, Shade(BarkPalette, shade, x, y));
                }
            }
        }

        static void PaintOak(Canvas c)
        {
            Trunk(c, 32, 34, 9, 6);
            c.Line(32, 30, 22, 40, 3, BarkPalette[1]);
            c.Line(32, 32, 42, 42, 3, BarkPalette[1]);
            Blobs(c, new[]
            {
                (32f, 54f, 17f), (19f, 47f, 11f), (45f, 47f, 11f), (24f, 63f, 12f), (40f, 64f, 12f),
                (32f, 70f, 9f), (15f, 57f, 8f), (49f, 57f, 8f),
            }, LeafPalette, 0f, 3);
            c.Outline(C("#14210f"));
        }

        static void PaintPine(Canvas c)
        {
            Trunk(c, 24, 16, 6, 4);
            for (int tier = 0; tier < 5; tier++)
            {
                float baseY = 10 + tier * 15;
                float height = 28f - tier * 2f;
                float halfWidth = 22f - tier * 3.6f;
                for (int y = Mathf.FloorToInt(baseY - 2); y < baseY + height; y++)
                {
                    float t = (y - baseY) / height;
                    float half = halfWidth * (1f - t);
                    for (int x = Mathf.FloorToInt(24 - half); x <= Mathf.CeilToInt(24 + half); x++)
                    {
                        // Jagged bottom edge: hanging needle clumps.
                        if (y < baseY + ((x / 3) % 2 == 0 ? 0 : 2)) continue;
                        float across = (x - 24) / Mathf.Max(half, 1f);
                        float lit = 0.55f - across * 0.3f + t * 0.25f + (Hash(x / 2, y / 2, 33) - 0.5f) * 0.2f;
                        c.Set(x, y, Shade(PinePalette, lit, x, y));
                    }
                }
            }
            c.Outline(C("#0c1710"));
        }

        static void PaintDeadTree(Canvas c)
        {
            // A blighted tree: bare, twisted, ash-grey with a purple cast.
            void Branch(float x, float y, float angle, float length, float thickness, int depth)
            {
                float x1 = x + Mathf.Cos(angle) * length, y1 = y + Mathf.Sin(angle) * length;
                var color = Shade(BlightPalette, 0.25f + depth * 0.12f + (x1 < x ? 0.15f : 0f), (int)x1, (int)y1);
                c.Line(x, y, x1, y1, thickness, color);
                if (depth >= 4 || thickness < 1f) return;
                float spread = 0.32f + Hash(depth, (int)x, 7) * 0.28f;
                Branch(x1, y1, angle + spread, length * 0.7f, thickness * 0.6f, depth + 1);
                Branch(x1, y1, angle - spread * 0.9f, length * 0.66f, thickness * 0.6f, depth + 1);
            }
            Branch(28f, 0f, Mathf.PI * 0.5f, 24f, 6f, 0);
            for (int i = 0; i < 12; i++) c.Set(c.Range(10, 46), c.Range(30, 76), C("#7b5d86")); // drifting blight motes
            c.Outline(C("#151117"));
        }

        static void PaintBush(Canvas c)
        {
            Blobs(c, new[] { (11f, 10f, 9f), (20f, 13f, 11f), (29f, 10f, 9f), (20f, 6f, 7f), (14f, 17f, 6f), (27f, 17f, 6f) }, LeafPalette, 0.05f, 9);
            for (int i = 0; i < 4; i++)
            {
                int x = c.Range(6, 34), y = c.Range(5, 20);
                if (c.IsOpaque(x, y)) c.Set(x, y, C("#8c2f2f")); // berries
            }
            c.Outline(C("#14210f"));
        }

        static void PaintGrassTuft(Canvas c)
        {
            for (int i = 0; i < 9; i++)
            {
                float x0 = 2 + i * 1.4f;
                float lean = (x0 - 8f) * 0.35f + (c.Random01() - 0.5f) * 2f;
                float height = 5 + c.Random01() * 6f;
                c.Line(x0, 0, x0 + lean, height, 1f, GrassPalette[i % 2 == 0 ? 2 : 3]);
                c.Set(Mathf.RoundToInt(x0 + lean), Mathf.RoundToInt(height), GrassPalette[4]);
            }
        }

        static void PaintFlowers(Canvas c)
        {
            for (int i = 0; i < 6; i++)
            {
                float x0 = 3 + i * 2f;
                c.Line(x0, 0, x0 + (c.Random01() - 0.5f) * 2f, 4 + c.Random01() * 3f, 1f, GrassPalette[2]);
            }
            Color32[] petals = { C("#d9d2c2"), C("#d8b44a"), C("#8e6aa6"), C("#c46a5a") };
            for (int i = 0; i < 4; i++)
            {
                int x = 3 + i * 3, y = 6 + c.Range(0, 4);
                var petal = petals[i % petals.Length];
                c.Set(x, y, petal); c.Set(x + 1, y, petal); c.Set(x, y + 1, petal); c.Set(x + 1, y + 1, petal);
                c.Set(x, y - 1, GrassPalette[1]);
            }
        }

        static void PaintCanopyDisc(Canvas c)
        {
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    float d = new Vector2(x + 0.5f - 16f, y + 0.5f - 16f).magnitude;
                    if (d > 15f) continue;
                    byte v = (byte)(d > 12.5f ? 150 : 200 + (int)(Hash(x / 3, y / 3, 2) * 55f));
                    c.Set(x, y, new Color32(v, v, v, 255));
                }
            }
        }

        // ---------- Meshes ----------

        /// <summary>Saves or updates a mesh asset in place (keeps its GUID, so scene references survive re-runs).</summary>
        public static Mesh SaveMesh(string name, Action<Mesh> build)
        {
            EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = name };
            mesh.Clear();
            build(mesh);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Camera-facing quad (pivot at the bottom centre), with the same bent normals as the character sprites.</summary>
        public static void BuildBillboardQuad(Mesh mesh)
        {
            var normal = (Vector3.back * 0.4f + Vector3.up * 0.6f).normalized;
            mesh.vertices = new[] { new Vector3(-0.5f, 0f), new Vector3(0.5f, 0f), new Vector3(-0.5f, 1f), new Vector3(0.5f, 1f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { normal, normal, normal, normal };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        }

        /// <summary>Flat quad lying on the ground (for map canopies), centred, facing up.</summary>
        public static void BuildFlatQuad(Mesh mesh)
        {
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        }

        /// <summary>
        /// A unit cube (like Unity's primitive, -0.5..0.5) whose UVs are in world tiles for the given scale,
        /// so one shared material tiles evenly on walls of any size.
        /// </summary>
        public static void BuildWorldUvBox(Mesh mesh, Vector3 scale, float metresPerTile)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            void Face(Vector3 normal, Vector3 right, Vector3 up, float width, float height)
            {
                int start = vertices.Count;
                Vector3 center = normal * 0.5f;
                Vector3[] corners = { center - right * 0.5f - up * 0.5f, center + right * 0.5f - up * 0.5f, center - right * 0.5f + up * 0.5f, center + right * 0.5f + up * 0.5f };
                Vector2[] tile = { new(0f, 0f), new(width, 0f), new(0f, height), new(width, height) };
                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(corners[i]);
                    normals.Add(normal);
                    uvs.Add(tile[i] / metresPerTile);
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start + 2, start + 3, start + 1 });
            }

            Face(Vector3.back, Vector3.right, Vector3.up, scale.x, scale.y);
            Face(Vector3.forward, Vector3.left, Vector3.up, scale.x, scale.y);
            Face(Vector3.left, Vector3.back, Vector3.up, scale.z, scale.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, scale.z, scale.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, scale.x, scale.z);
            Face(Vector3.down, Vector3.right, Vector3.back, scale.x, scale.z);

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
        }

        /// <summary>
        /// Gable roof in metres: base at y = 0 with eaves dropping slightly, ridge along the longer side.
        /// Submesh 0 = tiles (top), submesh 1 = gable ends and underside (wood).
        /// </summary>
        public static void BuildGableRoof(Mesh mesh, float width, float depth, float height, float metresPerTile)
        {
            bool ridgeAlongX = width >= depth;
            float length = ridgeAlongX ? width : depth;   // along the ridge
            float span = ridgeAlongX ? depth : width;     // across the ridge
            const float eaveDrop = 0.25f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tiles = new List<int>();
            var wood = new List<int>();

            // Builds in "ridge space" (a = along ridge, s = across), then maps to x/z.
            Vector3 Map(float a, float y, float s) => ridgeAlongX ? new Vector3(a, y, s) : new Vector3(s, y, a);

            void Quad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector2 size, List<int> target)
            {
                int start = vertices.Count;
                var normal = Vector3.Cross(v2 - v0, v1 - v0).normalized;
                vertices.AddRange(new[] { v0, v1, v2, v3 });
                normals.AddRange(new[] { normal, normal, normal, normal });
                uvs.AddRange(new[] { Vector2.zero, new Vector2(size.x, 0f) / metresPerTile, new Vector2(0f, size.y) / metresPerTile, size / metresPerTile });
                target.AddRange(new[] { start, start + 2, start + 1, start + 2, start + 3, start + 1 });
            }

            void Triangle(Vector3 v0, Vector3 v1, Vector3 v2, List<int> target)
            {
                int start = vertices.Count;
                var normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                vertices.AddRange(new[] { v0, v1, v2 });
                normals.AddRange(new[] { normal, normal, normal });
                uvs.AddRange(new[] { new Vector2(v0.x + v0.z, v0.y) / metresPerTile, new Vector2(v1.x + v1.z, v1.y) / metresPerTile, new Vector2(v2.x + v2.z, v2.y) / metresPerTile });
                target.AddRange(new[] { start, start + 1, start + 2 });
            }

            float a0 = -length * 0.5f, a1 = length * 0.5f, s0 = -span * 0.5f, s1 = span * 0.5f;
            float slope = Mathf.Sqrt(height * height + span * span * 0.25f);
            Vector3 ridge0 = Map(a0, height, 0f), ridge1 = Map(a1, height, 0f);
            Vector3 eaveA0 = Map(a0, -eaveDrop, s0), eaveA1 = Map(a1, -eaveDrop, s0);
            Vector3 eaveB0 = Map(a0, -eaveDrop, s1), eaveB1 = Map(a1, -eaveDrop, s1);

            // Two tiled slopes (wound so both face outward/up).
            Quad(eaveA0, eaveA1, ridge0, ridge1, new Vector2(length, slope), tiles);
            Quad(eaveB1, eaveB0, ridge1, ridge0, new Vector2(length, slope), tiles);
            // Undersides (seen from low camera angles under the eaves).
            Quad(eaveA1, eaveA0, ridge1, ridge0, new Vector2(length, slope), wood);
            Quad(eaveB0, eaveB1, ridge0, ridge1, new Vector2(length, slope), wood);
            // Gable ends, inset slightly so the tiles overhang them.
            const float inset = 0.25f;
            Triangle(Map(a0 + inset, -eaveDrop, s0), Map(a0 + inset, -eaveDrop, s1), Map(a0 + inset, height, 0f), wood);
            Triangle(Map(a1 - inset, -eaveDrop, s1), Map(a1 - inset, -eaveDrop, s0), Map(a1 - inset, height, 0f), wood);
            if (!ridgeAlongX)
            {
                // Mapping a→z flips handedness: flip every triangle so faces still point outward.
                FlipWinding(tiles);
                FlipWinding(wood);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(FixNormals(vertices, normals, tiles, wood));
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tiles, 0);
            mesh.SetTriangles(wood, 1);
        }

        static void FlipWinding(List<int> triangles)
        {
            for (int i = 0; i < triangles.Count; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        }

        /// <summary>Recomputes flat normals from the final winding (so they always match the visible side).</summary>
        static List<Vector3> FixNormals(List<Vector3> vertices, List<Vector3> normals, params List<int>[] submeshes)
        {
            var result = new List<Vector3>(normals);
            foreach (var triangles in submeshes)
            {
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var b = vertices[triangles[i + 1]];
                    var c = vertices[triangles[i + 2]];
                    var n = Vector3.Cross(b - a, c - a).normalized;
                    result[triangles[i]] = n;
                    result[triangles[i + 1]] = n;
                    result[triangles[i + 2]] = n;
                }
            }
            return result;
        }

        /// <summary>Low-poly boulder: a jittered icosphere with a flattened base, flat-shaded, box-projected UVs.</summary>
        public static void BuildRock(Mesh mesh, int seed)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var random = new System.Random(seed);
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i].normalized * (0.42f + (float)random.NextDouble() * 0.16f);
                p.x *= 1.15f;
                p.y = p.y < -0.1f ? -0.1f : p.y * 0.8f; // flat bottom sits on the ground
                points[i] = p + Vector3.up * 0.1f;
            }

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                // The face list is wound so (b - a) × (c - a) points outward: Unity's visible side.
                var a = points[faces[f]];
                var b = points[faces[f + 1]];
                var c = points[faces[f + 2]];
                var n = Vector3.Cross(b - a, c - a).normalized;
                foreach (var v in new[] { a, b, c })
                {
                    triangles.Add(vertices.Count);
                    vertices.Add(v);
                    // Box projection on the dominant axis.
                    var abs = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                    uvs.Add(abs.y > abs.x && abs.y > abs.z ? new Vector2(v.x, v.z) : abs.x > abs.z ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y));
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
        }

        /// <summary>A flat ribbon along a polyline (paths), UVs in world tiles, edges slightly ragged.</summary>
        public static void BuildRibbon(Mesh mesh, IList<Vector3> points, float width, float metresPerTile, int seed)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var random = new System.Random(seed);
            float travelled = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 forward = i < points.Count - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1];
                forward.y = 0f;
                forward.Normalize();
                var side = new Vector3(forward.z, 0f, -forward.x);
                if (i > 0) travelled += Vector3.Distance(points[i], points[i - 1]);
                float left = width * 0.5f * (0.85f + (float)random.NextDouble() * 0.3f);
                float right = width * 0.5f * (0.85f + (float)random.NextDouble() * 0.3f);
                vertices.Add(points[i] - side * left);
                vertices.Add(points[i] + side * right);
                uvs.Add(new Vector2(-left / metresPerTile, travelled / metresPerTile));
                uvs.Add(new Vector2(right / metresPerTile, travelled / metresPerTile));
                if (i == 0) continue;
                int b = vertices.Count - 4;
                triangles.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            var up = new Vector3[vertices.Count];
            for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.normals = up;
        }

        /// <summary>
        /// An irregular flat disc (a village square), UVs in world tiles. Rim points for which 'blocked' returns true
        /// (positions relative to the centre) are pulled inward, so the disc bends around e.g. a field.
        /// </summary>
        public static void BuildDisc(Mesh mesh, float radius, float metresPerTile, int seed, Func<Vector3, bool> blocked = null)
        {
            const int segments = 28;
            var vertices = new List<Vector3> { Vector3.zero };
            var triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float r = radius * (0.88f + 0.24f * Hash(i, 0, seed));
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // Pull the rim in until it's clear, checking the whole spoke (the edge is straight between rim points).
                while (blocked != null && r > 1f)
                {
                    bool clear = true;
                    for (float d = 0.5f; d <= r && clear; d += 0.5f) clear = !blocked(direction * d);
                    if (clear) break;
                    r -= 0.25f;
                }
                vertices.Add(direction * r);
                triangles.AddRange(new[] { 0, i + 1 == segments ? 1 : i + 2, i + 1 });
            }
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            foreach (var v in vertices)
            {
                uvs.Add(new Vector2(v.x, v.z) / metresPerTile);
                normals.Add(Vector3.up);
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
