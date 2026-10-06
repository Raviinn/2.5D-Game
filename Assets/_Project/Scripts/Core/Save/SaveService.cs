using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Beast.Core
{
    [Serializable]
    public sealed class SaveFile
    {
        public int version;
        public string sceneName;
        public string savedAtUtc;
        public SaveSummary summary = new();
        public List<SaveEntry> entries = new();
    }

    [Serializable]
    public sealed class SaveEntry
    {
        public string id;
        public string json;
    }

    /// <summary>
    /// What the slot list shows without loading the game: day and time, play time and a few short lines
    /// ("120 gold", "Combat 4 · Farming 2"...) from any saveable that is also an ISaveSummarySource.
    /// </summary>
    [Serializable]
    public sealed class SaveSummary
    {
        /// <summary>0 = unknown (a save from before slots had summaries).</summary>
        public int day;
        public int hour;
        public int minute;
        public double playSeconds;
        public List<string> details = new();
    }

    /// <summary>A saveable that also adds a line (or the day and time) to its slot's summary.</summary>
    public interface ISaveSummarySource
    {
        void Describe(SaveSummary summary);
    }

    /// <summary>What's in a slot, read from disk without loading it.</summary>
    public readonly struct SlotInfo
    {
        public readonly int Slot;
        public readonly bool Exists;
        /// <summary>False when the file exists but can't be read (damaged, or from a newer version of the game).</summary>
        public readonly bool Readable;
        public readonly SaveSummary Summary;
        public readonly DateTime SavedAt;

        public SlotInfo(int slot, bool exists, bool readable, SaveSummary summary, DateTime savedAt)
        {
            Slot = slot;
            Exists = exists;
            Readable = readable;
            Summary = summary;
            SavedAt = savedAt;
        }

        public bool IsEmpty => !Exists;
    }

    /// <summary>
    /// Collects state from every registered ISaveable into one JSON file per slot.
    /// There are SlotCount slots, each its own playthrough: the active slot is the one sleeping, the pause menu
    /// and F5/F9 save to and load from. Writes are atomic (temp file + replace) and keep a .bak of the previous save.
    /// Loading into another scene restores objects as they register during that scene's load.
    /// </summary>
    public sealed class SaveService : MonoBehaviour
    {
        /// <summary>Bump when the save format changes, and add a step in Migrate().</summary>
        public const int CurrentVersion = 2;
        public const int SlotCount = 3;
        const string SaveFolder = "saves";

        readonly List<ISaveable> saveables = new();
        readonly Dictionary<string, string> pendingRestore = new(StringComparer.Ordinal);

        GameStateService state;
        SceneLoader sceneLoader;
        bool busy;
        double playSeconds;

        /// <summary>The playthrough in progress: sleeping, the pause menu and F5/F9 use this slot.</summary>
        public int ActiveSlot { get; private set; }
        /// <summary>True while a load or new game is switching scenes.</summary>
        public bool IsBusy => busy;
        /// <summary>Real seconds played in the active slot (menus included, the main menu and loading not).</summary>
        public double PlaySeconds => playSeconds;

        public void Initialize(GameStateService state, SceneLoader sceneLoader)
        {
            this.state = state;
            this.sceneLoader = sceneLoader;
        }

        void Update()
        {
            if (state != null && state.Current is GameState.Playing or GameState.Paused or GameState.InGameMenu or GameState.Cutscene)
                playSeconds += Time.unscaledDeltaTime;
        }

        public void Register(ISaveable saveable)
        {
            if (saveables.Contains(saveable)) return;

            foreach (var existing in saveables)
            {
                if (existing.SaveId == saveable.SaveId)
                    Debug.LogWarning($"[Save] Duplicate SaveId '{saveable.SaveId}'. Only one will load correctly.");
            }

            saveables.Add(saveable);
            TryRestore(saveable);
        }

        public void Unregister(ISaveable saveable) => saveables.Remove(saveable);

        public static string GetSlotPath(int slot) =>
            Path.Combine(Application.persistentDataPath, SaveFolder, $"slot_{slot}.json");

        public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        public bool HasSave(int slot) => File.Exists(GetSlotPath(slot));

        /// <summary>When the slot was last written (local time), or null if it's empty.</summary>
        public DateTime? SavedAt(int slot)
        {
            string path = GetSlotPath(slot);
            return File.Exists(path) ? File.GetLastWriteTime(path) : null;
        }

        /// <summary>Reads a slot's summary from disk (for the slot list). Never throws.</summary>
        public SlotInfo ReadSlot(int slot)
        {
            string path = GetSlotPath(slot);
            if (!File.Exists(path)) return new SlotInfo(slot, false, false, null, default);

            DateTime savedAt = File.GetLastWriteTime(path);
            var file = TryReadFile(path);
            bool readable = file != null && file.version <= CurrentVersion;
            return new SlotInfo(slot, true, readable, readable ? file.summary ?? new SaveSummary() : null, savedAt);
        }

        /// <summary>The slot written most recently (what "Continue" loads), or -1 when every slot is empty.</summary>
        public int MostRecentSlot()
        {
            int best = -1;
            DateTime bestTime = DateTime.MinValue;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                var info = ReadSlot(slot);
                if (!info.Exists || !info.Readable || info.SavedAt <= bestTime) continue;
                best = slot;
                bestTime = info.SavedAt;
            }
            return best;
        }

        /// <summary>Deletes a slot's save and its backup.</summary>
        public bool DeleteSlot(int slot)
        {
            string path = GetSlotPath(slot);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            Debug.Log($"[Save] Slot {slot} deleted.");
            return true;
        }

        /// <summary>Saves the playthrough in progress to its own slot.</summary>
        public bool Save() => Save(ActiveSlot);

        public bool Save(int slot)
        {
            if (busy || state.Current is not (GameState.Playing or GameState.Paused))
            {
                Debug.LogWarning($"[Save] Can't save right now (state: {state.Current}).");
                return false;
            }
            if (!IsValidSlot(slot))
            {
                Debug.LogError($"[Save] There is no slot {slot}.");
                return false;
            }

            var file = new SaveFile
            {
                version = CurrentVersion,
                sceneName = SceneManager.GetActiveScene().name,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
            };
            file.summary.playSeconds = playSeconds;
            foreach (var saveable in saveables)
            {
                file.entries.Add(new SaveEntry { id = saveable.SaveId, json = saveable.CaptureState() });
                if (saveable is ISaveSummarySource source) source.Describe(file.summary);
            }

            string path = GetSlotPath(slot);
            string tempPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(tempPath, JsonUtility.ToJson(file, prettyPrint: Application.isEditor));
                if (File.Exists(path)) File.Replace(tempPath, path, path + ".bak");
                else File.Move(tempPath, path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }

            ActiveSlot = slot;
            Debug.Log($"[Save] Slot {slot} saved ({file.entries.Count} entries) → {path}");
            EventBus<GameSavedEvent>.Raise(new GameSavedEvent(slot));
            return true;
        }

        /// <summary>Fire-and-forget: reloads the active slot's last save.</summary>
        public void Load() => Load(ActiveSlot);

        /// <summary>Fire-and-forget version for buttons and hotkeys.</summary>
        public async void Load(int slot)
        {
            try { await LoadAsync(slot); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public async Awaitable<bool> LoadAsync(int slot)
        {
            // Not mid-conversation or with a menu open: restoring would yank state out from under them.
            if (busy || state.Current is not (GameState.Playing or GameState.Paused or GameState.MainMenu))
            {
                Debug.LogWarning($"[Save] Can't load right now (state: {state.Current}).");
                return false;
            }

            string path = GetSlotPath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Save] No save in slot {slot}.");
                return false;
            }

            var file = TryReadFile(path);
            if (file == null || file.version > CurrentVersion)
            {
                Debug.LogError($"[Save] Slot {slot} is unreadable or from a newer version ({file?.version}).");
                return false;
            }
            Migrate(file);

            busy = true;
            try
            {
                pendingRestore.Clear();
                foreach (var entry in file.entries)
                    pendingRestore[entry.id] = entry.json;

                if (file.sceneName != SceneManager.GetActiveScene().name)
                {
                    // Saveables in the new scene restore themselves as they register.
                    if (!await sceneLoader.LoadAsync(file.sceneName)) return false;
                }

                for (int i = 0; i < saveables.Count; i++)
                    TryRestore(saveables[i]);

                ActiveSlot = slot;
                playSeconds = file.summary != null ? file.summary.playSeconds : 0;
            }
            finally
            {
                pendingRestore.Clear();
                busy = false;
            }

            Debug.Log($"[Save] Slot {slot} loaded.");
            EventBus<GameLoadedEvent>.Raise(new GameLoadedEvent(slot));
            return true;
        }

        /// <summary>Fire-and-forget version for buttons.</summary>
        public async void NewGame(int slot, string sceneName)
        {
            try { await NewGameAsync(slot, sceneName); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>
        /// Starts a fresh playthrough in a slot: persistent services reset (NewGameEvent), the world scene loads
        /// from scratch, and the slot is saved straight away so it's claimed (overwriting what was there).
        /// </summary>
        public async Awaitable<bool> NewGameAsync(int slot, string sceneName)
        {
            if (busy || state.Current is not (GameState.MainMenu or GameState.Playing or GameState.Paused))
            {
                Debug.LogWarning($"[Save] Can't start a new game right now (state: {state.Current}).");
                return false;
            }
            if (!IsValidSlot(slot))
            {
                Debug.LogError($"[Save] There is no slot {slot}.");
                return false;
            }

            busy = true;
            try
            {
                pendingRestore.Clear();
                ActiveSlot = slot;
                playSeconds = 0;
                EventBus<NewGameEvent>.Raise(new NewGameEvent(slot));
                if (!await sceneLoader.LoadAsync(sceneName)) return false;
                await Awaitable.NextFrameAsync(); // let the new scene's Start() run before the first save
            }
            finally
            {
                busy = false;
            }

            Debug.Log($"[Save] New game in slot {slot}.");
            return Save(slot);
        }

        static SaveFile TryReadFile(string path)
        {
            try { return JsonUtility.FromJson<SaveFile>(File.ReadAllText(path)); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Couldn't read {path}: {e.Message}");
                return null;
            }
        }

        void TryRestore(ISaveable saveable)
        {
            if (pendingRestore.Remove(saveable.SaveId, out var json))
                saveable.RestoreState(json);
        }

        static void Migrate(SaveFile file)
        {
            // v1 → v2: slot summaries were added. Old saves load fine; their summary stays "unknown" until saved again.
            file.summary ??= new SaveSummary();
            file.version = CurrentVersion;
        }
    }
}
