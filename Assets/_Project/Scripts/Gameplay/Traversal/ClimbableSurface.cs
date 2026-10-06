using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Marks a wall the player can climb freely (up, down, sideways), not just hang from its top edge.
    /// Put it on the collider's object or any parent. Make these walls look different (ivy, a ladder) so players can tell.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClimbableSurface : MonoBehaviour
    {
    }
}
