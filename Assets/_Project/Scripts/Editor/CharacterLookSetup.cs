using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 19 setup (character look): adds PlayerAppearance to the player in World_Test (the character
    /// creator's look, and the sprite redrawn for sword &amp; shield or greatsword), and marks each combat style
    /// with the weapon it shows. Safe to re-run.
    /// </summary>
    public static class CharacterLookSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";

        [MenuItem("Beast/Setup/Run Milestone 19 Setup (Character Look)", priority = 18)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test scene not found. Run the earlier setups first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No Player in World_Test.");
                return;
            }
            if (!player.TryGetComponent(out PlayerAppearance _)) player.AddComponent<PlayerAppearance>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Loaded only now: opening a scene unloads assets nothing references.
            int marked = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:MovesetData"))
            {
                var moveset = AssetDatabase.LoadAssetAtPath<MovesetData>(AssetDatabase.GUIDToAssetPath(guid));
                if (moveset == null) continue;
                var look = moveset.ResolvedLook; // a default look on a "Greatsword" style becomes Greatsword
                if (moveset.Look == look) continue;
                moveset.Look = look;
                EditorUtility.SetDirty(moveset);
                marked++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Milestone 19 character look complete. Player has PlayerAppearance; {marked} combat style(s) marked with their weapon look.");
        }
    }
}
