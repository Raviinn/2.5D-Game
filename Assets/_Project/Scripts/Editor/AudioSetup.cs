using System;
using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 20 setup (sound): synthesises the placeholder sounds into Audio/Placeholder and fills
    /// Resources/SoundLibrary with them. GameAudio finds the library by itself, so no scene changes are needed.
    /// Safe to re-run: existing WAVs aren't redrawn, and library entries you've pointed at your own clips are kept.
    /// </summary>
    public static class AudioSetup
    {
        const string Root = "Assets/_Project";
        const string Folder = Root + "/Audio/Placeholder";
        const string LibraryPath = Root + "/Resources/SoundLibrary.asset";

        [MenuItem("Beast/Setup/Run Milestone 20 Setup (Sound)", priority = 19)]
        public static void Run()
        {
            EnsureFolder(Folder);
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            int written = 0;
            AudioClip[] Clips(string name, int variants, Func<int, float[]> make, float peak = 0.9f)
            {
                var clips = new AudioClip[variants];
                for (int i = 0; i < variants; i++)
                    clips[i] = Clip(variants == 1 ? name : $"{name}_{i + 1}", () => make(i), peak, loop: false, ref written);
                return clips;
            }
            AudioClip Loop(string name, Func<float[]> make) => Clip(name, make, 0.8f, loop: true, ref written);

            Fill(ref library.Swing, Clips("Swing", 3, SoundSynth.Swing));
            Fill(ref library.HeavySwing, Clips("HeavySwing", 2, SoundSynth.HeavySwing));
            Fill(ref library.Hit, Clips("Hit", 3, SoundSynth.Hit));
            Fill(ref library.Block, Clips("Block", 2, SoundSynth.Block));
            Fill(ref library.Parry, Clips("Parry", 1, _ => SoundSynth.Parry()));
            Fill(ref library.Death, Clips("Death", 1, _ => SoundSynth.Death()));
            Fill(ref library.Dodge, Clips("Dodge", 1, _ => SoundSynth.Dodge()));
            Fill(ref library.Footstep, Clips("Footstep", 4, SoundSynth.Footstep, 0.8f));
            Fill(ref library.Climb, Clips("Climb", 2, SoundSynth.Climb, 0.7f));
            Fill(ref library.Pickup, Clips("Pickup", 1, _ => SoundSynth.Pickup(), 0.7f));
            Fill(ref library.Coins, Clips("Coins", 1, _ => SoundSynth.Coins(), 0.7f));
            Fill(ref library.Eat, Clips("Eat", 1, _ => SoundSynth.Eat()));
            Fill(ref library.Drink, Clips("Drink", 1, _ => SoundSynth.Drink()));
            Fill(ref library.Till, Clips("Till", 1, _ => SoundSynth.Till()));
            Fill(ref library.Plant, Clips("Plant", 1, _ => SoundSynth.Plant()));
            Fill(ref library.Water, Clips("Water", 1, _ => SoundSynth.Water(), 0.7f));
            Fill(ref library.Harvest, Clips("Harvest", 1, _ => SoundSynth.Harvest()));
            Fill(ref library.QuestAccepted, Clips("QuestAccepted", 1, _ => SoundSynth.QuestAccepted(), 0.7f));
            Fill(ref library.QuestReady, Clips("QuestReady", 1, _ => SoundSynth.QuestReady(), 0.7f));
            Fill(ref library.QuestComplete, Clips("QuestComplete", 1, _ => SoundSynth.QuestComplete(), 0.75f));
            Fill(ref library.LevelUp, Clips("LevelUp", 1, _ => SoundSynth.LevelUp(), 0.8f));
            Fill(ref library.Sleep, Clips("Sleep", 1, _ => SoundSynth.Sleep(), 0.6f));
            Fill(ref library.UiClick, Clips("UiClick", 1, _ => SoundSynth.UiClick(), 0.6f));
            Fill(ref library.AmbienceDay, Loop("Ambience_Day", SoundSynth.AmbienceDay));
            Fill(ref library.AmbienceNight, Loop("Ambience_Night", SoundSynth.AmbienceNight));
            Fill(ref library.AmbienceRain, Loop("Ambience_Rain", SoundSynth.AmbienceRain));
            Fill(ref library.AmbienceMenu, Loop("Ambience_Menu", SoundSynth.AmbienceMenu));

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Milestone 20 sound complete: {written} placeholder sound(s) written to {Folder}; " +
                      "SoundLibrary is in Resources. Volumes: Settings → Audio.");
        }

        static AudioClip Clip(string name, Func<float[]> make, float peak, bool loop, ref int written)
        {
            string path = $"{Folder}/{name}.wav";
            if (!File.Exists(path))
            {
                SoundSynth.WriteWav(path, make(), peak, fadeEnds: !loop);
                AssetDatabase.ImportAsset(path);
                written++;
            }

            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>Uses the placeholders unless the entry already holds clips of your own.</summary>
        static void Fill(ref AudioClip[] entry, AudioClip[] placeholders)
        {
            if (entry != null && entry.Length > 0 && Array.Exists(entry, c => c != null && !IsPlaceholder(c))) return;
            entry = placeholders;
        }

        static void Fill(ref AudioClip entry, AudioClip placeholder)
        {
            if (entry != null && !IsPlaceholder(entry)) return;
            entry = placeholder;
        }

        static bool IsPlaceholder(AudioClip clip) => AssetDatabase.GetAssetPath(clip).StartsWith(Folder + "/");

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
