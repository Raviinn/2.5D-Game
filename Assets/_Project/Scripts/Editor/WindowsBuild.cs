using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 23: one-click Windows builds into Builds/Windows (ignored by git), and the leftover template scene
    /// removed. The build uses the scenes ticked in Build Settings (Bootstrap, MainMenu, World_Test).
    /// </summary>
    public static class WindowsBuild
    {
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Beast/Build/Windows (Development)", priority = 300)]
        static void Development() => Build(development: true);

        [MenuItem("Beast/Build/Windows (Release)", priority = 301)]
        static void Release() => Build(development: false);

        /// <summary>For command-line builds: -executeMethod Beast.EditorTools.WindowsBuild.ReleaseFromCommandLine</summary>
        public static void ReleaseFromCommandLine()
        {
            bool ok = Build(development: false);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Builds Builds/Windows[ (Dev)]/Beast.exe and opens the folder. Returns true on success.</summary>
        public static bool Build(bool development)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] No scenes ticked in Build Settings. Run the Milestone 16 setup (it adds Bootstrap, MainMenu and World_Test).");
                return false;
            }
            string folder = Path.GetFullPath(development ? "Builds/Windows (Dev)" : "Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, $"{PlayerSettings.productName}.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            });
            var summary = report.summary;
            bool ok = summary.result == BuildResult.Succeeded;
            Debug.Log($"[Build] Windows {(development ? "development" : "release")} build {summary.result}: {summary.totalErrors} error(s), " +
                      $"{summary.totalSize / (1024f * 1024f):0} MB, {summary.totalTime.TotalSeconds:0} s → {folder}");
            if (ok && !Application.isBatchMode) EditorUtility.RevealInFinder(Path.Combine(folder, $"{PlayerSettings.productName}.exe"));
            return ok;
        }

        [MenuItem("Beast/Setup/Run Milestone 23 Setup (Cleanup)", priority = 19)]
        public static void Cleanup()
        {
            var list = EditorBuildSettings.scenes.ToList();
            int removed = list.RemoveAll(s => s.path == SampleScenePath);
            if (removed > 0) EditorBuildSettings.scenes = list.ToArray();
            bool deleted = File.Exists(SampleScenePath) && AssetDatabase.DeleteAsset(SampleScenePath);
            if (AssetDatabase.IsValidFolder("Assets/Scenes") && AssetDatabase.FindAssets("", new[] { "Assets/Scenes" }).Length == 0)
                AssetDatabase.DeleteAsset("Assets/Scenes");
            Debug.Log($"[Setup] Milestone 23 cleanup complete: SampleScene {(deleted ? "deleted" : "already gone")}" +
                      $"{(removed > 0 ? " and taken out of Build Settings" : "")}. Build with Beast → Build → Windows. " +
                      "Characters now show as sprites in the Scene view (Beast → Show Character Sprites in Edit Mode toggles it).");
        }
    }
}
