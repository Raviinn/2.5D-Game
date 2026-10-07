using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Every sound the game plays, by purpose. Lists hold variants (one is picked at random each time).
    /// Lives at Resources/SoundLibrary so GameAudio finds it in any scene. The Milestone 20 setup fills it
    /// with generated placeholders; swap in real clips here, no code changes needed.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Audio/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Header("Combat")]
        public AudioClip[] Swing;
        public AudioClip[] HeavySwing;
        public AudioClip[] Hit;
        public AudioClip[] Block;
        public AudioClip[] Parry;
        public AudioClip[] Death;
        public AudioClip[] Dodge;

        [Header("Movement")]
        public AudioClip[] Footstep;
        public AudioClip[] Climb;

        [Header("Items & farming")]
        public AudioClip[] Pickup;
        public AudioClip[] Coins;
        public AudioClip[] Eat;
        public AudioClip[] Drink;
        public AudioClip[] Till;
        public AudioClip[] Plant;
        public AudioClip[] Water;
        public AudioClip[] Harvest;

        [Header("Progress (interface volume)")]
        public AudioClip[] QuestAccepted;
        public AudioClip[] QuestReady;
        public AudioClip[] QuestComplete;
        public AudioClip[] LevelUp;
        public AudioClip[] Sleep;
        public AudioClip[] UiClick;

        [Header("Ambience loops")]
        public AudioClip AmbienceDay;
        public AudioClip AmbienceNight;
        public AudioClip AmbienceRain;
        public AudioClip AmbienceMenu;
    }
}
