using System.Collections.Generic;
using Beast.Core;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>Collects every GameData asset into the GameDatabase and fills missing Ids.</summary>
    public static class GameDatabaseBuilder
    {
        [MenuItem("Beast/Data/Rebuild Game Database")]
        static void RebuildFromMenu()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDatabase");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[GameDatabase] No GameDatabase asset found. Run Beast > Setup > Run Milestone 1 Setup.");
                return;
            }
            foreach (string guid in guids)
                Rebuild(AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        public static void Rebuild(GameDatabase database)
        {
            var entries = new List<GameData>();
            var seen = new Dictionary<string, GameData>();

            foreach (string guid in AssetDatabase.FindAssets("t:GameData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<GameData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;

                var serialized = new SerializedObject(data);
                var idProperty = serialized.FindProperty("id");
                if (string.IsNullOrWhiteSpace(idProperty.stringValue))
                {
                    idProperty.stringValue = data.name.Trim().ToLowerInvariant().Replace(' ', '_');
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if (seen.TryGetValue(data.Id, out var other))
                {
                    Debug.LogError($"[GameDatabase] Duplicate Id '{data.Id}' on '{data.name}' and '{other.name}'. Skipped '{data.name}'.", data);
                    continue;
                }
                seen.Add(data.Id, data);
                entries.Add(data);
            }

            database.EditorSetEntries(entries);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GameDatabase] Rebuilt with {entries.Count} entries.");
        }
    }
}
