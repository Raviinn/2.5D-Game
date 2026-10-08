using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Beast.Core;
using Ink.Runtime;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Runs conversations from the compiled master Ink story and bridges Ink ↔ game:
    /// quests, items, gold, Discipline levels, town standing and opening shops are exposed as Ink EXTERNAL functions.
    /// The story's state (variables, visit counts) is saved, so NPCs remember past conversations.
    /// The game is paused (InGameMenu) during conversations; Esc abandons the conversation (menu hotkeys don't).
    /// </summary>
    public sealed class DialogueRunner : MonoBehaviour, ISaveable
    {
        /// <summary>"Oswin: Hello there" → speaker "Oswin", text "Hello there".</summary>
        static readonly Regex SpeakerPrefix = new(@"^([A-Z][\w' ]{0,23}):\s+(.+)$");

        [SerializeField, Tooltip("Main.json, compiled from Main.ink.")] TextAsset storyJson;
        [SerializeField] SpeakerData[] speakers;
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "dialogue.story";

        Story story;
        GameStateService state;
        GameDatabase database;
        QuestLog quests;
        Inventory inventory;
        PlayerProgression progression;
        Reputation reputation;
        PlayerAppearance playerAppearance;
        GearCondition gear;
        DialogueSpeaker currentSpeaker;
        Shopkeeper pendingShop;
        readonly List<string> choices = new();
        readonly Dictionary<string, SpeakerData> speakersByName = new(StringComparer.OrdinalIgnoreCase);

        public string SaveId => saveId;
        public bool IsActive { get; private set; }
        public string CurrentText { get; private set; } = string.Empty;
        /// <summary>Null for narration.</summary>
        public string CurrentSpeakerName { get; private set; }
        public SpeakerData CurrentSpeakerData { get; private set; }
        /// <summary>The NPC being talked to (the dialogue camera frames them); null for a conversation without one.</summary>
        public Transform ConversationPartner => currentSpeaker != null ? currentSpeaker.transform : null;
        public IReadOnlyList<string> Choices => choices;
        /// <summary>Changes whenever a new line is shown (the UI restarts its typewriter effect).</summary>
        public int LineId { get; private set; }

        void Awake()
        {
            if (speakers != null)
                foreach (var speaker in speakers)
                    if (speaker != null) speakersByName[speaker.DisplayName] = speaker;

            if (storyJson == null)
            {
                Debug.LogError("[Dialogue] No compiled story assigned. Run Beast > Setup > Run Milestone 8 Setup (Dialogue & Quests).", this);
                enabled = false;
                return;
            }

            story = new Story(storyJson.text);
            story.onError += (message, type) =>
            {
                if (type == Ink.ErrorType.Error) Debug.LogError($"[Ink] {message}");
                else Debug.LogWarning($"[Ink] {message}");
            };
            BindExternalFunctions();
            Services.Register(this);
        }

        void OnDestroy() => Services.Unregister(this);

        void OnEnable()
        {
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start()
        {
            state = Services.Get<GameStateService>();
            Services.TryGet(out database);
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                quests = player.GetComponent<QuestLog>();
                inventory = player.GetComponent<Inventory>();
                progression = player.GetComponent<PlayerProgression>();
                reputation = player.GetComponent<Reputation>();
                playerAppearance = player.GetComponent<PlayerAppearance>();
                gear = player.GetComponent<GearCondition>();
            }
        }

        // ---------- Conversation flow ----------

        /// <summary>Starts a conversation at an Ink knot (e.g. "oswin").</summary>
        public bool StartConversation(string knot, DialogueSpeaker speaker)
        {
            if (story == null || IsActive || string.IsNullOrEmpty(knot)) return false;

            currentSpeaker = speaker;
            pendingShop = null;
            state.SetState(GameState.InGameMenu);
            IsActive = true;
            state.BlockHotkeyClose = true; // only Esc abandons a conversation
            FaceEachOther(speaker);
            try
            {
                story.ChoosePathString(knot);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Dialogue] Can't start knot '{knot}': {e.Message}");
                End();
                return false;
            }
            Advance();
            return true;
        }

        /// <summary>The player and the NPC turn to each other at once (the game is paused, so they can't turn slowly).</summary>
        static void FaceEachOther(DialogueSpeaker speaker)
        {
            var player = GameObject.FindWithTag("Player");
            if (speaker == null || player == null) return;
            Vector3 between = speaker.transform.position - player.transform.position;
            between.y = 0f;
            if (between.sqrMagnitude < 0.0001f) return;
            if (player.TryGetComponent(out PlayerMotor motor)) motor.SnapRotation(between);
            else player.transform.rotation = Quaternion.LookRotation(between);
            if (speaker.TryGetComponent(out NpcController npc)) npc.FaceNow(-between);
        }

        /// <summary>Shows the next line, or ends the conversation. Ignored while choices are showing.</summary>
        public void Advance()
        {
            if (!IsActive || choices.Count > 0) return;

            while (story.canContinue)
            {
                string line = story.Continue().Trim();
                if (line.Length == 0) continue;
                ShowLine(line, story.currentTags);
                RefreshChoices();
                return;
            }

            RefreshChoices();
            if (choices.Count > 0) return; // choices with no preceding text: keep the last line visible
            End();
        }

        public void Choose(int index)
        {
            if (!IsActive || index < 0 || index >= choices.Count) return;
            choices.Clear();
            story.ChooseChoiceIndex(index);
            Advance();
        }

        void RefreshChoices()
        {
            choices.Clear();
            if (story.canContinue) return;
            foreach (var choice in story.currentChoices) choices.Add(choice.text);
        }

        void ShowLine(string line, List<string> tags)
        {
            string speakerName = null;
            if (tags != null)
                foreach (string tag in tags)
                    if (tag.StartsWith("speaker:", StringComparison.OrdinalIgnoreCase)) speakerName = tag.Substring(8).Trim();

            var match = SpeakerPrefix.Match(line);
            if (match.Success)
            {
                speakerName = match.Groups[1].Value;
                line = match.Groups[2].Value;
            }

            CurrentSpeakerName = speakerName;
            CurrentSpeakerData = speakerName != null && speakersByName.TryGetValue(speakerName, out var data) ? data : null;
            CurrentText = line;
            LineId++;
        }

        void End()
        {
            if (!IsActive) return;
            IsActive = false; // before changing state, so OnGameStateChanged doesn't treat it as an abort
            state.BlockHotkeyClose = false;
            choices.Clear();
            CurrentText = string.Empty;
            currentSpeaker = null;

            var shop = pendingShop;
            pendingShop = null;
            if (state.Current == GameState.InGameMenu) state.SetState(GameState.Playing);
            if (shop != null) shop.Open();
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            // Esc leaves the menu: abandon the conversation. The next conversation restarts at its knot.
            if (!IsActive || evt.Current == GameState.InGameMenu) return;
            IsActive = false;
            state.BlockHotkeyClose = false;
            choices.Clear();
            pendingShop = null;
            currentSpeaker = null;
        }

        // ---------- Ink → game ----------

        void BindExternalFunctions()
        {
            // Queries (safe for Ink to evaluate ahead of time)
            story.BindExternalFunction("quest_state", (string id) => (object)QuestStateName(id), true);
            story.BindExternalFunction("has_item", (string id, int count) => (object)(Item(id) is { } item && inventory != null && inventory.CountOf(item) >= count), true);
            story.BindExternalFunction("gold", () => (object)(inventory != null ? inventory.Gold : 0), true);
            story.BindExternalFunction("discipline_level", (string name) =>
                (object)(progression != null && Enum.TryParse(name, true, out Discipline d) ? progression.Level(d) : 0), true);
            story.BindExternalFunction("standing", () => (object)(reputation != null ? reputation.Standing : 0), true);
            story.BindExternalFunction("standing_tier", () => (object)(reputation != null ? reputation.TierIndex : 0), true);
            story.BindExternalFunction("player_name", () => (object)(playerAppearance != null ? playerAppearance.PlayerName : CharacterAppearance.DefaultName), true);

            // Actions
            story.BindExternalFunction("start_quest", (string id) => (object)(Quest(id) is { } quest && quests != null && quests.StartQuest(quest)));
            story.BindExternalFunction("turn_in_quest", (string id) => (object)(Quest(id) is { } quest && quests != null && quests.TurnIn(quest)));
            story.BindExternalFunction("complete_objective", (string id, int index) =>
            {
                if (Quest(id) is { } quest && quests != null) quests.CompleteObjective(quest, index);
            });
            story.BindExternalFunction("take_item", (string id, int count) =>
                (object)(Item(id) is { } item && inventory != null && inventory.Remove(item, count)));
            story.BindExternalFunction("give_item", (string id, int count) =>
            {
                if (Item(id) is not { } item || inventory == null) return;
                int leftover = inventory.Add(item, count);
                if (leftover > 0) ItemPickup.SpawnItem(inventory.transform.position, item, leftover, 1f); // bag full: drop it
            });
            story.BindExternalFunction("give_gold", (int amount) =>
            {
                if (inventory != null) inventory.AddGold(amount);
            });
            story.BindExternalFunction("heal_player", () =>
            {
                var player = inventory != null ? inventory.gameObject : GameObject.FindWithTag("Player");
                if (player == null) return;
                if (player.TryGetComponent(out Combatant combatant)) combatant.Heal(combatant.MaxHealth);
                if (player.TryGetComponent(out Stamina stamina)) stamina.Refill();
            });
            story.BindExternalFunction("is_night", () => (object)(Services.TryGet(out DayNightCycle cycle) && cycle.IsNight), true);
            story.BindExternalFunction("weather", () => (object)(Services.TryGet(out WeatherSystem w) ? w.Current.ToString() : "Clear"), true);
            story.BindExternalFunction("hurt", () =>
            {
                var c = inventory != null ? inventory.GetComponent<Combatant>() : null;
                return (object)(c != null && c.MaxHealth > 0f && c.Health < c.MaxHealth * 0.5f);
            }, true);
            story.BindExternalFunction("wears", (string id) =>
            {
                var equipment = inventory != null ? inventory.GetComponent<PlayerEquipment>() : null;
                return (object)(equipment != null && Item(id) is EquipmentData gear && equipment.EquippedCount(gear) > 0);
            }, true);
            story.BindExternalFunction("broken_gear", () =>
            {
                var equipment = inventory != null ? inventory.GetComponent<PlayerEquipment>() : null;
                if (equipment == null || gear == null) return (object)false;
                if (gear.IsBroken(equipment.WeaponAt(0)) || gear.IsBroken(equipment.WeaponAt(1))) return (object)true;
                foreach (var slot in PlayerEquipment.ArmorSlotOrder) if (gear.IsBroken(equipment.ArmorAt(slot))) return (object)true;
                return (object)false;
            }, true);
            story.BindExternalFunction("season", () => (object)Calendar.Current.ToString(), true);
            story.BindExternalFunction("festival", () => (object)Calendar.FestivalName(Calendar.FestivalToday), true);
            story.BindExternalFunction("repair_cost", () => (object)(gear != null ? gear.TotalRepairPrice() : 0), true);
            story.BindExternalFunction("repair_gear", () => (object)(gear != null && gear.RepairAllForGold()));
            story.BindExternalFunction("change_standing", (int amount) =>
            {
                if (reputation != null) reputation.Change(amount, StandingSource.Dialogue);
            });
            story.BindExternalFunction("open_shop", () =>
            {
                // Opened when the conversation ends, so the dialogue box and shop never overlap.
                pendingShop = currentSpeaker != null ? currentSpeaker.GetComponent<Shopkeeper>() : null;
            });
        }

        string QuestStateName(string id)
        {
            var quest = Quest(id);
            if (quest == null || quests == null) return "inactive";
            return quests.StatusOf(quest) switch
            {
                QuestStatus.Active => "active",
                QuestStatus.Ready => "ready",
                QuestStatus.Completed => "done",
                _ => "inactive",
            };
        }

        QuestData Quest(string id)
        {
            if (database != null && database.TryGet(id, out QuestData quest)) return quest;
            Debug.LogWarning($"[Dialogue] Ink asked for unknown quest '{id}'.");
            return null;
        }

        ItemData Item(string id)
        {
            if (database != null && database.TryGet(id, out ItemData item)) return item;
            Debug.LogWarning($"[Dialogue] Ink asked for unknown item '{id}'.");
            return null;
        }

        // ---------- Save ----------

        public string CaptureState() => story != null ? story.state.ToJson() : string.Empty;

        public void RestoreState(string json)
        {
            if (story == null || string.IsNullOrEmpty(json)) return;
            if (IsActive) End();
            story.state.LoadJson(json);
        }
    }
}
