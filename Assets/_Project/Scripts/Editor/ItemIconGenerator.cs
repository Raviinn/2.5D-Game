using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Draws 32×32 pixel-art placeholder icons for items. The drawing is picked from the item asset's name
    /// (Bread, Healroot, Greatsword...), falling back to a shape per category for items added later.
    /// Real icons replace these by assigning a different sprite to the item's Icon field.
    /// </summary>
    public static class ItemIconGenerator
    {
        public const int Size = 32;

        static readonly Color32 Outline = new(28, 22, 30, 255);
        static readonly Color32 Steel = new(196, 202, 214, 255);
        static readonly Color32 SteelLight = new(236, 240, 246, 255);
        static readonly Color32 SteelDark = new(120, 126, 140, 255);
        static readonly Color32 Rust = new(156, 96, 62, 255);
        static readonly Color32 RustLight = new(196, 130, 84, 255);
        static readonly Color32 RustDark = new(104, 62, 42, 255);
        static readonly Color32 Brass = new(210, 170, 80, 255);
        static readonly Color32 BrassDark = new(150, 112, 50, 255);
        static readonly Color32 Grip = new(110, 72, 44, 255);
        static readonly Color32 Leaf = new(92, 168, 72, 255);
        static readonly Color32 LeafDark = new(58, 116, 50, 255);

        /// <summary>Writes the PNG (if missing) as a point-filtered sprite and returns it.</summary>
        public static Sprite CreateIcon(string assetPath, ItemData item, bool regenerate = false) =>
            Write(assetPath, canvas => Draw(canvas, item), regenerate);

        /// <summary>A small stack of coins, for gold dropped in the world.</summary>
        public static Sprite CreateGoldIcon(string assetPath, bool regenerate = false) => Write(assetPath, DrawGold, regenerate);

        static Sprite Write(string assetPath, System.Action<Canvas> draw, bool regenerate)
        {
            if (regenerate || !File.Exists(assetPath))
            {
                var canvas = new Canvas();
                draw(canvas);
                canvas.AddOutline(Outline);
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                texture.SetPixels32(canvas.Pixels);
                texture.Apply();
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(assetPath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        static void Draw(Canvas c, ItemData item)
        {
            string n = item.name;
            bool rusted = n.Contains("Rusted");
            if (n.Contains("Seeds")) DrawSeedPouch(c, n.Contains("Healroot") ? new Color32(196, 52, 66, 255) : new Color32(96, 176, 76, 255));
            else if (n.Contains("Bread")) DrawBread(c);
            else if (n.Contains("Draught") || n.Contains("Potion") || n.Contains("Tonic")) DrawFlask(c, item.PlaceholderColor);
            else if (n.Contains("Stew")) DrawBowl(c, new Color32(176, 112, 64, 255), steam: true);
            else if (n.Contains("Roast")) DrawRoast(c);
            else if (n.Contains("Healroot")) DrawHealroot(c);
            else if (n.Contains("Turnip")) DrawTurnip(c);
            else if (n.Contains("Cloth")) DrawCloth(c);
            else if (n.Contains("Scrap")) DrawScrap(c);
            else if (n.Contains("Helm")) DrawHelm(c);
            else if (n.Contains("Cap")) DrawCap(c);
            else if (n.Contains("Hauberk") || n.Contains("Chain")) DrawTunic(c, chain: true);
            else if (n.Contains("Gambeson")) DrawTunic(c, chain: false);
            else if (n.Contains("Leggings")) DrawLeggings(c);
            else if (n.Contains("Token")) DrawToken(c);
            else if (n.Contains("Greatsword")) DrawGreatsword(c, rusted);
            else if (n.Contains("SwordShield")) DrawSwordAndShield(c, rusted);
            else DrawByCategory(c, item);
        }

        /// <summary>Items without a drawing of their own get a shape that says what kind of thing they are.</summary>
        static void DrawByCategory(Canvas c, ItemData item)
        {
            Color32 color = item.PlaceholderColor;
            switch (item.Category)
            {
                case ItemCategory.Consumable: DrawFlask(c, color); break;
                case ItemCategory.Seed: DrawSeedPouch(c, color); break;
                case ItemCategory.Crop:
                    Leaves(c, 16, 11);
                    c.Circle(16, 20, 8, color);
                    c.Circle(13, 17, 2, Lighten(color, 0.35f));
                    break;
                case ItemCategory.Equipment:
                    if (item is WeaponData) DrawSwordAndShield(c, false);
                    else DrawTunic(c, chain: false);
                    break;
                case ItemCategory.Quest:
                    c.Rect(7, 8, 18, 16, new Color32(226, 208, 164, 255));
                    c.Rect(5, 6, 22, 4, new Color32(196, 172, 120, 255));
                    c.Rect(5, 22, 22, 4, new Color32(196, 172, 120, 255));
                    for (int y = 12; y <= 19; y += 3) c.Rect(10, y, 12, 1, new Color32(120, 96, 70, 255));
                    break;
                default:
                    c.Polygon(color, (7, 20), (13, 9), (24, 11), (26, 22), (15, 27));
                    c.Polygon(Lighten(color, 0.3f), (13, 9), (24, 11), (19, 16));
                    break;
            }
        }

        // ---------- Food & crops ----------

        static void DrawBread(Canvas c)
        {
            c.Ellipse(16, 20, 13, 7, new Color32(160, 102, 50, 255));
            c.Ellipse(16, 17, 13, 7, new Color32(208, 148, 74, 255));
            c.Ellipse(14, 14, 9, 3, new Color32(234, 182, 104, 255));
            for (int i = 0; i < 3; i++)
                c.Line(9 + i * 6, 18, 12 + i * 6, 13, new Color32(150, 96, 46, 255), 1);
        }

        static void DrawFlask(Canvas c, Color32 liquid)
        {
            var glass = new Color32(198, 218, 230, 255);
            c.Circle(16, 20, 9, glass);
            c.Rect(13, 6, 6, 7, glass);
            c.Circle(16, 21, 7, liquid, minY: 17);
            c.Rect(14, 18, 5, 1, Lighten(liquid, 0.3f));
            c.Rect(12, 3, 8, 4, new Color32(150, 104, 60, 255));
            c.Rect(12, 3, 8, 1, new Color32(186, 138, 86, 255));
            c.Rect(10, 17, 2, 4, new Color32(250, 252, 255, 255));
        }

        static void DrawBowl(Canvas c, Color32 soup, bool steam)
        {
            var wood = new Color32(132, 86, 50, 255);
            var woodLight = new Color32(170, 116, 70, 255);
            c.Ellipse(16, 21, 13, 7, wood);
            c.Rect(5, 17, 23, 4, wood);
            c.Ellipse(16, 16, 12, 4, woodLight);
            c.Ellipse(16, 16, 10, 3, soup);
            c.Circle(12, 16, 1, new Color32(150, 78, 168, 255));
            c.Circle(19, 15, 1, new Color32(238, 230, 238, 255));
            if (!steam) return;
            var vapour = new Color32(236, 236, 236, 255);
            c.Line(11, 11, 12, 5, vapour, 1);
            c.Line(16, 11, 15, 4, vapour, 1);
            c.Line(21, 11, 22, 6, vapour, 1);
        }

        static void DrawRoast(Canvas c)
        {
            c.Line(4, 27, 28, 6, new Color32(150, 104, 60, 255), 2); // skewer
            c.Circle(11, 21, 5, new Color32(206, 150, 90, 255));
            c.Circle(11, 21, 5, new Color32(160, 82, 120, 255), maxY: 18);
            c.Circle(20, 13, 5, new Color32(206, 150, 90, 255));
            c.Circle(20, 13, 5, new Color32(160, 82, 120, 255), maxY: 10);
            c.Rect(9, 22, 3, 1, new Color32(110, 66, 36, 255));
            c.Rect(18, 14, 3, 1, new Color32(110, 66, 36, 255));
        }

        static void DrawHealroot(Canvas c)
        {
            Leaves(c, 16, 10);
            c.Line(16, 24, 19, 30, new Color32(150, 30, 44, 255), 2);
            c.Ellipse(16, 18, 6, 8, new Color32(184, 38, 54, 255));
            c.Rect(13, 13, 2, 6, new Color32(232, 96, 108, 255));
            c.Line(11, 21, 8, 24, new Color32(150, 30, 44, 255), 1);
            c.Line(21, 19, 24, 21, new Color32(150, 30, 44, 255), 1);
        }

        static void DrawTurnip(Canvas c)
        {
            Leaves(c, 16, 11);
            c.Line(16, 26, 16, 30, new Color32(214, 200, 210, 255), 1);
            c.Circle(16, 20, 8, new Color32(238, 230, 238, 255));
            c.Circle(16, 20, 8, new Color32(150, 78, 168, 255), maxY: 18);
            c.Rect(12, 14, 3, 2, new Color32(196, 128, 210, 255));
        }

        static void Leaves(Canvas c, int x, int y)
        {
            c.Line(x, y, x - 6, y - 8, LeafDark, 2);
            c.Line(x, y, x + 6, y - 8, LeafDark, 2);
            c.Line(x, y, x, y - 9, Leaf, 2);
            c.Line(x - 1, y - 1, x - 5, y - 6, Leaf, 1);
            c.Line(x + 1, y - 1, x + 5, y - 6, Leaf, 1);
        }

        static void DrawSeedPouch(Canvas c, Color32 tag)
        {
            var burlap = new Color32(194, 162, 112, 255);
            var burlapDark = new Color32(156, 124, 82, 255);
            c.Ellipse(16, 21, 10, 9, burlap);
            c.Ellipse(16, 25, 9, 4, burlapDark, minY: 26);
            c.Rect(12, 8, 8, 5, burlap);
            c.Ellipse(16, 7, 6, 3, burlap);
            c.Rect(11, 12, 10, 2, new Color32(116, 84, 56, 255));
            c.Circle(16, 21, 5, tag);
            c.Rect(14, 19, 2, 2, Darken(tag, 0.45f));
            c.Rect(17, 21, 2, 2, Darken(tag, 0.45f));
            c.Rect(14, 23, 2, 2, Darken(tag, 0.45f));
        }

        // ---------- Materials ----------

        static void DrawCloth(Canvas c)
        {
            var cloth = new Color32(132, 58, 46, 255);
            var dark = new Color32(96, 40, 34, 255);
            c.Rect(5, 9, 22, 15, cloth);
            c.Rect(5, 11, 22, 2, new Color32(170, 86, 64, 255));
            c.Rect(5, 16, 22, 1, dark);
            c.Rect(5, 21, 22, 1, dark);
            c.Polygon(dark, (19, 24), (27, 24), (27, 29));
            foreach (int y in new[] { 10, 14, 19, 22 }) c.Clear(26, y); // frayed edge
            foreach (int y in new[] { 12, 17, 20 }) c.Clear(5, y);
        }

        static void DrawScrap(Canvas c)
        {
            c.Polygon(new Color32(132, 138, 150, 255), (5, 21), (13, 12), (20, 16), (17, 27), (8, 27));
            c.Polygon(new Color32(100, 106, 118, 255), (17, 9), (27, 7), (28, 18), (21, 15));
            c.Line(6, 20, 13, 13, SteelLight, 1);
            c.Line(18, 9, 26, 8, SteelLight, 1);
            c.Rect(11, 21, 2, 2, Outline);
            c.Rect(23, 11, 2, 2, Outline);
            c.Rect(14, 24, 3, 2, Rust);
        }

        // ---------- Armour ----------

        static void DrawHelm(Canvas c)
        {
            var iron = new Color32(150, 156, 168, 255);
            c.Circle(16, 16, 11, iron, maxY: 17);
            c.Rect(5, 16, 22, 10, iron);
            c.Rect(5, 16, 22, 2, SteelDark);
            c.Rect(9, 19, 14, 8, new Color32(40, 34, 44, 255));
            c.Rect(15, 14, 3, 11, iron);
            c.Rect(10, 8, 3, 6, SteelLight);
        }

        static void DrawCap(Canvas c)
        {
            var cloth = new Color32(176, 144, 102, 255);
            c.Circle(16, 17, 11, cloth);
            c.Ellipse(16, 22, 6, 6, new Color32(60, 46, 40, 255));
            for (int x = 8; x <= 24; x += 4) c.Line(x, 8, x, 15, new Color32(140, 110, 74, 255), 1);
            c.Rect(5, 25, 22, 3, new Color32(150, 120, 82, 255));
        }

        static void DrawTunic(Canvas c, bool chain)
        {
            var body = chain ? new Color32(146, 152, 164, 255) : new Color32(192, 166, 120, 255);
            var dark = chain ? new Color32(96, 102, 116, 255) : new Color32(150, 124, 84, 255);
            c.Rect(2, 8, 6, 13, body);
            c.Rect(24, 8, 6, 13, body);
            c.Polygon(body, (6, 6), (26, 6), (24, 29), (8, 29));
            c.Polygon(dark, (13, 6), (19, 6), (16, 11));
            if (chain)
            {
                for (int y = 9; y < 29; y += 2)
                    for (int x = 3 + y % 4 / 2; x < 30; x += 3)
                        if (c.IsSet(x, y)) c.Set(x, y, dark);
            }
            else
            {
                for (int y = 12; y < 29; y += 4) c.Rect(7, y, 18, 1, dark);
                for (int y = 12; y < 21; y += 4) { c.Rect(2, y, 6, 1, dark); c.Rect(24, y, 6, 1, dark); }
            }
            c.Rect(8, 22, 16, 2, new Color32(100, 70, 46, 255)); // belt
            c.Rect(15, 22, 2, 2, Brass);
        }

        static void DrawLeggings(Canvas c)
        {
            var leather = new Color32(142, 96, 60, 255);
            var dark = new Color32(104, 68, 42, 255);
            c.Polygon(leather, (8, 7), (16, 7), (15, 29), (9, 29));
            c.Polygon(leather, (16, 7), (24, 7), (23, 29), (17, 29));
            c.Rect(8, 4, 16, 4, dark);
            c.Rect(15, 4, 2, 4, Brass);
            c.Rect(10, 17, 4, 3, dark);
            c.Rect(18, 17, 4, 3, dark);
            c.Line(11, 8, 11, 15, new Color32(176, 126, 84, 255), 1);
        }

        static void DrawToken(Canvas c)
        {
            var cord = new Color32(120, 84, 56, 255);
            c.Line(7, 2, 15, 12, cord, 1);
            c.Line(25, 2, 17, 12, cord, 1);
            c.Circle(16, 20, 9, Brass);
            c.Circle(16, 20, 6, BrassDark);
            c.Rect(15, 15, 3, 11, Brass);
            c.Rect(11, 19, 11, 3, Brass);
            c.Rect(11, 14, 3, 2, new Color32(244, 220, 140, 255));
        }

        // ---------- Weapons ----------

        static void DrawGreatsword(Canvas c, bool rusted)
        {
            var blade = rusted ? Rust : Steel;
            c.Line(11, 21, 28, 4, blade, 3);
            c.Line(12, 19, 27, 4, rusted ? RustLight : SteelLight, 1);
            if (rusted) { c.Rect(20, 10, 2, 2, RustDark); c.Rect(15, 16, 2, 1, RustDark); }
            c.Line(6, 17, 15, 26, rusted ? RustDark : Brass, 2);
            c.Line(10, 22, 4, 28, Grip, 2);
            c.Circle(3, 29, 2, rusted ? RustDark : Brass);
        }

        static void DrawSwordAndShield(Canvas c, bool rusted)
        {
            var blade = rusted ? Rust : Steel;
            c.Line(15, 16, 28, 3, blade, 2);
            c.Line(16, 15, 27, 4, rusted ? RustLight : SteelLight, 1);
            c.Line(13, 13, 19, 19, rusted ? RustDark : Brass, 2);
            c.Line(15, 17, 12, 20, Grip, 2);

            var rim = rusted ? new Color32(120, 104, 90, 255) : Brass;
            var face = rusted ? new Color32(112, 82, 56, 255) : new Color32(72, 98, 146, 255);
            c.Polygon(rim, (2, 11), (19, 11), (19, 22), (10.5f, 30), (2, 22));
            c.Polygon(face, (4, 13), (17, 13), (17, 21), (10.5f, 28), (4, 21));
            c.Rect(9, 13, 3, 14, rim);
            c.Rect(4, 17, 13, 2, rim);
            if (rusted) { c.Rect(6, 23, 2, 2, RustDark); c.Rect(14, 14, 2, 2, RustDark); }
        }

        static void DrawGold(Canvas c)
        {
            var gold = new Color32(232, 186, 64, 255);
            var dark = new Color32(176, 128, 36, 255);
            var light = new Color32(255, 230, 140, 255);
            for (int i = 0; i < 4; i++)
            {
                int y = 25 - i * 4;
                c.Ellipse(13, y + 1, 9, 3, dark);
                c.Ellipse(13, y, 9, 3, gold);
            }
            c.Ellipse(13, 13, 6, 1, light);
            c.Ellipse(24, 24, 6, 6, dark);
            c.Ellipse(24, 23, 6, 6, gold);
            c.Ellipse(24, 23, 3, 3, dark);
            c.Rect(21, 19, 2, 2, light);
        }

        static Color32 Lighten(Color32 c, float t) => Color32.Lerp(c, new Color32(255, 255, 255, 255), t);
        static Color32 Darken(Color32 c, float t) => Color32.Lerp(c, new Color32(0, 0, 0, 255), t);

        // ---------- Canvas ----------

        /// <summary>32×32 pixels. Coordinates are (x right, y DOWN from the top) to match how icons are sketched.</summary>
        sealed class Canvas
        {
            public readonly Color32[] Pixels = new Color32[Size * Size];

            public void Set(int x, int y, Color32 color)
            {
                if ((uint)x >= Size || (uint)y >= Size) return;
                color.a = 255;
                Pixels[(Size - 1 - y) * Size + x] = color;
            }

            public void Clear(int x, int y)
            {
                if ((uint)x >= Size || (uint)y >= Size) return;
                Pixels[(Size - 1 - y) * Size + x] = default;
            }

            public bool IsSet(int x, int y) => (uint)x < Size && (uint)y < Size && Pixels[(Size - 1 - y) * Size + x].a != 0;

            public void Rect(int x, int y, int w, int h, Color32 color)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        Set(x + i, y + j, color);
            }

            public void Circle(int cx, int cy, int r, Color32 color, int minY = int.MinValue, int maxY = int.MaxValue) =>
                Ellipse(cx, cy, r, r, color, minY, maxY);

            public void Ellipse(int cx, int cy, int rx, int ry, Color32 color, int minY = int.MinValue, int maxY = int.MaxValue)
            {
                for (int y = -ry; y <= ry; y++)
                {
                    if (cy + y < minY || cy + y > maxY) continue;
                    for (int x = -rx; x <= rx; x++)
                    {
                        float nx = x / (rx + 0.5f), ny = y / (ry + 0.5f);
                        if (nx * nx + ny * ny <= 1f) Set(cx + x, cy + y, color);
                    }
                }
            }

            public void Line(int x0, int y0, int x1, int y1, Color32 color, int thickness)
            {
                int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                int error = dx + dy;
                int offset = (thickness - 1) / 2;
                while (true)
                {
                    Rect(x0 - offset, y0 - offset, thickness, thickness, color);
                    if (x0 == x1 && y0 == y1) break;
                    int e2 = 2 * error;
                    if (e2 >= dy) { error += dy; x0 += sx; }
                    if (e2 <= dx) { error += dx; y0 += sy; }
                }
            }

            /// <summary>Fills a polygon (pixel centres inside it).</summary>
            public void Polygon(Color32 color, params (float x, float y)[] points)
            {
                for (int y = 0; y < Size; y++)
                {
                    float py = y + 0.5f;
                    var crossings = new List<float>();
                    for (int i = 0; i < points.Length; i++)
                    {
                        var a = points[i];
                        var b = points[(i + 1) % points.Length];
                        if ((a.y <= py && b.y > py) || (b.y <= py && a.y > py))
                            crossings.Add(a.x + (py - a.y) / (b.y - a.y) * (b.x - a.x));
                    }
                    crossings.Sort();
                    for (int i = 0; i + 1 < crossings.Count; i += 2)
                        for (int x = Mathf.CeilToInt(crossings[i] - 0.5f); x <= Mathf.FloorToInt(crossings[i + 1] - 0.5f); x++)
                            Set(x, y, color);
                }
            }

            /// <summary>1px dark outline around everything drawn.</summary>
            public void AddOutline(Color32 outline)
            {
                var toOutline = new List<(int, int)>();
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        if (!IsSet(x, y) && (IsSet(x - 1, y) || IsSet(x + 1, y) || IsSet(x, y - 1) || IsSet(x, y + 1)))
                            toOutline.Add((x, y));
                foreach (var (x, y) in toOutline) Set(x, y, outline);
            }
        }
    }
}
