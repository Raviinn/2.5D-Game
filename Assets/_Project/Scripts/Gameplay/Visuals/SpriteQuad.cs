using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Shared unit quads for billboarded sprites (characters, crops). One mesh per pivot/normal-bend pair.</summary>
    public static class SpriteQuad
    {
        static readonly Dictionary<(Vector2 pivot, float bend), Mesh> cache = new();

        /// <summary>
        /// Unit quad with the pivot at the origin. Its front (-Z) faces the camera when the transform looks along
        /// the camera's forward. Normals bend upward so sprites stay evenly lit from every angle.
        /// </summary>
        public static Mesh Get(Vector2 pivot, float normalBend)
        {
            var key = (pivot, normalBend);
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            float x0 = -pivot.x, x1 = 1f - pivot.x, y0 = -pivot.y, y1 = 1f - pivot.y;
            var normal = (Vector3.back * (1f - normalBend) + Vector3.up * normalBend).normalized;
            var mesh = new Mesh
            {
                name = "SpriteQuad",
                hideFlags = HideFlags.DontSave,
                vertices = new[] { new Vector3(x0, y0), new Vector3(x1, y0), new Vector3(x0, y1), new Vector3(x1, y1) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                normals = new[] { normal, normal, normal, normal },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
            };
            mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }
    }
}
