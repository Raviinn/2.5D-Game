using System;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>How the player's weapon is drawn. Set per combat style (MovesetData.Look).</summary>
    public enum WeaponLook
    {
        SwordAndShield,
        Greatsword,
    }

    /// <summary>
    /// The player's chosen look (character creator). Stored as indices into the preset lists below,
    /// so saves stay small and the lists can grow (append only: saves keep the index).
    /// </summary>
    [Serializable]
    public sealed class CharacterAppearance
    {
        public struct Outfit
        {
            public string Name;
            public Color32 Body, Trim, ShieldFace;
        }

        public static readonly string[] HairStyleNames = { "Short", "Long", "Ponytail", "Bun", "Shaved" };

        public static readonly string[] HairColorNames = { "Ash", "Black", "Dark brown", "Chestnut", "Auburn", "Blonde", "White", "Woad blue" };
        public static readonly Color32[] HairColors =
        {
            new(150, 156, 168, 255), new(40, 34, 38, 255), new(82, 54, 36, 255), new(128, 72, 40, 255),
            new(156, 62, 40, 255), new(214, 182, 110, 255), new(228, 226, 222, 255), new(70, 92, 156, 255),
        };

        public static readonly string[] SkinToneNames = { "Pale", "Fair", "Light", "Tan", "Brown", "Deep" };
        public static readonly Color32[] SkinTones =
        {
            new(248, 218, 194, 255), new(232, 192, 160, 255), new(214, 164, 124, 255),
            new(178, 124, 86, 255), new(132, 88, 58, 255), new(92, 62, 44, 255),
        };

        public static readonly Outfit[] Outfits =
        {
            new() { Name = "Knight's Tabard", Body = new(70, 95, 140, 255), Trim = new(200, 170, 80, 255), ShieldFace = new(150, 156, 168, 255) },
            new() { Name = "Hollows Wool", Body = new(108, 120, 72, 255), Trim = new(96, 68, 44, 255), ShieldFace = new(128, 96, 62, 255) },
            new() { Name = "Wine Coat", Body = new(116, 42, 72, 255), Trim = new(214, 186, 116, 255), ShieldFace = new(90, 34, 56, 255) },
            new() { Name = "Ranger Leathers", Body = new(104, 74, 50, 255), Trim = new(62, 94, 62, 255), ShieldFace = new(74, 110, 70, 255) },
            new() { Name = "Ashen Plate", Body = new(92, 96, 106, 255), Trim = new(176, 178, 188, 255), ShieldFace = new(60, 62, 72, 255) },
        };

        static readonly Color32 Outline = new(24, 20, 30, 255);
        static readonly Color32 Steel = new(215, 220, 230, 255);

        /// <summary>The defaults match the original Knight placeholder.</summary>
        public int hairStyle;
        public int hairColor;
        public int skinTone = 1;
        public int outfit;

        public CharacterAppearance Clone() => (CharacterAppearance)MemberwiseClone();

        /// <summary>Pulls every index back into range (old or hand-edited saves).</summary>
        public void Clamp()
        {
            hairStyle = Mathf.Clamp(hairStyle, 0, HairStyleNames.Length - 1);
            hairColor = Mathf.Clamp(hairColor, 0, HairColors.Length - 1);
            skinTone = Mathf.Clamp(skinTone, 0, SkinTones.Length - 1);
            outfit = Mathf.Clamp(outfit, 0, Outfits.Length - 1);
        }

        public static CharacterAppearance Random()
        {
            return new CharacterAppearance
            {
                hairStyle = UnityEngine.Random.Range(0, HairStyleNames.Length),
                hairColor = UnityEngine.Random.Range(0, HairColors.Length),
                skinTone = UnityEngine.Random.Range(0, SkinTones.Length),
                outfit = UnityEngine.Random.Range(0, Outfits.Length),
            };
        }

        public bool SameAs(CharacterAppearance other) =>
            other != null && hairStyle == other.hairStyle && hairColor == other.hairColor && skinTone == other.skinTone && outfit == other.outfit;

        public CharacterSpriteBuilder.Palette ToPalette(WeaponLook look)
        {
            Clamp();
            var clothes = Outfits[outfit];
            return new CharacterSpriteBuilder.Palette
            {
                Body = clothes.Body,
                Trim = clothes.Trim,
                ShieldFace = clothes.ShieldFace,
                Skin = SkinTones[skinTone],
                Hair = HairColors[hairColor],
                HairStyle = (HairStyle)hairStyle,
                Weapon = Steel,
                Outline = Outline,
                HasWeapon = true,
                HasShield = look == WeaponLook.SwordAndShield,
                Greatsword = look == WeaponLook.Greatsword,
            };
        }
    }
}
