using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Dialogue presentation, cinematic style (the camera frames the speaker over your shoulder, see
    /// ThirdPersonCamera): a soft shade along the bottom edge, the speaker's name centred in gold over a thin rule,
    /// the line centred under it (typewriter), and the answer choices as dark pills on the right.
    /// Advance: Space / Enter / F / click / A (first press finishes the typewriter).
    /// Choices: hover + click, Up/Down + Advance, or number keys.
    /// </summary>
    public sealed class DialogueBox : MonoBehaviour
    {
        const float ChoiceHeight = 42f;

        [SerializeField] float charactersPerSecond = 60f;

        DialogueRunner runner;
        InputService input;
        int shownLineId = -1;
        float lineStartTime;
        bool revealAll;
        int selected;
        int hoveredChoice = -1;
        bool navHeld;
        GUIStyle nameStyle, lineStyle, narrationStyle, choiceStyle, discStyle;
        Texture2D shadeTex, ruleTex;

        void Start()
        {
            Services.TryGet(out runner);
            input = Services.Get<InputService>();
        }

        int VisibleCharacters
        {
            get
            {
                int length = runner.CurrentText.Length;
                if (revealAll) return length;
                // Clamp as a float BEFORE converting: huge values must never reach the int cast.
                float typed = Mathf.Clamp((Time.unscaledTime - lineStartTime) * charactersPerSecond, 0f, length);
                return (int)typed;
            }
        }
        bool LineFullyShown => VisibleCharacters >= runner.CurrentText.Length;

        void Update()
        {
            if (runner == null || !runner.IsActive) return;

            if (runner.LineId != shownLineId)
            {
                shownLineId = runner.LineId;
                lineStartTime = Time.unscaledTime;
                revealAll = false;
                selected = 0;
                hoveredChoice = -1; // never let a hover from the previous choices pick from the new ones
            }

            bool advance = input.DialogueAdvance.WasPressedThisFrame();
            bool mouseClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (runner.Choices.Count > 0 && LineFullyShown)
            {
                UpdateChoiceSelection();
                if (TryNumberKey(out int number))
                {
                    runner.Choose(number);
                    return;
                }
                // A click only picks the choice under the mouse; clicking elsewhere does nothing.
                if (advance && (!mouseClick || hoveredChoice >= 0)) runner.Choose(mouseClick ? hoveredChoice : selected);
                return;
            }

            if (!advance) return;
            if (!LineFullyShown) revealAll = true; // first press: reveal the whole line
            else runner.Advance();
        }

        void UpdateChoiceSelection()
        {
            float y = input.Navigate.ReadValue<Vector2>().y;
            if (Mathf.Abs(y) < 0.5f)
            {
                navHeld = false;
                return;
            }
            if (navHeld) return;
            navHeld = true;
            int count = runner.Choices.Count;
            selected = (selected + (y > 0f ? -1 : 1) + count) % count;
        }

        bool TryNumberKey(out int index)
        {
            index = -1;
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            for (int i = 0; i < runner.Choices.Count && i < 9; i++)
            {
                if (!keyboard[Key.Digit1 + i].wasPressedThisFrame) continue;
                index = i;
                return true;
            }
            return false;
        }

        void OnGUI()
        {
            if (runner == null || !runner.IsActive) return;
            UITheme.Begin(-1);
            EnsureStyles();

            float width = UITheme.Width, height = UITheme.Height;
            // A soft shade rising from the bottom edge instead of a box: the camera frames the speaker above it.
            GUI.DrawTexture(new Rect(0f, height - 360f, width, 360f), shadeTex);

            bool narration = string.IsNullOrEmpty(runner.CurrentSpeakerName);
            float textWidth = Mathf.Min(1100f, width - 120f);
            float textX = (width - textWidth) * 0.5f;
            if (!narration)
            {
                ShadowText(new Rect(textX, height - 236f, textWidth, 40f), runner.CurrentSpeakerName, nameStyle, UITheme.InkGold, null);
                // A thin gold rule fading out at both ends, with a small diamond in the middle.
                float ruleWidth = Mathf.Min(760f, textWidth);
                GUI.DrawTexture(new Rect((width - ruleWidth) * 0.5f, height - 192f, ruleWidth, 2f), ruleTex);
                UITheme.DrawIcon(new Rect(width * 0.5f - 6f, height - 197f, 12f, 12f), UITheme.DiamondIcon, UITheme.InkGold);
            }

            // The whole line is laid out at once (the unrevealed part transparent), so centred text never shifts
            // while it types out.
            string full = runner.CurrentText;
            int shown = VisibleCharacters;
            var lineRect = new Rect(textX, height - (narration ? 200f : 176f), textWidth, 130f);
            ShadowText(lineRect, full.Substring(0, shown), narration ? narrationStyle : lineStyle,
                narration ? UITheme.MutedOnInk : UITheme.OffWhite, full.Substring(shown));

            int choiceCount = LineFullyShown ? runner.Choices.Count : 0;
            DrawChoices(choiceCount);

            float hintsY = height - 40f;
            float hintsRight = width - 40f;
            if (choiceCount == 0 && LineFullyShown)
            {
                // Blinking "continue" arrow (pointing down) under the text, and the key hints bottom right.
                float alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                UITheme.DrawIcon(new Rect(width * 0.5f - 9f, height - 46f, 18f, 18f), UITheme.ArrowIcon,
                    new Color(UITheme.InkGold.r, UITheme.InkGold.g, UITheme.InkGold.b, alpha), flipY: true);
                UITheme.KeyHints(hintsRight, hintsY, true, ("Space", "Continue"), ("Esc", "Leave"));
            }
            else if (choiceCount > 0)
            {
                UITheme.KeyHints(hintsRight, hintsY, true, ($"1–{choiceCount}", "Choose"), ("Esc", "Leave"));
            }
        }

        /// <summary>
        /// Answer choices: dark rounded pills stacked on the right, above the line, each with a speech-bubble disc
        /// (showing the number key on keyboard). The selected one is lit gold.
        /// </summary>
        void DrawChoices(int count)
        {
            hoveredChoice = -1;
            if (count == 0) return;
            float width = UITheme.Width, height = UITheme.Height;
            float pillWidth = Mathf.Min(600f, width * 0.4f);
            float x = Mathf.Min(width - pillWidth - 40f, width * 0.5f + 180f);
            float bottom = height - 270f;
            var mouse = Event.current.mousePosition;
            for (int i = 0; i < count; i++)
            {
                var rect = ChoiceRect(i, count, x, bottom, pillWidth);
                if (rect.Contains(mouse))
                {
                    hoveredChoice = i;
                    selected = i;
                }
                bool isSelected = i == selected;
                UITheme.Pill(rect, 0.9f);
                if (isSelected)
                {
                    UITheme.Pill(rect, 0.6f); // a shade darker, with a gold bookmark edge on the left
                    UITheme.Fill(new Rect(rect.x + 6f, rect.y + 10f, 3f, rect.height - 20f), UITheme.InkGold);
                }
                var disc = new Rect(rect.x + 16f, rect.y + (rect.height - 30f) * 0.5f, 30f, 30f);
                UITheme.DrawIcon(disc, UITheme.CircleIcon, isSelected ? UITheme.InkGold : UITheme.OffWhite);
                string glyph = UITheme.UsingGamepad ? (isSelected ? "A" : "…") : (i + 1).ToString();
                GUI.Label(disc, glyph, discStyle);
                choiceStyle.normal.textColor = isSelected ? UITheme.InkGold : UITheme.OffWhite;
                GUI.Label(new Rect(rect.x + 58f, rect.y, rect.width - 72f, rect.height), runner.Choices[i], choiceStyle);
            }
        }

        static Rect ChoiceRect(int index, int count, float x, float bottom, float width) =>
            new(x, bottom - (count - index) * (ChoiceHeight + 10f), width, ChoiceHeight);

        /// <summary>Text with a drop shadow; <paramref name="hidden"/> is laid out but invisible (typewriter).</summary>
        static void ShadowText(Rect rect, string visible, GUIStyle style, Color color, string hidden)
        {
            string tail = string.IsNullOrEmpty(hidden) ? string.Empty : $"<color=#00000000>{hidden}</color>";
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), $"<color=#000000c0>{visible}</color>{tail}", style);
            GUI.Label(rect, $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{visible}</color>{tail}", style);
        }

        void EnsureStyles()
        {
            if (shadeTex == null)
            {
                shadeTex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                for (int y = 0; y < 64; y++)
                {
                    float up = y / 63f; // IMGUI draws texture row 0 at the bottom of the rect
                    shadeTex.SetPixel(0, y, new Color(0f, 0f, 0f, 0.8f * (1f - up * up)));
                }
                shadeTex.Apply();
                ruleTex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var gold = UITheme.InkGold;
                for (int x = 0; x < 64; x++)
                {
                    float middle = 1f - Mathf.Abs(x / 63f * 2f - 1f);
                    ruleTex.SetPixel(x, 0, new Color(gold.r, gold.g, gold.b, 0.85f * Mathf.SmoothStep(0f, 1f, middle)));
                }
                ruleTex.Apply();
            }
            if (nameStyle != null && nameStyle.font == UITheme.InkHeader.font) return;
            nameStyle = new GUIStyle(UITheme.InkHeader)
                { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = Mathf.RoundToInt(UITheme.InkHeader.fontSize * 1.35f) };
            lineStyle = new GUIStyle(UITheme.InkBody)
                { alignment = TextAnchor.UpperCenter, richText = true, wordWrap = true, fontSize = Mathf.RoundToInt(UITheme.InkBody.fontSize * 1.4f) };
            narrationStyle = new GUIStyle(lineStyle) { fontStyle = FontStyle.Italic };
            choiceStyle = new GUIStyle(UITheme.InkBody)
                { wordWrap = false, richText = false, alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(UITheme.InkBody.fontSize * 1.12f) };
            discStyle = new GUIStyle(UITheme.KeyStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = UITheme.Ink } };
        }
    }
}
