using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Stops ledge grabs and climbing on this object and its children: houses whose roofs have no collider,
    /// set dressing, anything players shouldn't stand on top of.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NotClimbable : MonoBehaviour
    {
    }
}
