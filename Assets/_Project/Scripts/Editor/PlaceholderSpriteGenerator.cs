using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Writes placeholder character sheets (PNG) for NPCs and enemies. The drawing itself lives in
    /// CharacterSpriteBuilder (runtime), which also redraws the player from their chosen look.
    /// </summary>
    public static class PlaceholderSpriteGenerator
    {
        public const int CellWidth = CharacterSpriteBuilder.CellWidth;
        public const int CellHeight = CharacterSpriteBuilder.CellHeight;
        public const int Rows = CharacterSpriteBuilder.Rows;
        public const float PixelsPerUnit = CharacterSpriteBuilder.PixelsPerUnit;
        public const float PivotY = CharacterSpriteBuilder.PivotY;
        public const int Columns = CharacterSpriteBuilder.Columns;
        /// <summary>Column count of sheets generated before the climbing clips existed (Milestone 3).</summary>
        public const int LegacyColumns = 21;

        public static readonly CharacterSpriteBuilder.ClipLayout[] Layout = CharacterSpriteBuilder.Layout;

        public struct Palette
        {
            public Color32 Body, Trim, Skin, Hair, Weapon, Outline;
            public bool HasWeapon, HasShield;
            /// <summary>A bow instead of a sword (archers).</summary>
            public bool Bow;

            /// <summary>The original placeholder look: short hair, the shield painted in the hair colour, one-handed sword.</summary>
            public CharacterSpriteBuilder.Palette ToBuilder() => new()
            {
                Body = Body, Trim = Trim, Skin = Skin, Hair = Hair, Weapon = Weapon, Outline = Outline, ShieldFace = Hair,
                HasWeapon = HasWeapon, HasShield = HasShield, HairStyle = HairStyle.Short, Bow = Bow,
            };
        }

        /// <summary>Writes the PNG (if missing) with pixel-art import settings and returns the texture.</summary>
        public static Texture2D CreateSheetTexture(string assetPath, Palette palette) => CreateSheetTexture(assetPath, palette, false);

        /// <summary>As above; 'regenerate' redraws an existing placeholder PNG (to add new clips).</summary>
        public static Texture2D CreateSheetTexture(string assetPath, Palette palette, bool regenerate)
        {
            if (regenerate || !File.Exists(assetPath))
            {
                var texture = CharacterSpriteBuilder.Build(palette.ToBuilder());
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
    }
}
