using Beast.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Beast.Gameplay
{
    /// <summary>
    /// Plays the game's sounds. Created once at startup (when Resources/SoundLibrary exists) and kept across scenes.
    /// - Effects: listens to game events (hits, deaths, pickups, gold, farming, eating) and to sprite frames
    ///   (footsteps on contact frames, swings on the strike frame, dodges, climbing).
    /// - Interface: button clicks, quest and level-up chimes.
    /// - Ambience: day / night loops crossfaded by the clock, rain over them, a quiet wind on the title screen.
    /// Volumes follow Settings → Audio (master through AudioListener.volume; effects, ambience and interface here).
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        enum Category { Effects, Interface }

        const int PoolSize = 24;
        const float HearingRange = 30f;
        const float AmbienceFadeSpeed = 0.35f; // volume per second
        /// <summary>Quest and level-up events raised while a save is being restored stay silent.</summary>
        const float QuietAfterLoad = 1.5f;

        public static GameAudio Instance { get; private set; }

        SoundLibrary library;
        AudioSource[] pool;
        float[] baseVolume;
        Category[] category;
        int nextSource;

        AudioSource day, night, rain, menu;
        AudioListener fallbackListener;
        float quietUntil;
        float lastPickupSound;
        float lastFarmSound;
        float listenerCheckAt;
        WeatherSystem weather;
        float weatherSearchAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance != null || Resources.Load<SoundLibrary>("SoundLibrary") == null) return;
            var go = new GameObject("[Game Audio]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameAudio>();
        }

        public SoundLibrary Library => library;
        /// <summary>Effect and interface sounds started so far (tests read it).</summary>
        public int SoundsPlayed { get; private set; }
        /// <summary>The library list the last sound came from (tests read it).</summary>
        public AudioClip LastClip { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            library = Resources.Load<SoundLibrary>("SoundLibrary");

            pool = new AudioSource[PoolSize];
            baseVolume = new float[PoolSize];
            category = new Category[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Sfx {i}");
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 4f;
                source.maxDistance = HearingRange + 5f;
                source.dopplerLevel = 0f;
                pool[i] = source;
            }

            day = CreateLoop("Ambience Day", library.AmbienceDay);
            night = CreateLoop("Ambience Night", library.AmbienceNight);
            rain = CreateLoop("Ambience Rain", library.AmbienceRain);
            menu = CreateLoop("Ambience Menu", library.AmbienceMenu);

            fallbackListener = gameObject.AddComponent<AudioListener>();
            fallbackListener.enabled = false;

            int lists = 0;
            foreach (var field in typeof(SoundLibrary).GetFields())
                if (field.GetValue(library) is AudioClip[] clips && clips.Length > 0 && clips[0] != null) lists++;
            Debug.Log($"[Audio] Sound ready: {lists} sound lists, ambience {(library.AmbienceDay != null ? "loaded" : "MISSING")}, " +
                      $"listener volume {AudioListener.volume:0.##}{(AudioListener.pause ? " (PAUSED)" : "")}. " +
                      "Hearing nothing? Check the Game view's Mute Audio button and the Windows volume mixer.");
        }

        AudioSource CreateLoop(string loopName, AudioClip clip)
        {
            var source = new GameObject(loopName).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        void OnEnable()
        {
            UITheme.Clicked += OnUiClicked;
            DirectionalSpriteRenderer.FrameShown += OnFrameShown;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EventBus<DamageDealtEvent>.Subscribe(OnDamage);
            EventBus<CombatantDiedEvent>.Subscribe(OnDied);
            EventBus<ItemsAddedEvent>.Subscribe(OnItemsAdded);
            EventBus<GoldChangedEvent>.Subscribe(OnGold);
            EventBus<ItemUsedEvent>.Subscribe(OnItemUsed);
            EventBus<FarmActionEvent>.Subscribe(OnFarm);
            EventBus<QuestStartedEvent>.Subscribe(OnQuestStarted);
            EventBus<QuestReadyEvent>.Subscribe(OnQuestReady);
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<SleptEvent>.Subscribe(OnSlept);
            EventBus<GameLoadedEvent>.Subscribe(OnGameLoaded);
        }

        void OnDisable()
        {
            UITheme.Clicked -= OnUiClicked;
            DirectionalSpriteRenderer.FrameShown -= OnFrameShown;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EventBus<DamageDealtEvent>.Unsubscribe(OnDamage);
            EventBus<CombatantDiedEvent>.Unsubscribe(OnDied);
            EventBus<ItemsAddedEvent>.Unsubscribe(OnItemsAdded);
            EventBus<GoldChangedEvent>.Unsubscribe(OnGold);
            EventBus<ItemUsedEvent>.Unsubscribe(OnItemUsed);
            EventBus<FarmActionEvent>.Unsubscribe(OnFarm);
            EventBus<QuestStartedEvent>.Unsubscribe(OnQuestStarted);
            EventBus<QuestReadyEvent>.Unsubscribe(OnQuestReady);
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<SleptEvent>.Unsubscribe(OnSlept);
            EventBus<GameLoadedEvent>.Unsubscribe(OnGameLoaded);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Playing ----------

        /// <summary>Plays one of the clips. With a position the sound is 3D (and skipped when far from the listener).</summary>
        public void Play(AudioClip[] clips, Vector3? position = null, float volume = 1f, float pitch = 1f, float pitchJitter = 0.06f) =>
            Play(clips, position, volume, pitch, pitchJitter, Category.Effects);

        void Play(AudioClip[] clips, Vector3? position, float volume, float pitch, float pitchJitter, Category kind)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            var listener = ListenerPosition();
            if (position.HasValue && listener.HasValue && (position.Value - listener.Value).sqrMagnitude > HearingRange * HearingRange) return;

            int index = FreeSource();
            var source = pool[index];
            source.Stop();
            source.clip = clip;
            source.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            source.spatialBlend = position.HasValue ? 1f : 0f;
            if (position.HasValue) source.transform.position = position.Value;
            baseVolume[index] = volume;
            category[index] = kind;
            source.volume = volume * CategoryVolume(kind);
            source.Play();
            SoundsPlayed++;
            LastClip = clip;
        }

        /// <summary>A silent source, or the one that has played longest.</summary>
        int FreeSource()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                int index = (nextSource + i) % PoolSize;
                if (!pool[index].isPlaying)
                {
                    nextSource = (index + 1) % PoolSize;
                    return index;
                }
            }
            int oldest = nextSource;
            nextSource = (nextSource + 1) % PoolSize;
            return oldest;
        }

        static float CategoryVolume(Category kind)
        {
            if (!Services.TryGet(out SettingsService settings)) return 1f;
            return kind == Category.Interface ? settings.InterfaceVolume : settings.EffectsVolume;
        }

        static Vector3? ListenerPosition()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : null;
        }

        // ---------- Every frame: volumes, ambience, listener ----------

        void Update()
        {
            float effects = CategoryVolume(Category.Effects), ui = CategoryVolume(Category.Interface);
            for (int i = 0; i < PoolSize; i++)
                if (pool[i].isPlaying) pool[i].volume = baseVolume[i] * (category[i] == Category.Interface ? ui : effects);

            UpdateAmbience();

            if (Time.unscaledTime >= listenerCheckAt)
            {
                listenerCheckAt = Time.unscaledTime + 0.5f;
                EnsureListener();
            }
        }

        void UpdateAmbience()
        {
            float ambience = Services.TryGet(out SettingsService settings) ? settings.AmbienceVolume : 1f;
            Services.TryGet(out GameStateService state);
            bool inMenu = state == null || state.Current is GameState.MainMenu or GameState.Boot;
            bool loading = state != null && state.Current == GameState.Loading;

            float dayTarget = 0f, nightTarget = 0f, rainTarget = 0f, menuTarget = 0f;
            if (inMenu)
            {
                menuTarget = 0.7f;
            }
            else if (!loading)
            {
                float nightness = Services.TryGet(out WorldClock clock) ? Nightness(clock.Hour + clock.Minute / 60f) : 0f;
                float raining = WeatherRain();
                dayTarget = (1f - nightness) * (1f - 0.6f * raining) * 0.8f;
                nightTarget = nightness * (1f - 0.5f * raining) * 0.8f;
                rainTarget = raining * 0.9f;
            }

            Fade(day, dayTarget * ambience);
            Fade(night, nightTarget * ambience);
            Fade(rain, rainTarget * ambience);
            Fade(menu, menuTarget * ambience);
        }

        /// <summary>0 by day, 1 at night, with an hour or two of crossfade at dusk (19-21) and dawn (5-7).</summary>
        static float Nightness(float hour)
        {
            if (hour >= 21f || hour < 5f) return 1f;
            if (hour >= 19f) return Mathf.SmoothStep(0f, 1f, (hour - 19f) / 2f);
            if (hour < 7f) return Mathf.SmoothStep(1f, 0f, (hour - 5f) / 2f);
            return 0f;
        }

        float WeatherRain()
        {
            if (weather == null && Time.unscaledTime >= weatherSearchAt)
            {
                weatherSearchAt = Time.unscaledTime + 2f; // scenes without weather aren't searched every frame
                weather = FindFirstObjectByType<WeatherSystem>();
            }
            return weather != null ? Mathf.Clamp01(weather.RainAmount) : 0f;
        }

        static void Fade(AudioSource source, float target)
        {
            if (source.clip == null) return;
            source.volume = Mathf.MoveTowards(source.volume, target, AmbienceFadeSpeed * Time.unscaledDeltaTime);
            if (source.volume > 0.001f && !source.isPlaying)
            {
                source.time = Random.Range(0f, source.clip.length * 0.9f); // don't always start at the same spot
                source.Play();
            }
            else if (source.volume <= 0.001f && source.isPlaying && target <= 0f)
            {
                source.Stop();
            }
        }

        /// <summary>The title screen has no listener of its own; this object's stands in whenever no other is active.</summary>
        void EnsureListener()
        {
            bool other = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener != fallbackListener && listener.isActiveAndEnabled) other = true;
            fallbackListener.enabled = !other;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            quietUntil = Time.unscaledTime + QuietAfterLoad;
            weather = null;
            weatherSearchAt = 0f;
            EnsureListener();
        }

        void OnGameLoaded(GameLoadedEvent evt) => quietUntil = Time.unscaledTime + QuietAfterLoad;

        bool Quiet => Time.unscaledTime < quietUntil;

        // ---------- Event → sound ----------

        void OnUiClicked() => Play(library.UiClick, null, 0.6f, 1f, 0.04f, Category.Interface);

        void OnFrameShown(DirectionalSpriteRenderer sprite, CharacterAnim anim, int frame)
        {
            var character = sprite.Character;
            if (character == null) return;
            bool isPlayer = character.CompareTag("Player");
            Vector3 at = character.position;
            switch (anim)
            {
                case CharacterAnim.Run when frame == 0 || frame == 3:
                    Play(library.Footstep, at, isPlayer ? 0.45f : 0.3f, 1f, 0.12f);
                    break;
                case CharacterAnim.Attack when frame == sprite.Sheet.Find(CharacterAnim.Attack)?.StartupFrames:
                    bool heavy = isPlayer && character.TryGetComponent(out PlayerAppearance look) && look.ShownLook == WeaponLook.Greatsword;
                    Play(heavy ? library.HeavySwing : library.Swing, at + Vector3.up, heavy ? 0.9f : 0.75f, isPlayer ? 1f : 0.9f, 0.08f);
                    break;
                case CharacterAnim.Dodge when frame == 0:
                    Play(library.Dodge, at, 0.6f);
                    break;
                case CharacterAnim.Climb when frame == 0 || frame == 2:
                    Play(library.Climb, at + Vector3.up, 0.5f, 1f, 0.1f);
                    break;
            }
        }

        void OnDamage(DamageDealtEvent evt)
        {
            if (evt.Target == null) return;
            Vector3 at = evt.Target.transform.position + Vector3.up;
            switch (evt.Result)
            {
                case HitResult.Hit:
                case HitResult.Killed:
                    Play(library.Hit, at, 0.85f, 1f, 0.1f);
                    break;
                case HitResult.Blocked:
                    Play(library.Block, at, 0.8f);
                    break;
                case HitResult.GuardBroken:
                    Play(library.Block, at, 1f, 0.8f);
                    break;
                case HitResult.Parried:
                    Play(library.Parry, at, 0.9f);
                    break;
            }
        }

        void OnDied(CombatantDiedEvent evt)
        {
            if (evt.Combatant != null) Play(library.Death, evt.Combatant.transform.position, 0.9f);
        }

        void OnItemsAdded(ItemsAddedEvent evt)
        {
            // Harvests already make their own sound; several pickups at once make one.
            if (Quiet || Time.unscaledTime - lastFarmSound < 0.2f || Time.unscaledTime - lastPickupSound < 0.08f) return;
            lastPickupSound = Time.unscaledTime;
            Play(library.Pickup, null, 0.5f, 1f, 0.05f);
        }

        void OnGold(GoldChangedEvent evt)
        {
            if (!Quiet && evt.Delta != 0) Play(library.Coins, null, 0.55f, 1f, 0.08f);
        }

        void OnItemUsed(ItemUsedEvent evt)
        {
            if (evt.Item == null) return;
            string name = evt.Item.DisplayName ?? string.Empty;
            bool drink = name.Contains("Draught") || name.Contains("Potion") || name.Contains("Drink") || name.Contains("Ale");
            Play(drink ? library.Drink : library.Eat, null, 0.7f);
        }

        void OnFarm(FarmActionEvent evt)
        {
            lastFarmSound = Time.unscaledTime;
            var clips = evt.Action switch
            {
                FarmAction.Till or FarmAction.Clear => library.Till,
                FarmAction.Plant => library.Plant,
                FarmAction.Water => library.Water,
                _ => library.Harvest,
            };
            Play(clips, evt.Position, 0.8f, 1f, 0.1f);
        }

        void OnQuestStarted(QuestStartedEvent evt)
        {
            if (!Quiet) Play(library.QuestAccepted, null, 0.7f, 1f, 0f, Category.Interface);
        }

        void OnQuestReady(QuestReadyEvent evt)
        {
            if (!Quiet) Play(library.QuestReady, null, 0.6f, 1f, 0f, Category.Interface);
        }

        void OnQuestCompleted(QuestCompletedEvent evt)
        {
            if (!Quiet) Play(library.QuestComplete, null, 0.8f, 1f, 0f, Category.Interface);
        }

        void OnLevelUp(LevelUpEvent evt)
        {
            if (!Quiet) Play(library.LevelUp, null, 0.85f, 1f, 0f, Category.Interface);
        }

        void OnSlept(SleptEvent evt) => Play(library.Sleep, null, 0.7f, 1f, 0f, Category.Interface);
    }
}
