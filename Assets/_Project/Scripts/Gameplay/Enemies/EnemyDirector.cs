using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Group tactics: only a few enemies may attack the player at the same time. An enemy needs one of the
    /// attack tokens to close in and swing; the others hold a ring around the player, circling and waiting
    /// their turn. Keeps fights against groups readable (every wind-up can be seen) without making them easy.
    /// </summary>
    public static class EnemyDirector
    {
        /// <summary>How many enemies may be closing in / attacking at once.</summary>
        public const int MaxAttackers = 2;

        static readonly List<EnemyController> attackers = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => attackers.Clear();

        public static int AttackerCount
        {
            get
            {
                attackers.RemoveAll(e => e == null || !e.isActiveAndEnabled);
                return attackers.Count;
            }
        }

        public static bool HasToken(EnemyController enemy) => attackers.Contains(enemy);

        /// <summary>Takes a token if one is free (or already held).</summary>
        public static bool TryTakeToken(EnemyController enemy)
        {
            if (attackers.Contains(enemy)) return true;
            if (AttackerCount >= MaxAttackers) return false;
            attackers.Add(enemy);
            return true;
        }

        public static void ReleaseToken(EnemyController enemy) => attackers.Remove(enemy);
    }
}
