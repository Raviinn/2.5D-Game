using UnityEngine;

namespace Beast.Core
{
    /// <summary>
    /// Base class for all content assets (items, classes, enemies, crops, factions...).
    /// Saves reference content by Id, never by asset reference.
    /// Empty Ids are filled from the asset name by Beast > Data > Rebuild Game Database.
    /// </summary>
    public abstract class GameData : ScriptableObject
    {
        [SerializeField, Tooltip("Unique, stable ID. Don't change it once saves exist.")]
        string id;

        public string Id => id;
    }
}
