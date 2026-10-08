using UnityEngine;

namespace Beast.Gameplay
{
    public static partial class CharacterSpriteBuilder
    {
        /// <summary>
        /// A wolf on four legs, in the same grid as people (same clips and rows), so it animates with the same code.
        /// Palette use: Body = fur, Trim = darker fur (back, legs, ears), Skin = pale muzzle and chest, Hair = eyes,
        /// Weapon = teeth. Rows: front, front-right, right, back-right, back (left is mirrored), facing screen-right.
        /// The attack is a crouch (wind-up), then a lunge with the jaws open (the strike frame).
        /// </summary>
        static void DrawWolfCell(Canvas c, int row, Pose pose, Palette p)
        {
            if (pose.Lying)
            {
                // On its side, legs out toward the viewer.
                FillEllipse(c, 22, 7, 12, 4, p.Body);
                FillEllipse(c, 22, 9, 10, 2, p.Trim);
                FillCircle(c, 36, 7, 4, p.Body);
                FillRect(c, 39, 5, 5, 3, p.Skin);
                FillRect(c, 34, 11, 2, 3, p.Trim);
                for (int i = 0; i < 4; i++) FillRect(c, 14 + i * 5, 2, 2, 3, p.Trim);
                Line(c, new Vector2Int(10, 7), new Vector2Int(3, 5), p.Trim);
                return;
            }

            float side = row switch { 1 => 0.6f, 2 => 1f, 3 => 0.6f, _ => 0f };
            bool back = row >= 3;
            bool striking = pose.Trail;
            bool windingUp = pose.WeaponAngle > 100f; // attack frames 0 and 1: crouched, ready to spring
            int lean = pose.Lean;                     // the lunge pushes the whole wolf forward
            int bob = pose.Bob + (windingUp ? 2 : 0);

            int bodyHalf = Mathf.RoundToInt(7f + 6f * side);
            int bodyX = 24 - Mathf.RoundToInt(3f * side) + Mathf.RoundToInt(lean * side);
            int bodyY = 13 - bob;
            int headX = 24 + Mathf.RoundToInt(13f * side) + Mathf.RoundToInt(lean * Mathf.Max(side, 0.3f));
            int headY = 20 - bob - (windingUp ? 2 : 0) + (striking ? -1 : 0) + (back ? 1 : 0);
            int headR = side > 0.9f ? 5 : 6;

            // Legs (far pair first, a shade darker), then the near pair over the body's edge.
            float swing = pose.LegSwing;
            int stride = Mathf.RoundToInt(swing * 3f * Mathf.Max(side, 0.35f));
            int legTop = bodyY - 2;
            if (side > 0.3f)
            {
                int frontX = bodyX + bodyHalf - 5, rearX = bodyX - bodyHalf + 3;
                Leg(c, frontX + 2 - stride, legTop, swing < 0f, p.Trim);
                Leg(c, rearX + 2 + stride, legTop, swing > 0f, p.Trim);
                Leg(c, frontX + stride, legTop, swing > 0f, p.Body);
                Leg(c, rearX - stride, legTop, swing < 0f, p.Body);
            }
            else
            {
                int spread = back ? 4 : 5;
                Leg(c, 24 - spread - 1 + stride, legTop, swing > 0f, back ? p.Trim : p.Body);
                Leg(c, 24 + spread - 1 - stride, legTop, swing < 0f, back ? p.Trim : p.Body);
            }

            // Tail: hidden behind the body from the front, sweeping back from the side, hanging down from behind.
            int wag = pose.Bob;

            // From behind the head is beyond the body: draw it first.
            if (back) DrawWolfHead(c, row, side, headX, headY, headR, striking, back, p);

            FillEllipse(c, bodyX, bodyY, bodyHalf, 6, p.Body);
            FillEllipse(c, bodyX - Mathf.RoundToInt(side), bodyY + 3, Mathf.Max(3, bodyHalf - 2), 2, p.Trim); // darker back
            if (!back && side < 0.9f) FillEllipse(c, headX - Mathf.RoundToInt(2f * side), bodyY + 1, 4, 4, p.Skin); // pale chest

            if (side > 0.3f)
            {
                int tailRoot = bodyX - bodyHalf + 1;
                Line(c, new Vector2Int(tailRoot, bodyY + 2), new Vector2Int(tailRoot - 6, bodyY + 5 + wag), p.Trim);
                Line(c, new Vector2Int(tailRoot - 6, bodyY + 5 + wag), new Vector2Int(tailRoot - 9, bodyY + 3 + wag), p.Body);
            }
            else if (back)
            {
                Line(c, new Vector2Int(24, bodyY + 3), new Vector2Int(24 + wag, bodyY - 5), p.Trim);
            }

            if (!back) DrawWolfHead(c, row, side, headX, headY, headR, striking, back, p);

            if (pose.MotionLines)
                for (int i = 0; i < 3; i++)
                    FillRect(c, bodyX - bodyHalf - 10, bodyY - 2 + i * 4, 6 - i * 2, 1, TrailColor);
        }

        static void DrawWolfHead(Canvas c, int row, float side, int hx, int hy, int r, bool open, bool back, Palette p)
        {
            // Neck joining head and shoulders.
            FillEllipse(c, hx - Mathf.RoundToInt(3f * side), hy - 4, 4, 4, p.Body);
            // Ears.
            if (side > 0.9f)
            {
                FillRect(c, hx - 3, hy + r - 1, 3, 4, p.Trim);
                FillRect(c, hx, hy + r - 2, 2, 3, p.Body);
            }
            else
            {
                int gap = Mathf.RoundToInt(4f - 2f * side);
                FillRect(c, hx - gap - 3, hy + r - 2, 3, 5, p.Trim);
                FillRect(c, hx + gap, hy + r - 2, 3, 5, p.Trim);
            }
            FillCircle(c, hx, hy, r, p.Body);
            if (back) return; // the back of the head: fur and ears only

            if (side < 0.3f)
            {
                // Facing the viewer: muzzle below the eyes, nose on top of it.
                FillRect(c, hx - 3, hy - 5, 6, 5, p.Skin);
                FillRect(c, hx - 1, hy - 2, 2, 2, p.Outline);
                Dot(c, hx - 4, hy + 1, p.Hair);
                Dot(c, hx + 2, hy + 1, p.Hair);
                if (open)
                {
                    FillRect(c, hx - 3, hy - 7, 6, 2, p.Outline);
                    FillRect(c, hx - 3, hy - 6, 1, 1, p.Weapon);
                    FillRect(c, hx + 2, hy - 6, 1, 1, p.Weapon);
                }
                return;
            }

            // Profile and three-quarter: a long muzzle toward screen-right.
            int snout = Mathf.RoundToInt(3f + 4f * side);
            int jaw = open ? 3 : 0;
            FillRect(c, hx + r - 2, hy - 3, snout, 4, p.Skin);                 // upper jaw
            FillRect(c, hx + r - 2 + snout - 1, hy - 1, 2, 2, p.Outline);       // nose
            FillRect(c, hx + r - 3, hy - 5 - jaw, snout - 1, 2, p.Skin);         // lower jaw (drops when biting)
            if (open)
            {
                FillRect(c, hx + r - 2, hy - 4 - jaw + 1, snout - 1, jaw, p.Outline);
                for (int x = hx + r - 1; x < hx + r - 2 + snout - 1; x += 2) c.Set(x, hy - 4, p.Weapon);
            }
            Dot(c, hx + 1 + Mathf.RoundToInt(side), hy + 1, p.Hair);
            if (row == 1) Dot(c, hx - 3, hy + 1, p.Hair); // three-quarter: the far eye too
        }

        /// <summary>A leg from the body down to the ground (y 2); 'lifted' raises the paw mid-stride.</summary>
        static void Leg(Canvas c, int x, int top, bool lifted, Color32 color)
        {
            int foot = lifted ? 4 : 2;
            FillRect(c, x, foot, 3, Mathf.Max(1, top - foot + 1), color);
            FillRect(c, x - 1 + (lifted ? 1 : 0), foot, 4, 1, color); // paw
        }

        static void FillEllipse(Canvas c, int cx, int cy, int rx, int ry, Color32 color)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    float nx = x / (rx + 0.5f), ny = y / (ry + 0.5f);
                    if (nx * nx + ny * ny <= 1f) c.Set(cx + x, cy + y, color);
                }
        }
    }
}
