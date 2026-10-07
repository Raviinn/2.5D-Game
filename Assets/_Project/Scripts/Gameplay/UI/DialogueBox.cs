using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Dialogue presentation: an ink band across the bottom of the screen with the portrait (framed in paper), the
    /// speaker's name in vermilion capitals, typewriter text and the choices as paper rows (the selected one red).
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
        GUIStyle nameStyle, lineStyle, choiceStyle;

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

            int choiceCount = LineFullyShown ? runner.Choices.Count : 0;
            float height = 232f + choiceCount * (ChoiceHeight + 8f);
            // The band runs past both screen edges, so its ragged brush ends never show.
            var band = new Rect(-60f, UITheme.Height - height, UITheme.Width + 120f, height);
            UITheme.Fill(new Rect(0f, band.y - 60f, UITheme.Width, 60f), new Color(0f, 0f, 0f, 0.12f));
            UITheme.Fill(new Rect(0f, band.y, UITheme.Width, height), new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.9f));
            UITheme.Fill(new Rect(0f, band.y, UITheme.Width, 2f), new Color(1f, 1f, 1f, 0.12f));

            float contentWidth = Mathf.Min(1180f, UITheme.Width - 80f);
            float left = (UITheme.Width - contentWidth) * 0.5f;
            var portrait = new Rect(left, band.y + 34f, 150f, 150f);
            DrawPortrait(portrait);

            float textX = portrait.xMax + 34f;
            float textWidth = left + contentWidth - textX;
            bool narration = string.IsNullOrEmpty(runner.CurrentSpeakerName);
            if (!narration)
                GUI.Label(new Rect(textX, band.y + 24f, textWidth, 34f), UITheme.Spaced(runner.CurrentSpeakerName), nameStyle);

            string visible = runner.CurrentText.Substring(0, VisibleCharacters);
            string text = narration ? $"<i><color={UITheme.MutedOnInkHex}>{visible}</color></i>" : visible;
            GUI.Label(new Rect(textX, band.y + (narration ? 34f : 66f), textWidth, 110f), text, lineStyle);

            hoveredChoice = -1;
            var mouse = Event.current.mousePosition;
            for (int i = 0; i < choiceCount; i++)
            {
                var rect = new Rect(textX, band.y + 184f + i * (ChoiceHeight + 8f), Mathf.Min(textWidth, 760f), ChoiceHeight);
                bool hovered = rect.Contains(mouse);
                if (hovered)
                {
                    hoveredChoice = i;
                    selected = i;
                }
                bool isSelected = i == selected;
                UITheme.PaperCard(rect, isSelected);
                var chip = new Rect(rect.x + 10f, rect.y + 9f, 24f, 24f);
                UITheme.KeyCap(chip);
                GUI.Label(chip, (i + 1).ToString(), UITheme.KeyStyle);
                choiceStyle.normal.textColor = isSelected ? UITheme.OffWhite : UITheme.Ink;
                GUI.Label(new Rect(rect.x + 48f, rect.y, rect.width - 58f, rect.height), runner.Choices[i], choiceStyle);
            }

            float hintsY = UITheme.Height - 40f;
            if (choiceCount == 0 && LineFullyShown)
            {
                // Blinking "continue" arrow (pointing down) beside the hint.
                float alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                float hintsX = UITheme.KeyHints(left + contentWidth, hintsY, true, ("Space", "Continue"));
                UITheme.DrawIcon(new Rect(hintsX - 26f, hintsY + 4f, 16f, 16f), UITheme.ArrowIcon,
                    new Color(UITheme.InkGold.r, UITheme.InkGold.g, UITheme.InkGold.b, alpha), flipY: true);
            }
            else if (choiceCount > 0)
            {
                UITheme.KeyHints(left + contentWidth, hintsY, true, ($"1–{choiceCount}", "Choose"), ("Esc", "Leave"));
            }
        }

        void DrawPortrait(Rect rect)
        {
            // A paper mat around the portrait.
            UITheme.Fill(new Rect(rect.x - 6f, rect.y - 6f, rect.width + 12f, rect.height + 12f), UITheme.Paper);
            var data = runner.CurrentSpeakerData;
            if (data != null && data.Portrait != null)
            {
                var sprite = data.Portrait;
                var tex = sprite.texture;
                var r = sprite.textureRect;
                GUI.DrawTextureWithTexCoords(rect, tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
                return;
            }

            // Placeholder: the speaker's colour with their initial (narration gets an ink square).
            UITheme.Fill(rect, data != null ? data.PlaceholderColor : new Color(0.12f, 0.12f, 0.12f));
            UITheme.Fill(new Rect(rect.x, rect.y, rect.width, rect.height * 0.4f), new Color(1f, 1f, 1f, 0.08f));
            string initial = string.IsNullOrEmpty(runner.CurrentSpeakerName) ? "…" : runner.CurrentSpeakerName.Substring(0, 1);
            UITheme.ShadowLabel(rect, initial, UITheme.Huge, UITheme.OffWhite);
        }

        void EnsureStyles()
        {
            if (nameStyle != null && nameStyle.font == UITheme.InkHeader.font) return;
            nameStyle = new GUIStyle(UITheme.InkHeader) { normal = { textColor = UITheme.Vermilion } };
            lineStyle = new GUIStyle(UITheme.InkBody) { fontSize = Mathf.RoundToInt(UITheme.InkBody.fontSize * 1.18f) };
            choiceStyle = new GUIStyle(UITheme.PaperBody) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
        }
    }
}
