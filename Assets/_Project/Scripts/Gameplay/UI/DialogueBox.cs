using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Prototype dialogue presentation (IMGUI): bottom box with portrait, speaker name plate, typewriter text and choices.
    /// Advance: Space / Enter / F / click / A (first press finishes the typewriter).
    /// Choices: hover + click, Up/Down + Advance, or number keys.
    /// </summary>
    public sealed class DialogueBox : MonoBehaviour
    {
        const float ChoiceHeight = 38f;

        [SerializeField] float charactersPerSecond = 60f;

        DialogueRunner runner;
        InputService input;
        int shownLineId = -1;
        float lineStartTime;
        bool revealAll;
        int selected;
        int hoveredChoice = -1;
        bool navHeld;

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

            int choiceCount = LineFullyShown ? runner.Choices.Count : 0;
            float width = Mathf.Min(1100f, UITheme.Width - 48f);
            float height = 196f + choiceCount * (ChoiceHeight + 6f);
            var box = new Rect((UITheme.Width - width) * 0.5f, UITheme.Height - height - 28f, width, height);

            // Soft gradient behind the box keeps the world visible but the text readable.
            UITheme.Fill(new Rect(0f, box.y - 40f, UITheme.Width, UITheme.Height - box.y + 40f), new Color(0f, 0f, 0f, 0.25f));
            UITheme.Panel(box);

            var portrait = new Rect(box.x + 20f, box.y + 20f, 150f, 150f);
            DrawPortrait(portrait);

            float textX = portrait.xMax + 24f;
            float textWidth = box.xMax - textX - 24f;
            bool narration = string.IsNullOrEmpty(runner.CurrentSpeakerName);
            if (!narration)
            {
                // Name plate sits on the box's top edge, like a tab.
                float nameWidth = UITheme.Header.CalcSize(new GUIContent(runner.CurrentSpeakerName)).x + 36f;
                var plate = new Rect(textX - 8f, box.y - 18f, nameWidth, 36f);
                UITheme.Panel(plate);
                GUI.Label(plate, runner.CurrentSpeakerName, UITheme.HeaderCenter);
            }

            string visible = runner.CurrentText.Substring(0, VisibleCharacters);
            string text = narration ? $"<i><color={UITheme.MutedHex}>{visible}</color></i>" : visible;
            GUI.Label(new Rect(textX, box.y + 30f, textWidth, 120f), $"<size=21>{text}</size>", UITheme.Body);

            hoveredChoice = -1;
            var mouse = Event.current.mousePosition;
            for (int i = 0; i < choiceCount; i++)
            {
                var rect = new Rect(textX - 8f, box.y + 168f + i * (ChoiceHeight + 6f), textWidth + 8f, ChoiceHeight);
                bool hovered = rect.Contains(mouse);
                if (hovered)
                {
                    hoveredChoice = i;
                    selected = i;
                }
                bool isSelected = i == selected;
                UITheme.Slot(rect, isSelected, false);
                var chip = new Rect(rect.x + 8f, rect.y + 7f, 24f, 24f);
                UITheme.HudPanel(chip);
                GUI.Label(chip, (i + 1).ToString(), UITheme.KeyStyle);
                GUI.Label(new Rect(rect.x + 44f, rect.y, rect.width - 52f, rect.height),
                    isSelected ? $"<color={UITheme.GoldHex}>{runner.Choices[i]}</color>" : runner.Choices[i], UITheme.BodyMiddle);
            }

            if (choiceCount == 0 && LineFullyShown)
            {
                // Blinking "continue" arrow (pointing down) in the corner.
                float alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                var arrow = new Rect(box.xMax - 40f, box.yMax - 36f, 18f, 18f);
                UITheme.DrawIcon(arrow, UITheme.ArrowIcon, new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, alpha), flipY: true);
                GUI.Label(new Rect(box.xMax - 260f, box.yMax - 40f, 210f, 24f), $"<color={UITheme.MutedHex}>Space / F / click</color>", UITheme.SmallRight);
            }
            else if (choiceCount > 0)
            {
                GUI.Label(new Rect(box.xMax - 360f, box.yMax - 30f, 336f, 22f), $"<color={UITheme.MutedHex}>1–{choiceCount} or ↑↓ + Space · Esc leaves</color>", UITheme.SmallRight);
            }
        }

        void DrawPortrait(Rect rect)
        {
            UITheme.Inset(new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f));
            var data = runner.CurrentSpeakerData;
            if (data != null && data.Portrait != null)
            {
                var sprite = data.Portrait;
                var tex = sprite.texture;
                var r = sprite.textureRect;
                GUI.DrawTextureWithTexCoords(rect, tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
                return;
            }

            // Placeholder: coloured square with the speaker's initial (narration gets a dark box).
            UITheme.Fill(rect, data != null ? data.PlaceholderColor : new Color(0.12f, 0.11f, 0.1f));
            UITheme.Fill(new Rect(rect.x, rect.y, rect.width, rect.height * 0.4f), new Color(1f, 1f, 1f, 0.08f));
            string initial = string.IsNullOrEmpty(runner.CurrentSpeakerName) ? "…" : runner.CurrentSpeakerName.Substring(0, 1);
            UITheme.ShadowLabel(rect, initial, UITheme.Huge, UITheme.Text);
        }
    }
}
