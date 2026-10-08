using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A night-only enemy (Milestone 40: the Blighted Brute): abroad from dusk to dawn, gone by day. While away it's
    /// hidden, can't be hit or targeted and doesn't count as an enemy (minimap, music, quests). It comes back each
    /// night at home, healed, even if it was killed the night before.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public sealed class NightPresence : MonoBehaviour
    {
        EnemyController enemy;
        CharacterController body;
        DayNightCycle dayNight;
        bool present = true;

        /// <summary>True while it's out (night).</summary>
        public bool Present => present;

        void Awake()
        {
            enemy = GetComponent<EnemyController>();
            body = GetComponent<CharacterController>();
        }

        void Start() => Refresh(force: true);

        void Update() => Refresh(force: false);

        void Refresh(bool force)
        {
            if (dayNight == null) Services.TryGet(out dayNight);
            bool night = dayNight != null && dayNight.IsNight;
            if (night == present && !force) return;
            present = night;
            if (present)
            {
                enemy.enabled = true;
                enemy.RespawnNow();
            }
            else
            {
                enemy.SetVisible(false);
                body.enabled = false;
                enemy.enabled = false; // leaves EnemyController.Active: no marker, no combat music, no quest target
            }
        }
    }
}
