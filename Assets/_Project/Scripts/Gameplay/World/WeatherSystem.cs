using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum Weather
    {
        Clear,
        Cloudy,
        Rain,
    }

    /// <summary>Raised when a new day brings different weather (and once on load).</summary>
    public readonly struct WeatherChangedEvent : IEvent
    {
        public readonly Weather Previous;
        public readonly Weather Current;
        public WeatherChangedEvent(Weather previous, Weather current) { Previous = previous; Current = current; }
    }

    /// <summary>
    /// Daily weather: each in-game day (from midnight) is Clear, Cloudy or Rain, rolled from a fixed seed so a
    /// save always gets the same weather back. Day 1 is always clear. Rain waters every tilled tile (FarmPlot
    /// listens) and falls as streaks around the camera. Lighting reads Overcast / RainAmount (DayNightCycle).
    /// Dev builds: F4 cycles the weather.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("Same seed = same weather on the same days.")] int seed = 1337;
        [SerializeField, Range(0f, 1f)] float cloudyChance = 0.25f;
        [SerializeField, Range(0f, 1f)] float rainChance = 0.2f;
        [SerializeField, Tooltip("Rain streak material (Beast/Rain).")] Material rainMaterial;
        [SerializeField, Tooltip("Real seconds to fade from one weather to the next.")] float blendSeconds = 6f;
        [SerializeField] int maxRaindrops = 2500;

        Weather current;
        bool hasState;
        float overcast;   // 0 clear · 0.5 cloudy · 1 rain (blended)
        float rainAmount; // 0..1 (blended)
        ParticleSystem rain;
        Transform cameraTransform;
        WorldClock clock;

        [Serializable]
        sealed class State
        {
            public int weather;
        }

        public string SaveId => "world.weather";
        public Weather Current => current;
        /// <summary>How grey the sky is: 0 clear, 0.5 cloudy, 1 rain. Blends over a few seconds.</summary>
        public float Overcast => overcast;
        /// <summary>0..1, blends over a few seconds.</summary>
        public float RainAmount => rainAmount;
        public bool IsRaining => current == Weather.Rain;
        /// <summary>In winter, rain falls as snow (slow, drifting flakes).</summary>
        public bool IsSnow => Calendar.Current == Season.Winter;

        void Awake()
        {
            Services.Register(this);
            BuildRain();
        }

        void OnDestroy() => Services.Unregister(this);

        void OnEnable()
        {
            EventBus<DayPassedEvent>.Subscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            EventBus<DayPassedEvent>.Unsubscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start()
        {
            Services.TryGet(out clock);
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            if (!hasState) Set(Roll(clock != null ? clock.Day : 1), announce: false);
            SnapVisuals();
        }

        void OnDayPassed(DayPassedEvent evt) => Set(Roll(evt.Day), announce: true);

        /// <summary>The weather for a day: deterministic for a given seed.</summary>
        public Weather Roll(int day)
        {
            if (day <= 1) return Weather.Clear;
            var random = new System.Random(unchecked(seed * 7919 + day * 104729));
            double roll = random.NextDouble();
            // Seasons (Milestone 46): dry summers, wet autumns, snowy winters.
            float wet = rainChance * Calendar.SeasonOf(day) switch { Season.Summer => 0.5f, Season.Autumn => 1.4f, Season.Winter => 1.5f, _ => 1f };
            return roll < wet ? Weather.Rain : roll < wet + cloudyChance ? Weather.Cloudy : Weather.Clear;
        }

        /// <summary>Changes the weather now (new day, debug key, tests).</summary>
        public void Set(Weather weather, bool announce = true)
        {
            var previous = current;
            current = weather;
            hasState = true;
            if (previous == weather && announce) return;
            EventBus<WeatherChangedEvent>.Raise(new WeatherChangedEvent(previous, weather));
            if (announce && weather == Weather.Rain && previous != Weather.Rain)
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent(IsSnow
                    ? "It's snowing: the field is watered today."
                    : "It's raining: the field is watered today."));
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame) Set((Weather)(((int)current + 1) % 3));
#endif
            float step = Time.deltaTime / Mathf.Max(0.01f, blendSeconds);
            overcast = Mathf.MoveTowards(overcast, TargetOvercast, step);
            rainAmount = Mathf.MoveTowards(rainAmount, current == Weather.Rain ? 1f : 0f, step);
            UpdateRain();
        }

        float TargetOvercast => current switch { Weather.Cloudy => 0.5f, Weather.Rain => 1f, _ => 0f };

        void SnapVisuals()
        {
            overcast = TargetOvercast;
            rainAmount = current == Weather.Rain ? 1f : 0f;
            UpdateRain();
        }

        // ---------- Rain particles ----------

        void BuildRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.1f;
            main.startSpeed = 18f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.78f, 0.84f, 0.95f, 0.32f);
            main.maxParticles = maxRaindrops;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 36f, 0.5f);
            shape.rotation = new Vector3(90f, 0f, 0f); // emit straight down
            shape.position = Vector3.zero;

            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(1.6f); // a little wind
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.4f);

            var emission = rain.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.025f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = rainMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void UpdateRain()
        {
            if (rain == null) return;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) rain.transform.position = cameraTransform.position + Vector3.up * 13f;

            StyleFall(IsSnow);
            var emission = rain.emission;
            emission.rateOverTime = (snowStyle ? maxRaindrops * 0.12f : maxRaindrops / 1.1f) * rainAmount;
            if (rainAmount > 0.01f && !rain.isPlaying && rainMaterial != null) rain.Play();
            else if (rainAmount <= 0.01f && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        bool snowStyle;

        /// <summary>Rain streaks, or (in winter) big slow snowflakes drifting down.</summary>
        void StyleFall(bool snow)
        {
            if (snow == snowStyle) return;
            snowStyle = snow;
            var main = rain.main;
            main.startLifetime = snow ? 6f : 1.1f;
            main.startSpeed = snow ? 2.4f : 18f;
            main.startSize = snow ? 0.09f : 0.035f;
            main.startColor = snow ? new Color(0.96f, 0.97f, 1f, 0.85f) : new Color(0.78f, 0.84f, 0.95f, 0.32f);
            var velocity = rain.velocityOverLifetime;
            // All three axes must use the same curve mode (two constants here), or Unity logs an error every frame.
            velocity.x = new ParticleSystem.MinMaxCurve(snow ? -0.6f : 1.6f, snow ? 0.9f : 1.6f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(snow ? -0.4f : 0.4f, snow ? 0.6f : 0.4f);
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = snow ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
        }

        // ---------- Save ----------

        public string CaptureState() => JsonUtility.ToJson(new State { weather = (int)current });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Set((Weather)Mathf.Clamp(state.weather, 0, 2), announce: false);
            SnapVisuals();
        }
    }
}
