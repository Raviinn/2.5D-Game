using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A warm point light (lantern, campfire) that fades in at dusk and out at dawn, with a gentle flicker.
    /// Optional glow renderer (the lantern's glass, the fire's embers) brightens with it.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LanternLight : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] float intensity = 2.2f;
        [SerializeField, Range(0f, 0.5f), Tooltip("0 = steady, 0.3 = campfire.")] float flicker = 0.08f;
        [SerializeField, Tooltip("Lit from this darkness (0 = day, 1 = night).")] float onAtDarkness = 0.25f;
        [SerializeField] Renderer glow;
        [SerializeField] Color glowColor = new(1f, 0.62f, 0.25f);

        Light lamp;
        DayNightCycle cycle;
        MaterialPropertyBlock block;
        float seed;
        float level;

        void Awake()
        {
            lamp = GetComponent<Light>();
            block = new MaterialPropertyBlock();
            seed = Random.value * 100f;
        }

        void LateUpdate()
        {
            if (cycle == null && !Services.TryGet(out cycle)) return;
            float target = Mathf.Clamp01((cycle.Darkness - onAtDarkness) / (1f - onAtDarkness) * 1.5f);
            level = Mathf.MoveTowards(level, target, Time.deltaTime * 0.5f);
            float wobble = 1f + flicker * (Mathf.PerlinNoise(seed, Time.time * 6f) - 0.5f) * 2f;

            lamp.enabled = level > 0.01f;
            lamp.intensity = intensity * level * wobble;
            if (glow != null)
            {
                glow.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(new Color(0.25f, 0.2f, 0.15f), glowColor * (1f + level * 0.6f * wobble), level));
                glow.SetPropertyBlock(block);
            }
        }
    }
}
