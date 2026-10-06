using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Unlocked skills, the two equipped active-skill slots (E / Q), cooldowns and timed buffs.
    /// Passives and active buffs feed PlayerStats.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat), typeof(PlayerProgression))]
    public sealed class PlayerSkills : MonoBehaviour, ISaveable, IStatSource
    {
        public const int SlotCount = 2;

        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "player.skills";

        readonly HashSet<SkillData> unlocked = new();
        readonly SkillData[] slots = new SkillData[SlotCount];
        readonly float[] cooldownEnds = new float[SlotCount];
        readonly List<(SkillData skill, float endTime)> buffs = new();

        PlayerCombat combat;
        PlayerProgression progression;
        PlayerStats stats;
        InputAction[] slotActions;

        [Serializable]
        sealed class State
        {
            public string[] unlocked;
            public string[] slots;
        }

        public string SaveId => saveId;
        public SkillData SlotSkill(int slot) => slots[slot];
        public bool IsUnlocked(SkillData skill) => unlocked.Contains(skill);
        public float CooldownRemaining(int slot) => Mathf.Max(0f, cooldownEnds[slot] - Time.time);

        void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            progression = GetComponent<PlayerProgression>();
            stats = GetComponent<PlayerStats>();
        }

        void OnEnable()
        {
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start()
        {
            var input = Services.Get<InputService>();
            slotActions = new[] { input.Skill1, input.Skill2 };
        }

        void Update()
        {
            for (int i = 0; i < SlotCount; i++)
                if (slotActions[i].WasPressedThisFrame()) TryActivate(i);

            // Expire buffs.
            bool expired = false;
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (Time.time < buffs[i].endTime) continue;
                buffs.RemoveAt(i);
                expired = true;
            }
            if (expired) MarkStatsDirty();
        }

        // ---------- Unlocking ----------

        /// <summary>Null if the skill can be unlocked now; otherwise the reason it can't.</summary>
        public string WhyLocked(SkillData skill)
        {
            if (unlocked.Contains(skill)) return "Unlocked";
            if (skill.Class != null && skill.Class != combat.Class) return $"{skill.Class.DisplayName} only";
            if (progression.Level(skill.Discipline) < skill.RequiredLevel) return $"Requires {skill.Discipline} level {skill.RequiredLevel}";
            if (skill.Prerequisites != null)
                foreach (var required in skill.Prerequisites)
                    if (required != null && !unlocked.Contains(required)) return $"Requires {required.DisplayName}";
            if (progression.Points(skill.Discipline) < skill.Cost) return $"Needs {skill.Cost} {skill.Discipline} point(s)";
            return null;
        }

        public bool Unlock(SkillData skill)
        {
            if (WhyLocked(skill) != null || !progression.SpendPoints(skill.Discipline, skill.Cost)) return false;

            unlocked.Add(skill);
            if (skill.Kind == SkillKind.Active)
            {
                int empty = Array.IndexOf(slots, null);
                if (empty >= 0) slots[empty] = skill;
            }
            MarkStatsDirty();
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"Learned {skill.DisplayName}"));
            return true;
        }

        public void EquipToSlot(SkillData skill, int slot)
        {
            if (skill == null || skill.Kind != SkillKind.Active || !unlocked.Contains(skill)) return;
            int existing = Array.IndexOf(slots, skill);
            if (existing >= 0) slots[existing] = slots[slot]; // swap if already equipped elsewhere
            slots[slot] = skill;
        }

        // ---------- Using ----------

        public bool TryActivate(int slot)
        {
            var skill = slots[slot];
            if (skill == null) return false;

            if (CooldownRemaining(slot) > 0f) return false;
            if (skill.RequiredMoveset != null && combat.Moveset != skill.RequiredMoveset)
            {
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"{skill.DisplayName} needs {skill.RequiredMoveset.DisplayName}"));
                return false;
            }
            if (!combat.TryStartSkill(skill.Attack)) return false;

            cooldownEnds[slot] = Time.time + skill.Cooldown;
            if (skill.Buff != null && skill.Buff.Length > 0 && skill.BuffDuration > 0f)
            {
                buffs.RemoveAll(b => b.skill == skill); // re-casting refreshes the duration
                buffs.Add((skill, Time.time + skill.BuffDuration));
                MarkStatsDirty();
            }
            return true;
        }

        public void CollectModifiers(List<StatModifier> into)
        {
            foreach (var skill in unlocked)
                if (skill.Kind == SkillKind.Passive && skill.PassiveModifiers != null) into.AddRange(skill.PassiveModifiers);
            foreach (var (skill, _) in buffs) into.AddRange(skill.Buff);
        }

        void MarkStatsDirty()
        {
            if (stats != null) stats.MarkDirty();
        }

        // ---------- Save ----------

        public string CaptureState()
        {
            var state = new State { unlocked = new string[unlocked.Count], slots = new string[SlotCount] };
            int n = 0;
            foreach (var skill in unlocked) state.unlocked[n++] = skill.Id;
            for (int i = 0; i < SlotCount; i++) state.slots[i] = slots[i] != null ? slots[i].Id : string.Empty;
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            if (!Services.TryGet(out GameDatabase database)) return;

            unlocked.Clear();
            buffs.Clear();
            if (state.unlocked != null)
                foreach (string id in state.unlocked)
                    if (database.TryGet(id, out SkillData skill)) unlocked.Add(skill);

            for (int i = 0; i < SlotCount; i++)
            {
                string id = state.slots != null && i < state.slots.Length ? state.slots[i] : string.Empty;
                slots[i] = !string.IsNullOrEmpty(id) && database.TryGet(id, out SkillData skill) && unlocked.Contains(skill) ? skill : null;
                cooldownEnds[i] = 0f;
            }
            MarkStatsDirty();
        }
    }
}
