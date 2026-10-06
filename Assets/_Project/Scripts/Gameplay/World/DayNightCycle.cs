using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Day/night lighting from the world clock: the sun rises at dawn, arcs east to west and sets at dusk; at night
    /// the same light becomes a dim blue moon (so there are still moon shadows). Sun colour, ambient light, fog and
    /// sky follow, and clouds / rain from the WeatherSystem dim and grey everything. Midday on a clear day matches
    /// the environment art's original lighting. Lanterns read Darkness to switch on.
    /// </summary>
    [DefaultExecutionOrder(-50)] // before sprites read the sun direction for their shadow quads
    public sealed class DayNightCycle : MonoBehaviour
    {
        [SerializeField] Light sun;
        [SerializeField, Tooltip("Procedural skybox to animate (a copy is used at runtime; the asset isn't changed).")] Material skybox;

        [Header("Day")]
        [SerializeField, Tooltip("Hour the sun rises.")] float dawnHour = 5.5f;
        [SerializeField, Tooltip("Hour the sun sets.")] float duskHour = 20f;
        [SerializeField, Tooltip("Hours that dawn and dusk take to brighten / darken.")] float twilightHours = 1.5f;
        [SerializeField] float maxSunElevation = 55f;
        [SerializeField] float sunIntensity = 1.15f;

        [Header("Night")]
        [SerializeField] float moonElevation = 38f;
        [SerializeField] float moonIntensity = 0.32f;
        [SerializeField] Color moonColor = new(0.55f, 0.66f, 0.95f);

        [Header("Ambient (day → night)")]
        [SerializeField] Color daySky = new(0.56f, 0.6f, 0.66f);
        [SerializeField] Color dayEquator = new(0.44f, 0.43f, 0.38f);
        [SerializeField] Color dayGround = new(0.2f, 0.18f, 0.15f);
        [SerializeField] Color nightSky = new(0.13f, 0.17f, 0.28f);
        [SerializeField] Color nightEquator = new(0.09f, 0.11f, 0.17f);
        [SerializeField] Color nightGround = new(0.04f, 0.045f, 0.06f);
        [SerializeField] Color twilightTint = new(0.2f, 0.08f, 0.0f);

        [Header("Fog")]
        [SerializeField] Color dayFog = new(0.6f, 0.63f, 0.62f);
        [SerializeField] Color nightFog = new(0.06f, 0.08f, 0.13f);
        [SerializeField] Color rainFog = new(0.4f, 0.43f, 0.46f);
        [SerializeField] Vector2 clearFogRange = new(32f, 110f);
        [SerializeField] Vector2 rainFogRange = new(14f, 70f);

        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        const float OvercastSkyThreshold = 0.3f;

        WorldClock clock;
        WeatherSystem weather;
        Material skyInstance;
        Camera cam;
        float sunYaw;

        /// <summary>0 in full daylight, 1 in full night (twilight in between).</summary>
        public float Darkness { get; private set; }
        public bool IsNight => Darkness > 0.5f;

        bool announcedNight;
        bool initialized;

        void Awake()
        {
            Services.Register(this);
            if (sun == null) sun = RenderSettings.sun;
            if (sun != null) sunYaw = sun.transform.eulerAngles.y;
            if (skybox != null)
            {
                skyInstance = new Material(skybox) { name = skybox.name + " (runtime)" };
                RenderSettings.skybox = skyInstance;
            }
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            if (skyInstance != null) Destroy(skyInstance);
        }

        void LateUpdate()
        {
            if (clock == null) Services.TryGet(out clock);
            if (weather == null) Services.TryGet(out weather);
            if (clock == null || sun == null) return;
            Apply((float)(clock.TotalMinutes % WorldClock.MinutesPerDay / 60.0));
            AnnounceNightfall();
        }

        /// <summary>Tells the player when nights start and end (bandits are bolder in between). Silent on load.</summary>
        void AnnounceNightfall()
        {
            if (!initialized)
            {
                initialized = true;
                announcedNight = IsNight;
                return;
            }
            if (IsNight == announcedNight) return;
            announcedNight = IsNight;
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent(IsNight
                ? "Night falls. Bandits are bolder now, and carry more."
                : "Dawn breaks."));
        }

        /// <summary>Sets every light, colour and the sky for this hour of the day (0–24).</summary>
        public void Apply(float hour)
        {
            float overcast = weather != null ? weather.Overcast : 0f;
            float rain = weather != null ? weather.RainAmount : 0f;

            // How much daylight: 0 at night, ramps through dawn and dusk.
            float daylight = Smooth(Mathf.InverseLerp(dawnHour, dawnHour + twilightHours, hour)) *
                             Smooth(Mathf.InverseLerp(duskHour, duskHour - twilightHours, hour));
            Darkness = 1f - daylight;
            float twilight = daylight * (1f - daylight) * 4f; // peaks halfway through dawn / dusk

            bool sunUp = hour > dawnHour && hour < duskHour;
            if (sunUp)
            {
                float arc = Mathf.InverseLerp(dawnHour, duskHour, hour);
                float elevation = maxSunElevation * Mathf.Sin(arc * Mathf.PI);
                sun.transform.rotation = Quaternion.Euler(Mathf.Max(elevation, 2f), sunYaw + Mathf.Lerp(-70f, 70f, arc), 0f);
                // Fades out as it nears the horizon, so swapping to the moon never pops.
                float horizonFade = Smooth(Mathf.Clamp01(elevation / 8f));
                sun.color = Color.Lerp(new Color(1f, 0.94f, 0.84f), new Color(1f, 0.55f, 0.3f), twilight);
                sun.intensity = sunIntensity * horizonFade * Mathf.Lerp(1f, 0.3f, overcast);
                sun.shadowStrength = Mathf.Lerp(0.85f, 0.3f, overcast);
            }
            else
            {
                // Hours since dusk, wrapping past midnight; the moon rises and sets over an hour.
                float sinceDusk = hour >= duskHour ? hour - duskHour : hour + 24f - duskHour;
                float nightLength = 24f - duskHour + dawnHour;
                float nightArc = Mathf.Clamp01(sinceDusk / nightLength);
                float moonFade = Smooth(Mathf.Clamp01(Mathf.Min(sinceDusk, nightLength - sinceDusk) / 1f));
                sun.transform.rotation = Quaternion.Euler(moonElevation, sunYaw + 180f + Mathf.Lerp(-40f, 40f, nightArc), 0f);
                sun.color = moonColor;
                sun.intensity = moonIntensity * moonFade * Mathf.Lerp(1f, 0.25f, overcast);
                sun.shadowStrength = Mathf.Lerp(0.6f, 0.2f, overcast);
            }

            // Ambient: night → day, warm at twilight, greyer (and a touch brighter, diffuse) under cloud.
            var grey = new Color(0.5f, 0.52f, 0.55f) * Mathf.Lerp(0.25f, 1f, daylight);
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(nightSky, daySky, daylight), grey, overcast * 0.5f);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(nightEquator, dayEquator, daylight), grey * 0.85f, overcast * 0.5f) + twilightTint * twilight;
            RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dayGround, daylight);

            var fog = Color.Lerp(nightFog, dayFog, daylight);
            fog = Color.Lerp(fog, rainFog * Mathf.Lerp(0.2f, 1f, daylight), Mathf.Max(rain, overcast * 0.4f)) + twilightTint * twilight * 0.6f;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = Mathf.Lerp(clearFogRange.x, rainFogRange.x, rain);
            RenderSettings.fogEndDistance = Mathf.Lerp(clearFogRange.y, rainFogRange.y, rain);

            if (skyInstance != null)
            {
                skyInstance.SetFloat(ExposureId, Mathf.Lerp(0.12f, 1.3f, daylight) * Mathf.Lerp(1f, 0.7f, overcast));
                skyInstance.SetColor(SkyTintId, Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.42f, 0.43f, 0.45f), overcast));
            }

            // A cloud cover is a flat grey sky: the procedural sky can't do that (thinning its atmosphere turns it
            // night-blue, thickening it turns it orange), so under clouds the camera shows the fog colour instead.
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                bool overcastSky = overcast > OvercastSkyThreshold;
                cam.clearFlags = overcastSky ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                cam.backgroundColor = fog;
            }
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
