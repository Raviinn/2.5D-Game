using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Controller (and arrow-key) navigation for the IMGUI screens. Every themed control (brush buttons, paper cards,
    /// options, steppers, sliders, toggles, slots) registers its on-screen rect as it's drawn; the UINavigator moves a
    /// focus between them with the D-pad / left stick / arrow keys (nearest control in that direction), A / Enter
    /// presses the focused one, and left / right adjust sliders and steppers. The focus shows as a vermilion frame —
    /// only while you're navigating that way: moving the mouse hides it. Key hints switch to pad buttons while a pad
    /// is in use.
    /// </summary>
    public static partial class UITheme
    {
        struct NavEntry
        {
            public Rect Screen;
            public bool Adjustable;
            public bool Preferred;
        }

        static List<NavEntry> navRegistering = new();
        static List<NavEntry> navKnown = new();
        static int navRegisterFrame = -1;
        static int navMissedFrames;
        static Rect navFocus;
        static bool navHasFocus;
        static bool navActivatePending;
        static int navAdjustPending;
        static int navScrollDepth;
        static Rect navFocusLocalInScroll;
        static bool navFocusInScroll;

        /// <summary>True while the focus frame shows (the last menu input was a pad or the arrow keys, not the mouse).</summary>
        public static bool NavShowing { get; private set; }
        /// <summary>True while the last input came from a gamepad: key hints show pad buttons.</summary>
        public static bool UsingGamepad { get; private set; }
        /// <summary>The focused control's on-screen rect (tests read it); valid while NavShowing.</summary>
        public static Rect NavFocus => navFocus;
        /// <summary>True if the focused control takes left / right as a value change (sliders, steppers). Tests read it.</summary>
        public static bool NavFocusAdjustable
        {
            get
            {
                int i = navHasFocus ? IndexOf(navFocus) : -1;
                return i >= 0 && navKnown[i].Adjustable;
            }
        }
        /// <summary>How many controls were on screen last frame (tests read it).</summary>
        public static int NavControlCount => navKnown.Count;
        /// <summary>True for the frame B (or Backspace) was pressed: screens without an Esc action (the title) back out.</summary>
        public static bool NavBackPressed { get; private set; }

        /// <summary>Hides the focus frame, as moving the mouse does (the next press shows it again where navigation starts).</summary>
        public static void HideNavFocus()
        {
            NavShowing = false;
            navHasFocus = false;
        }

        /// <summary>True once per B press (OnGUI runs several times a frame: only the first caller acts on it).</summary>
        public static bool ConsumeNavBack()
        {
            if (!NavBackPressed) return false;
            NavBackPressed = false;
            return true;
        }

        /// <summary>
        /// Registers a navigable control and says whether it has the focus. Call from every event (it registers on Repaint).
        /// adjustable: left / right change its value (NavAdjust) instead of moving the focus away.
        /// preferred: where the focus starts when navigation begins on this screen (e.g. the highlighted menu item).
        /// </summary>
        static bool NavControl(Rect rect, bool adjustable = false, bool preferred = false)
        {
            var screen = GUIUtility.GUIToScreenRect(rect);
            if (Event.current.type == EventType.Repaint)
            {
                if (navRegisterFrame != Time.frameCount)
                {
                    navRegisterFrame = Time.frameCount;
                    navRegistering.Clear();
                }
                navRegistering.Add(new NavEntry { Screen = screen, Adjustable = adjustable, Preferred = preferred });
            }
            bool focused = navHasFocus && Same(screen, navFocus);
            if (focused && navScrollDepth > 0)
            {
                navFocusLocalInScroll = rect;
                navFocusInScroll = true;
            }
            return focused;
        }

        /// <summary>True once when A / Enter is pressed while this control has the focus.</summary>
        static bool NavPressed(Rect rect, bool focused)
        {
            if (!focused || !navActivatePending) return false;
            navActivatePending = false;
            return true;
        }

        /// <summary>-1 / +1 when left / right was pressed on this (adjustable, focused) control; 0 otherwise.</summary>
        static int NavAdjust(bool focused)
        {
            if (!focused || navAdjustPending == 0) return 0;
            int step = navAdjustPending;
            navAdjustPending = 0;
            return step;
        }

        /// <summary>The focus frame: an ink-gold border just outside the control (vermilion already means "selected").</summary>
        static void DrawFocus(Rect rect, bool focused)
        {
            if (!focused || !NavShowing || Event.current.type != EventType.Repaint) return;
            const float t = 3f;
            var r = new Rect(rect.x - t - 1f, rect.y - t - 1f, rect.width + 2f * (t + 1f), rect.height + 2f * (t + 1f));
            Fill(new Rect(r.x, r.y, r.width, t), InkGold);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), InkGold);
            Fill(new Rect(r.x, r.y, t, r.height), InkGold);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), InkGold);
        }

        /// <summary>An invisible, navigable click area: true when clicked or pressed with A. Draws the focus frame.</summary>
        public static bool NavClick(Rect rect, bool preferred = false, bool drawFocus = true)
        {
            bool focused = NavControl(rect, preferred: preferred);
            if (drawFocus) DrawFocus(rect, focused);
            return Click(GUI.Button(rect, GUIContent.none, GUIStyle.none) | NavPressed(rect, focused));
        }

        /// <summary>A click area the pad skips (it has its own buttons for it, e.g. LB / RB for the menu tabs).</summary>
        public static bool PointerClick(Rect rect) => Click(GUI.Button(rect, GUIContent.none, GUIStyle.none));

        /// <summary>True if this rect has the navigation focus (for screens that draw their own highlight).</summary>
        public static bool IsNavFocused(Rect rect) => NavShowing && navHasFocus && Same(GUIUtility.GUIToScreenRect(rect), navFocus);

        /// <summary>GUI.BeginScrollView that also scrolls to keep the focused control visible (pair with EndScroll).</summary>
        public static Vector2 BeginScroll(Rect view, Vector2 scroll, Rect content)
        {
            var result = GUI.BeginScrollView(view, scroll, content);
            navScrollDepth++;
            navFocusInScroll = false;
            return result;
        }

        /// <summary>Ends a BeginScroll view; if the focus is inside and out of sight, scrolls it into view.</summary>
        public static void EndScroll(ref Vector2 scroll, Rect view)
        {
            GUI.EndScrollView();
            navScrollDepth = Mathf.Max(0, navScrollDepth - 1);
            if (!navFocusInScroll || !NavShowing) return;
            navFocusInScroll = false;
            var r = navFocusLocalInScroll;
            float margin = 12f;
            if (r.y - margin < scroll.y) scroll.y = Mathf.Max(0f, r.y - margin);
            else if (r.yMax + margin > scroll.y + view.height) scroll.y = r.yMax + margin - view.height;
        }

        /// <summary>What to show on a key cap: the key, or the pad button that does the same while a pad is in use.</summary>
        public static string KeyLabel(string key)
        {
            if (!UsingGamepad || key == null) return key;
            return key switch
            {
                "Esc" => "B",
                "Space" => "A",
                "Enter" => "A",
                "F" => "↑",
                "Shift" => "B",
                "A / D" => "L-stick",
                "W / S" => "L-stick",
                "Tab" => "View",
                "E" => "RB",
                "Q" => "RT",
                "R" => "↓",
                "X" => "→",
                "Q / E" => "LB / RB",
                "Right-click" => "A",
                "T" => "L3",
                _ when key.Length > 1 && key[0] == '1' && key.Contains("–") => "↑↓",
                _ => key,
            };
        }

        static bool Same(Rect a, Rect b) =>
            Mathf.Abs(a.x - b.x) < 1.5f && Mathf.Abs(a.y - b.y) < 1.5f && Mathf.Abs(a.width - b.width) < 1.5f && Mathf.Abs(a.height - b.height) < 1.5f;

        // ---------- Driven by UINavigator, once per frame before OnGUI ----------

        internal static void NavFrame(Vector2Int move, bool submit, bool back, bool pointerMoved, bool gamepadUsed, bool keyboardUsed)
        {
            // Last frame's controls are this frame's map. A frame that drew nothing (a skipped repaint, a screenshot)
            // keeps the old map; only several in a row mean the menu has really gone.
            bool newScreen = false;
            if (navRegisterFrame != Time.frameCount - 1)
            {
                navRegistering.Clear();
                if (++navMissedFrames > 4) navKnown.Clear();
            }
            else
            {
                navMissedFrames = 0;
                // A different screen (most controls are new): the focus starts afresh rather than near the old one.
                newScreen = SharedFraction(navKnown, navRegistering) < 0.5f;
                (navKnown, navRegistering) = (navRegistering, navKnown);
                navRegistering.Clear();
            }
            navActivatePending = false;
            navAdjustPending = 0;
            NavBackPressed = back;

            if (gamepadUsed) UsingGamepad = true;
            else if (keyboardUsed || pointerMoved) UsingGamepad = false;
            if (pointerMoved) NavShowing = false;

            if (navKnown.Count == 0)
            {
                navHasFocus = false;
                NavShowing = false;
                return;
            }

            if (navHasFocus && IndexOf(navFocus) < 0)
            {
                if (newScreen) navHasFocus = false;
                Refocus(preferStart: newScreen);
            }
            bool input = move != Vector2Int.zero || submit;
            if (!input) return;

            if (!NavShowing)
            {
                // The first press only shows where the focus is.
                NavShowing = true;
                if (!navHasFocus || IndexOf(navFocus) < 0) Refocus(preferStart: true);
                return;
            }
            if (!navHasFocus || IndexOf(navFocus) < 0) Refocus(preferStart: true);

            int current = IndexOf(navFocus);
            if (submit)
            {
                navActivatePending = true;
                return;
            }
            if (move.x != 0 && move.y == 0 && current >= 0 && navKnown[current].Adjustable)
            {
                navAdjustPending = move.x;
                return;
            }
            int next = Neighbour(current, move);
            if (next >= 0) navFocus = navKnown[next].Screen;
        }

        /// <summary>The share of 'next' controls that were also in 'previous'.</summary>
        static float SharedFraction(List<NavEntry> previous, List<NavEntry> next)
        {
            if (next.Count == 0) return 1f;
            int shared = 0;
            foreach (var n in next)
                foreach (var p in previous)
                    if (Same(n.Screen, p.Screen)) { shared++; break; }
            return shared / (float)next.Count;
        }

        static int IndexOf(Rect screen)
        {
            for (int i = 0; i < navKnown.Count; i++) if (Same(navKnown[i].Screen, screen)) return i;
            return -1;
        }

        /// <summary>Puts the focus back on the screen: the preferred control, else the one nearest the old focus, else the first in reading order.</summary>
        static void Refocus(bool preferStart = false)
        {
            int best = -1;
            if (preferStart || !navHasFocus)
            {
                for (int i = 0; i < navKnown.Count; i++) if (navKnown[i].Preferred) { best = i; break; }
            }
            if (best < 0 && navHasFocus)
            {
                float bestDistance = float.MaxValue;
                for (int i = 0; i < navKnown.Count; i++)
                {
                    float d = (navKnown[i].Screen.center - navFocus.center).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = i; }
                }
            }
            if (best < 0)
            {
                for (int i = 0; i < navKnown.Count; i++)
                    if (best < 0 || navKnown[i].Screen.y < navKnown[best].Screen.y - 4f ||
                        (Mathf.Abs(navKnown[i].Screen.y - navKnown[best].Screen.y) <= 4f && navKnown[i].Screen.x < navKnown[best].Screen.x))
                        best = i;
            }
            navHasFocus = best >= 0;
            if (navHasFocus) navFocus = navKnown[best].Screen;
        }

        /// <summary>
        /// The nearest control in a direction, favouring ones straight ahead. 'move' has +y = up; rects are in GUI
        /// screen space (+y = down).
        /// </summary>
        static int Neighbour(int from, Vector2Int move)
        {
            if (from < 0) return -1;
            var origin = navKnown[from].Screen;
            var direction = new Vector2(move.x, -move.y).normalized;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < navKnown.Count; i++)
            {
                if (i == from) continue;
                var target = navKnown[i].Screen;
                // Edge-to-edge along the axis of movement, centre offset across it.
                float along, across;
                if (move.x != 0)
                {
                    along = move.x > 0 ? target.xMin - origin.xMax : origin.xMin - target.xMax;
                    across = Mathf.Abs(target.center.y - origin.center.y);
                    if (OverlapY(origin, target)) across *= 0.2f;
                }
                else
                {
                    along = move.y > 0 ? origin.yMin - target.yMax : target.yMin - origin.yMax;
                    across = Mathf.Abs(target.center.x - origin.center.x);
                    if (OverlapX(origin, target)) across *= 0.2f;
                }
                if (Vector2.Dot(target.center - origin.center, direction) <= 0f || along < -origin.size.magnitude * 0.25f) continue;
                // The next row down beats a control in line but far away (a slider right of a back arrow, not the
                // button at the bottom of the screen); within a row or column the one in line wins.
                float score = Mathf.Max(0f, along) + across * 0.5f;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        static bool OverlapX(Rect a, Rect b) => a.xMin < b.xMax && b.xMin < a.xMax;
        static bool OverlapY(Rect a, Rect b) => a.yMin < b.yMax && b.yMin < a.yMax;
    }

    /// <summary>
    /// Reads the pad, the arrow keys and the mouse once per frame (before OnGUI) and drives UITheme's navigation.
    /// Created automatically and kept across scenes. Only acts while a menu has controls on screen, so it never
    /// steals A or the stick from gameplay.
    /// </summary>
    public sealed class UINavigator : MonoBehaviour
    {
        const float StickThreshold = 0.6f;
        const float RepeatDelay = 0.38f;
        const float RepeatRate = 0.11f;

        Vector2Int heldDirection;
        float nextRepeat;
        Vector2 lastMouse;
        CursorLockMode lastLock;
        float lockChangedAt = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindFirstObjectByType<UINavigator>() != null) return;
            var go = new GameObject("[UI Navigator]");
            DontDestroyOnLoad(go);
            go.AddComponent<UINavigator>();
        }

        void Update()
        {
            var pad = Gamepad.current;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool typing = GUIUtility.keyboardControl != 0; // a text field has the keyboard (the name field)

            Vector2 stick = Vector2.zero;
            bool padUsed = false;
            if (pad != null)
            {
                stick = pad.dpad.ReadValue() + pad.leftStick.ReadValue();
                padUsed = stick.sqrMagnitude > 0.25f || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                          pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
                          pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
                          pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame;
            }
            bool keysUsed = false;
            if (keyboard != null && !typing)
            {
                if (keyboard.upArrowKey.isPressed) stick.y += 1f;
                if (keyboard.downArrowKey.isPressed) stick.y -= 1f;
                if (keyboard.leftArrowKey.isPressed) stick.x -= 1f;
                if (keyboard.rightArrowKey.isPressed) stick.x += 1f;
                keysUsed = keyboard.anyKey.wasPressedThisFrame;
            }

            Vector2Int direction = Vector2Int.zero;
            if (Mathf.Abs(stick.x) >= StickThreshold || Mathf.Abs(stick.y) >= StickThreshold)
                direction = Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2Int(stick.x > 0 ? 1 : -1, 0) : new Vector2Int(0, stick.y > 0 ? 1 : -1);

            // One step on press, then repeats while held.
            Vector2Int move = Vector2Int.zero;
            if (direction != heldDirection)
            {
                heldDirection = direction;
                move = direction;
                nextRepeat = Time.unscaledTime + RepeatDelay;
            }
            else if (direction != Vector2Int.zero && Time.unscaledTime >= nextRepeat)
            {
                move = direction;
                nextRepeat = Time.unscaledTime + RepeatRate;
            }

            bool submit = (pad != null && pad.buttonSouth.wasPressedThisFrame) ||
                          (keyboard != null && !typing && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame));
            bool back = (pad != null && pad.buttonEast.wasPressedThisFrame) || (keyboard != null && !typing && keyboard.backspaceKey.wasPressedThisFrame);

            // Locking / unlocking the cursor (entering play, opening a menu) makes it jump: that's not the player
            // picking up the mouse, so ignore movement for a moment after.
            if (Cursor.lockState != lastLock)
            {
                lastLock = Cursor.lockState;
                lockChangedAt = Time.unscaledTime;
            }
            bool pointerMoved = false;
            if (mouse != null)
            {
                var position = mouse.position.ReadValue();
                bool settled = Time.unscaledTime - lockChangedAt > 0.35f;
                pointerMoved = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                               (settled && (position - lastMouse).sqrMagnitude > 9f);
                lastMouse = position;
            }

            UITheme.NavFrame(move, submit, back, pointerMoved, padUsed, keysUsed && !padUsed);
        }
    }
}
