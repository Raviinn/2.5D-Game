using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Cached physics masks for combat queries. Layers are created by the Milestone 2 setup.</summary>
    public static class CombatLayers
    {
        public const string PlayerLayer = "Player";
        public const string EnemyLayer = "Enemy";

        static int? playerMask;
        static int? enemyMask;

        public static int PlayerMask => playerMask ??= GetMask(PlayerLayer);
        public static int EnemyMask => enemyMask ??= GetMask(EnemyLayer);

        public static int OpponentMask(Team team) => team == Team.Player ? EnemyMask : PlayerMask;

        static int GetMask(string layer)
        {
            int mask = LayerMask.GetMask(layer);
            if (mask == 0)
                Debug.LogError($"[Combat] Layer '{layer}' is missing. Run Beast > Setup > Run Milestone 2 Setup (Combat).");
            return mask;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            playerMask = null;
            enemyMask = null;
        }
    }
}
