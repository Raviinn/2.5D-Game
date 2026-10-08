using Beast.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 33 setup (music): synthesises four placeholder music loops (title, day, night, combat) into
    /// Audio/Placeholder and adds them to Resources/SoundLibrary. GameAudio plays them by itself.
    /// Safe to re-run: existing WAVs aren't redrawn, and library entries pointing at your own music are kept.
    /// Run the Milestone 20 setup first (it creates the library).
    /// </summary>
    public static class MusicSetup
    {
        [MenuItem("Beast/Setup/Run Milestone 33 Setup (Music)", priority = 26)]
        public static void Run()
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AudioSetup.LibraryPath);
            if (library == null)
            {
                Debug.LogError("[Setup] No SoundLibrary yet. Run the Milestone 20 setup (Sound) first.");
                return;
            }

            int written = 0;
            AudioSetup.Fill(ref library.MusicMenu, AudioSetup.Clip("Music_Menu", MusicSynth.Menu, 0.8f, loop: true, ref written));
            AudioSetup.Fill(ref library.MusicDay, AudioSetup.Clip("Music_Day", MusicSynth.Day, 0.8f, loop: true, ref written));
            AudioSetup.Fill(ref library.MusicNight, AudioSetup.Clip("Music_Night", MusicSynth.Night, 0.8f, loop: true, ref written));
            AudioSetup.Fill(ref library.MusicCombat, AudioSetup.Clip("Music_Combat", MusicSynth.Combat, 0.8f, loop: true, ref written));

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Milestone 33 music complete: {written} track(s) written. Title, day, night and combat music " +
                      "crossfade by themselves; Settings → Audio → Music sets the volume.");
        }
    }
}
