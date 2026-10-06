using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beast.Core
{
    /// <summary>
    /// Id → GameData lookup. The entry list is filled in the editor by
    /// Beast > Data > Rebuild Game Database; don't edit it by hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Game Database", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        [SerializeField] List<GameData> entries = new();

        Dictionary<string, GameData> lookup;

        public IReadOnlyList<GameData> All => entries;

        public void Initialize()
        {
            lookup = new Dictionary<string, GameData>(entries.Count, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (!lookup.TryAdd(entry.Id, entry))
                    Debug.LogError($"[GameDatabase] Duplicate Id '{entry.Id}' ({entry.name}).", entry);
            }
        }

        public bool TryGet<T>(string id, out T data) where T : GameData
        {
            if (lookup == null) Initialize();
            if (id != null && lookup.TryGetValue(id, out var found) && found is T typed)
            {
                data = typed;
                return true;
            }
            data = null;
            return false;
        }

        public T Get<T>(string id) where T : GameData
        {
            if (TryGet(id, out T data)) return data;
            Debug.LogError($"[GameDatabase] No {typeof(T).Name} with Id '{id}'.");
            return null;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(List<GameData> newEntries)
        {
            entries = newEntries;
            lookup = null;
        }
#endif
    }
}
