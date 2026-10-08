using UnityEngine;

namespace Beast.Gameplay
{
    public static partial class CharacterSpriteBuilder
    {
        /// <summary>
        /// A hen (Milestone 45). Palette: Body = feathers, Trim = wing and tail, Skin = comb and wattle, Hair = eye,
        /// Weapon = beak and legs. Idle pecks now and then (frame 2), running bobs along on quick legs.
        /// </summary>
        static void DrawHenCell(Canvas c, int row, Pose pose, Palette p)
        {
            if (pose.Lying)
            {
                FillEllipse(c, 24, 5, 6, 3, p.Body);
                FillEllipse(c, 22, 6, 3, 2, p.Trim);
                FillCircle(c, 31, 5, 2, p.Body);
                return;
            }
            float side = row switch { 1 => 0.6f, 2 => 1f, 3 => 0.6f, _ => 0f };
            bool back = row >= 3;
            bool peck = pose.Bob > 0 && pose.LegSwing == 0f && pose.Lean == 0; // idle frames with a bob: head down
            int stride = Mathf.RoundToInt(pose.LegSwing * 2f);
            int bodyX = 24 - Mathf.RoundToInt(2f * side);
            int bodyY = 9 + (pose.LegSwing != 0f && Mathf.Abs(pose.LegSwing) > 0.7f ? 1 : 0);

            // Legs.
            int legGap = side > 0.3f ? 2 : 3;
            FillRect(c, bodyX - legGap + stride, 2, 1, bodyY - 5, p.Weapon);
            FillRect(c, bodyX + legGap - stride, 2, 1, bodyY - 5, p.Weapon);
            FillRect(c, bodyX - legGap + stride - 1, 2, 3, 1, p.Weapon);
            FillRect(c, bodyX + legGap - stride - 1, 2, 3, 1, p.Weapon);

            int headX = bodyX + Mathf.RoundToInt(6f * side) + (peck ? Mathf.RoundToInt(2f * side) : 0);
            int headY = bodyY + (peck ? 1 : 6);
            int tailX = bodyX - Mathf.RoundToInt(6f * side);

            if (back) DrawHenHead(c, side, headX, headY, back, p);
            // Tail feathers: up and back.
            if (side > 0.3f || back)
            {
                FillRect(c, tailX - 2, bodyY + 1, 4, 6, p.Trim);
                FillRect(c, tailX - 3, bodyY + 4, 2, 4, p.Trim);
            }
            FillEllipse(c, bodyX, bodyY, 5 + Mathf.RoundToInt(side), 4, p.Body);
            if (side > 0.3f) FillEllipse(c, bodyX - 1, bodyY, 3, 2, p.Trim); // folded wing
            else if (!back) { FillRect(c, bodyX - 6, bodyY - 1, 2, 3, p.Trim); FillRect(c, bodyX + 5, bodyY - 1, 2, 3, p.Trim); }
            if (!back) DrawHenHead(c, side, headX, headY, back, p);
        }

        static void DrawHenHead(Canvas c, float side, int hx, int hy, bool back, Palette p)
        {
            FillRect(c, hx - 1 - Mathf.RoundToInt(side), hy - 4, 3, 4, p.Body); // neck
            FillCircle(c, hx, hy, 3, p.Body);
            FillRect(c, hx - 1, hy + 3, 3, 2, p.Skin);                          // comb
            if (back) return;
            if (side > 0.3f)
            {
                FillRect(c, hx + 3, hy - 1, 2, 2, p.Weapon);                   // beak
                FillRect(c, hx + 2, hy - 3, 1, 2, p.Skin);                     // wattle
                c.Set(hx + 1, hy + 1, p.Hair);
            }
            else
            {
                FillRect(c, hx - 1, hy - 2, 2, 2, p.Weapon);
                c.Set(hx - 2, hy + 1, p.Hair);
                c.Set(hx + 1, hy + 1, p.Hair);
            }
        }

        /// <summary>
        /// A cow (Milestone 45): big, boxy, patched. Palette: Body = hide, Trim = patches and hooves, Skin = muzzle and
        /// udder, Hair = eyes, Weapon = horns.
        /// </summary>
        static void DrawCowCell(Canvas c, int row, Pose pose, Palette p)
        {
            if (pose.Lying)
            {
                FillEllipse(c, 22, 8, 15, 6, p.Body);
                FillEllipse(c, 18, 10, 4, 3, p.Trim);
                FillCircle(c, 39, 9, 5, p.Body);
                FillRect(c, 42, 5, 5, 4, p.Skin);
                return;
            }
            float side = row switch { 1 => 0.6f, 2 => 1f, 3 => 0.6f, _ => 0f };
            bool back = row >= 3;
            float swing = pose.LegSwing;
            int stride = Mathf.RoundToInt(swing * 2f * Mathf.Max(side, 0.35f));
            int bodyHalf = Mathf.RoundToInt(9f + 7f * side);
            int bodyX = 24 - Mathf.RoundToInt(3f * side);
            int bodyY = 17 - pose.Bob;
            int headX = 24 + Mathf.RoundToInt(15f * side);
            int headY = 22 - pose.Bob + (back ? 1 : 0) - (pose.Bob > 0 && swing == 0f ? 2 : 0); // grazing dips the head

            // Legs: thick, with dark hooves.
            int legTop = bodyY - 4;
            void CowLeg(int x, bool lifted, Color32 color)
            {
                int foot = lifted ? 3 : 2;
                FillRect(c, x, foot, 4, legTop - foot + 1, color);
                FillRect(c, x, foot, 4, 2, p.Trim);
            }
            if (side > 0.3f)
            {
                int frontX = bodyX + bodyHalf - 6, rearX = bodyX - bodyHalf + 2;
                CowLeg(frontX + 2 - stride, swing < 0f, p.Body);
                CowLeg(rearX + 2 + stride, swing > 0f, p.Body);
                CowLeg(frontX + stride, swing > 0f, p.Body);
                CowLeg(rearX - stride, swing < 0f, p.Body);
            }
            else
            {
                CowLeg(24 - 8 + stride, swing > 0f, p.Body);
                CowLeg(24 + 4 - stride, swing < 0f, p.Body);
            }

            if (back) DrawCowHead(c, side, headX, headY, back, p);
            FillEllipse(c, bodyX, bodyY, bodyHalf, 8, p.Body);
            // Patches.
            FillEllipse(c, bodyX - bodyHalf / 3, bodyY + 3, Mathf.Max(2, bodyHalf / 4), 3, p.Trim);
            if (side > 0.3f) FillEllipse(c, bodyX + bodyHalf / 3, bodyY - 1, 3, 2, p.Trim);
            // Udder (side views), tail.
            if (side > 0.3f) FillEllipse(c, bodyX - 2, bodyY - 8, 3, 2, p.Skin);
            if (side > 0.3f)
            {
                int tailRoot = bodyX - bodyHalf;
                Line(c, new Vector2Int(tailRoot, bodyY + 4), new Vector2Int(tailRoot - 2, bodyY - 6 + pose.Bob), p.Body);
                FillRect(c, tailRoot - 3, bodyY - 8 + pose.Bob, 3, 3, p.Trim);
            }
            else if (back)
            {
                Line(c, new Vector2Int(24, bodyY + 4), new Vector2Int(24 + pose.Bob, bodyY - 6), p.Body);
                FillRect(c, 23 + pose.Bob, bodyY - 8, 3, 3, p.Trim);
            }
            if (!back) DrawCowHead(c, side, headX, headY, back, p);
        }

        static void DrawCowHead(Canvas c, float side, int hx, int hy, bool back, Palette p)
        {
            // Horns and ears either side of the head.
            int spread = Mathf.RoundToInt(6f - 3f * side);
            FillRect(c, hx - spread - 2, hy + 4, 3, 2, p.Weapon);
            FillRect(c, hx + spread, hy + 4, 3, 2, p.Weapon);
            FillRect(c, hx - spread - 4, hy + 1, 3, 2, p.Body);
            FillRect(c, hx + spread + 2, hy + 1, 3, 2, p.Body);
            FillEllipse(c, hx, hy, 5, 6, p.Body);
            FillRect(c, hx - 2, hy + 3, 4, 3, p.Trim); // forelock patch
            if (back) return;
            if (side > 0.3f)
            {
                FillRect(c, hx + 2, hy - 6, 5, 5, p.Skin); // long muzzle forward
                c.Set(hx + 5, hy - 3, p.Outline);
                Dot(c, hx + 1, hy + 1, p.Hair);
            }
            else
            {
                FillRect(c, hx - 3, hy - 7, 7, 5, p.Skin);
                c.Set(hx - 2, hy - 5, p.Outline);
                c.Set(hx + 2, hy - 5, p.Outline);
                Dot(c, hx - 4, hy + 1, p.Hair);
                Dot(c, hx + 3, hy + 1, p.Hair);
            }
        }
    }
}
