using Beast.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 51 setup (name plates &amp; HUD polish): sets the name-plate height of the wolves (low) and the
    /// Blighted Brute (tall). The rest needs no setup: plates now hide behind walls, a lock-on bar shows your target
    /// at the top of the screen, and hits on you show which way they came from. Safe to re-run.
    /// </summary>
    public static class HudPolishSetup
    {
        [MenuItem("Beast/Setup/Run Milestone 51 Setup (Name Plates & HUD)", priority = 43)]
        public static void Run()
        {
            int set = 0;
            set += Plate(EnemyTypesSetup.WolfDataPath, 1.3f);
            set += Plate(EnemyTypesSetup.BruteDataPath, 3.1f);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Milestone 51 name plates & HUD setup complete: plate height set on {set} enemy type(s). " +
                      "Plates hide behind walls; lock on (middle mouse) for the target bar; hits show their direction.");
        }

        static int Plate(string path, float height)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null || !Mathf.Approximately(data.PlateHeight, 2.2f)) return 0;
            data.PlateHeight = height;
            EditorUtility.SetDirty(data);
            return 1;
        }
    }
}
