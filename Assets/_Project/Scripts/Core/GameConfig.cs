using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Core
{
    /// <summary>
    /// Global settings loaded at boot from Resources/GameConfig.
    /// Keep this small: it's the only asset loaded through Resources.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        [Header("Scenes")]
        public string BootstrapScene = "Bootstrap";
        [Tooltip("The world a new game starts in.")]
        public string FirstScene = "World_Test";
        [Tooltip("Title screen (New Game / Continue / Load). If it isn't in Build Settings, Bootstrap goes straight to FirstScene.")]
        public string MainMenuScene = "MainMenu";

        [Header("Input")]
        public InputActionAsset InputActions;

        [Header("Data")]
        public GameDatabase Database;

        [Header("World Time")]
        [Tooltip("Real-time seconds for one full in-game day. 1200 = 20 minutes.")]
        [Min(60f)] public float RealSecondsPerGameDay = 1200f;
        [Range(0, 23)] public int StartHour = 8;
    }
}
