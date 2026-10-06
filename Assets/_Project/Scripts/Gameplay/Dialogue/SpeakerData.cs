using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A character who speaks in dialogue. Ink lines like "Oswin: Hello" (or a "# speaker: Oswin" tag)
    /// are matched to a SpeakerData by DisplayName to show the name and portrait.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Dialogue/Speaker", fileName = "Speaker")]
    public sealed class SpeakerData : GameData
    {
        public string DisplayName = "Stranger";
        [Tooltip("Optional. Until portraits exist, a coloured box with the speaker's initial is shown.")]
        public Sprite Portrait;
        public Color PlaceholderColor = new(0.5f, 0.5f, 0.5f);
    }
}
