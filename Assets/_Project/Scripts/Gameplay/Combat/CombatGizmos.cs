using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Draws attack hitboxes (enable Gizmos in the Game view to see them while playing).</summary>
    public static class CombatGizmos
    {
        static readonly Color StartupColor = new(1f, 0.85f, 0.2f, 0.25f);
        static readonly Color ActiveColor = new(1f, 0.15f, 0.15f, 0.6f);

        public static void DrawHitbox(AttackExecutor attacks)
        {
            if (attacks.CurrentPhase is not (AttackExecutor.Phase.Startup or AttackExecutor.Phase.Active)) return;
            if (!attacks.TryGetHitbox(out var center, out var rotation, out var size)) return;

            Gizmos.color = attacks.CurrentPhase == AttackExecutor.Phase.Active ? ActiveColor : StartupColor;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
