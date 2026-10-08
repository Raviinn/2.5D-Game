using System;
using UnityEngine.InputSystem;

namespace Beast.Core
{
    /// <summary>
    /// Milestone 50: key rebinding. The actions you can rebind (one keyboard key and one controller button each),
    /// how a binding reads on a key cap, and the interactive "press a key" rebind. Overrides are kept as JSON in
    /// GameSettings.bindingOverrides (per PC, not per save) and applied by InputService.
    /// Sprint and Dodge share a key, so they're rebound together.
    /// </summary>
    public static class KeyBindings
    {
        public readonly struct Row
        {
            public readonly string Label;
            public readonly string Action;
            /// <summary>Composite part ("up", "down"...) for the movement keys; null otherwise.</summary>
            public readonly string Part;
            /// <summary>Another action that always shares this key (Dodge with Sprint).</summary>
            public readonly string Twin;
            /// <summary>The default keyboard label the HUD prints for it ("F", "Tab"...).</summary>
            public readonly string DefaultKey;

            public Row(string label, string action, string defaultKey, string part = null, string twin = null)
            {
                Label = label; Action = action; DefaultKey = defaultKey; Part = part; Twin = twin;
            }
        }

        public static readonly Row[] Rows =
        {
            new("Move forward", "Move", "W", part: "up"),
            new("Move back", "Move", "S", part: "down"),
            new("Move left", "Move", "A", part: "left"),
            new("Move right", "Move", "D", part: "right"),
            new("Jump / climb", "Jump", "Space"),
            new("Attack", "Attack", "LMB"),
            new("Heavy attack", "Heavy", null),
            new("Block / parry", "Block", "RMB"),
            new("Sprint / dodge", "Sprint", "Shift", twin: "Dodge"),
            new("Interact", "Interact", "F"),
            new("Lock on", "LockOn", "MMB"),
            new("Swap weapon", "SwitchStyle", "X"),
            new("Eat / quick item", "UseItem", "R"),
            new("Switch seeds", "CycleSeed", "V"),
            new("Skill 1", "Skill1", "E"),
            new("Skill 2", "Skill2", "Q"),
            new("Bag", "Inventory", "Tab"),
            new("Character", "Character", "C"),
            new("Journal", "Journal", "J"),
            new("Map", "Map", "M"),
            new("Track next quest", "TrackQuest", "T"),
        };

        static InputActionRebindingExtensions.RebindingOperation operation;

        /// <summary>True while waiting for a key (menus ignore Esc / B meanwhile).</summary>
        public static bool IsRebinding => operation != null;
        /// <summary>The row and column being rebound (-1 when none).</summary>
        public static int RebindingRow { get; private set; } = -1;
        public static bool RebindingGamepad { get; private set; }
        /// <summary>The frame the last rebind ended: menus ignore keys that frame and the next (the key just bound).</summary>
        public static int FinishedFrame { get; private set; } = -10;
        /// <summary>True while rebinding, and for a frame after (menus shouldn't react to the key just pressed).</summary>
        public static bool Busy => IsRebinding || UnityEngine.Time.frameCount - FinishedFrame <= 1;

        static InputActionAsset Asset => Services.TryGet(out InputService input) ? input.Actions : null;

        public static InputAction Find(string action) => Asset?.FindAction(action);

        /// <summary>The keyboard/mouse (or controller) binding of a row: its index in the action's bindings, or -1.</summary>
        public static int BindingIndex(InputAction action, Row row, bool gamepad)
        {
            if (action == null) return -1;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite) continue;
                if (row.Part != null && (!b.isPartOfComposite || !string.Equals(b.name, row.Part, StringComparison.OrdinalIgnoreCase))) continue;
                if (row.Part == null && b.isPartOfComposite) continue;
                string path = b.path ?? string.Empty;
                bool pad = path.StartsWith("<Gamepad>");
                bool keys = path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>") || path.StartsWith("<Pointer>");
                if (gamepad ? pad : keys) return i;
            }
            return -1;
        }

        /// <summary>What a row's binding reads as on a key cap ("F", "Space", "A", "RB"), or "–" when unbound.</summary>
        public static string Label(Row row, bool gamepad)
        {
            var action = Find(row.Action);
            int index = BindingIndex(action, row, gamepad);
            return index < 0 ? "–" : PathLabel(action.bindings[index].effectivePath);
        }

        /// <summary>The binding path a row ends in now (for conflict checks).</summary>
        public static string EffectivePath(Row row, bool gamepad)
        {
            var action = Find(row.Action);
            int index = BindingIndex(action, row, gamepad);
            return index < 0 ? null : action.bindings[index].effectivePath;
        }

        public static string PathLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) return "–";
            switch (path)
            {
                case "<Gamepad>/buttonSouth": return "A";
                case "<Gamepad>/buttonEast": return "B";
                case "<Gamepad>/buttonWest": return "X";
                case "<Gamepad>/buttonNorth": return "Y";
                case "<Gamepad>/leftShoulder": return "LB";
                case "<Gamepad>/rightShoulder": return "RB";
                case "<Gamepad>/leftTrigger": return "LT";
                case "<Gamepad>/rightTrigger": return "RT";
                case "<Gamepad>/dpad/up": return "↑";
                case "<Gamepad>/dpad/down": return "↓";
                case "<Gamepad>/dpad/left": return "←";
                case "<Gamepad>/dpad/right": return "→";
                case "<Gamepad>/select": return "View";
                case "<Gamepad>/start": return "Menu";
                case "<Gamepad>/leftStickPress": return "L3";
                case "<Gamepad>/rightStickPress": return "R3";
                case "<Mouse>/leftButton": return "LMB";
                case "<Mouse>/rightButton": return "RMB";
                case "<Mouse>/middleButton": return "MMB";
                case "<Keyboard>/leftShift": return "Shift";
                case "<Keyboard>/rightShift": return "R-Shift";
                case "<Keyboard>/space": return "Space";
                case "<Keyboard>/tab": return "Tab";
                case "<Keyboard>/leftCtrl": return "Ctrl";
                case "<Keyboard>/leftAlt": return "Alt";
            }
            string readable = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return readable.Length == 1 ? readable.ToUpperInvariant() : readable;
        }

        /// <summary>
        /// For HUD key caps: the label a default key ("F") now reads as, if that action was rebound; null if it wasn't
        /// (or isn't a rebindable key).
        /// </summary>
        public static string Rebound(string defaultKey, bool gamepad)
        {
            if (defaultKey == null) return null;
            foreach (var row in Rows)
            {
                if (row.DefaultKey != defaultKey) continue;
                var action = Find(row.Action);
                int index = BindingIndex(action, row, gamepad);
                if (index < 0 || string.IsNullOrEmpty(action.bindings[index].overridePath)) return null;
                return PathLabel(action.bindings[index].effectivePath);
            }
            return null;
        }

        /// <summary>Waits for the next key (or button) and binds it to this row. Esc (or Start) cancels.</summary>
        public static void StartRebind(int rowIndex, bool gamepad, Action<bool> finished)
        {
            Cancel();
            var row = Rows[rowIndex];
            var action = Find(row.Action);
            int index = BindingIndex(action, row, gamepad);
            if (index < 0) return;
            bool wasEnabled = action.enabled;
            action.Disable();
            RebindingRow = rowIndex;
            RebindingGamepad = gamepad;
            var op = action.PerformInteractiveRebinding(index)
                .WithCancelingThrough(gamepad ? "<Gamepad>/start" : "<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .OnMatchWaitForAnother(0.08f);
            if (gamepad) op.WithControlsHavingToMatchPath("<Gamepad>");
            else op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
            op.OnComplete(o => Done(action, row, index, wasEnabled, true, finished))
              .OnCancel(o => Done(action, row, index, wasEnabled, false, finished));
            operation = op;
            op.Start();
        }

        static void Done(InputAction action, Row row, int index, bool wasEnabled, bool bound, Action<bool> finished)
        {
            var op = operation;
            operation = null;
            RebindingRow = -1;
            FinishedFrame = UnityEngine.Time.frameCount;
            op?.Dispose();
            if (bound && row.Twin != null)
            {
                var twin = Find(row.Twin);
                int twinIndex = BindingIndex(twin, row, RebindingGamepad);
                if (twinIndex >= 0) twin.ApplyBindingOverride(twinIndex, action.bindings[index].effectivePath);
            }
            if (wasEnabled) action.Enable();
            finished?.Invoke(bound);
        }

        public static void Cancel()
        {
            if (operation == null) return;
            operation.Cancel();
        }

        /// <summary>Binds a row directly (tests, or a future "swap" button).</summary>
        public static void Bind(int rowIndex, bool gamepad, string path)
        {
            var row = Rows[rowIndex];
            var action = Find(row.Action);
            int index = BindingIndex(action, row, gamepad);
            if (index < 0) return;
            action.ApplyBindingOverride(index, path);
            if (row.Twin != null)
            {
                var twin = Find(row.Twin);
                int twinIndex = BindingIndex(twin, row, gamepad);
                if (twinIndex >= 0) twin.ApplyBindingOverride(twinIndex, path);
            }
        }

        public static string SaveOverrides() => Asset != null ? Asset.SaveBindingOverridesAsJson() : string.Empty;

        public static void ResetAll()
        {
            Cancel();
            Asset?.RemoveAllBindingOverrides();
        }
    }
}
