using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Glows the character on hit, block, parry, stagger, enemy wind-up and i-frames.
    /// Uses emission (additive), so it's visible on textured sprites; requires _EMISSION on the material.
    /// Writes through MaterialPropertyBlock without clobbering the sprite renderer's values.
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public sealed class CombatantFlash : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Color hitColor = Color.white;
        [SerializeField] Color blockColor = new(0.45f, 0.75f, 1f);
        [SerializeField] Color parryColor = new(1f, 0.9f, 0.2f);
        [SerializeField] Color staggerColor = new(0.65f, 0.35f, 1f);
        [SerializeField] Color telegraphColor = new(1f, 0.45f, 0.05f);
        [SerializeField] Color invulnerableColor = new(0.85f, 0.95f, 1f);
        [SerializeField, Range(0f, 2f)] float intensity = 0.8f;
        [SerializeField, Tooltip("Real-time seconds.")] float flashDuration = 0.12f;

        Combatant combatant;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Color flashColor;
        float flashTimeLeft;
        Color? applied;
        bool initialized;

        void Awake()
        {
            combatant = GetComponent<Combatant>();
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        void OnEnable() => combatant.Hit += OnHit;
        void OnDisable() => combatant.Hit -= OnHit;

        void OnHit(HitResult result)
        {
            flashColor = result switch
            {
                HitResult.Blocked => blockColor,
                HitResult.Parried => parryColor,
                HitResult.GuardBroken => staggerColor,
                _ => hitColor,
            };
            flashTimeLeft = flashDuration;
        }

        void Update()
        {
            if (flashTimeLeft > 0f) flashTimeLeft -= Time.unscaledDeltaTime;

            Color? target =
                flashTimeLeft > 0f ? flashColor :
                combatant.IsStaggered ? staggerColor :
                combatant.Telegraphing ? telegraphColor :
                combatant.Invulnerable ? invulnerableColor * 0.5f :
                (Color?)null;

            if (initialized && target == applied) return;
            initialized = true;
            applied = target;

            Color emission = target.HasValue ? target.Value * intensity : Color.black;
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(block);
            }
        }
    }
}
