using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// An arrow in flight: a light arc, a hit on the first opponent or piece of world it touches.
    /// Hits go through Combatant.ReceiveHit, so they can be blocked (from the front), parried and dodged.
    /// Arrows that hit the world stick for a few seconds. Built entirely in code (no prefab).
    /// </summary>
    public sealed class Arrow : MonoBehaviour
    {
        const float Gravity = -6f;          // gentler than the world's, so shots stay readable
        const float Lifetime = 4f;
        const float StuckTime = 3f;

        static Material material;

        Combatant shooter;
        Vector3 velocity;
        DamageInfo damage;
        int mask;
        float age;
        bool stuck;

        /// <summary>The launch velocity that reaches 'target' at 'speed' along this arrow's arc (straight-line time).</summary>
        public static Vector3 AimVelocity(Vector3 from, Vector3 target, float speed)
        {
            Vector3 delta = target - from;
            float time = delta.magnitude / Mathf.Max(1f, speed);
            delta.y -= 0.5f * Gravity * time * time; // aim above to make up for the drop
            return delta.normalized * speed;
        }

        public static Arrow Launch(Combatant shooter, Vector3 from, Vector3 velocity, float damage, float poiseDamage)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Arrow";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.05f, 0.05f, 0.75f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var arrow = go.AddComponent<Arrow>();
            arrow.shooter = shooter;
            arrow.velocity = velocity;
            arrow.damage = new DamageInfo
            {
                Damage = damage * (shooter != null ? shooter.DamageMultiplier : 1f),
                PoiseDamage = poiseDamage,
                Knockback = new Vector3(velocity.x, 0f, velocity.z).normalized * 2.5f,
                Attacker = shooter,
            };
            int opponents = CombatLayers.OpponentMask(shooter != null ? shooter.Team : Team.Enemy);
            int characters = CombatLayers.PlayerMask | CombatLayers.EnemyMask;
            int billboards = LayerMask.NameToLayer(EnvironmentLayers.Billboards);
            int world = Physics.DefaultRaycastLayers & ~characters & (billboards >= 0 ? ~(1 << billboards) : ~0);
            arrow.mask = world | opponents;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(velocity));
            return arrow;
        }

        static Material Material()
        {
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "Arrow (generated)" };
            var color = new Color(0.32f, 0.22f, 0.14f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            return material;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            if (stuck)
            {
                if (age > StuckTime) Destroy(gameObject);
                return;
            }
            if (age > Lifetime) { Destroy(gameObject); return; }

            velocity.y += Gravity * dt;
            Vector3 from = transform.position;
            Vector3 step = velocity * dt;
            if (Physics.Raycast(from, step.normalized, out var hit, step.magnitude + 0.35f, mask, QueryTriggerInteraction.Ignore) &&
                !IsShooter(hit.collider))
            {
                var target = hit.collider.GetComponentInParent<Combatant>();
                if (target != null)
                {
                    if (target.Team != (shooter != null ? shooter.Team : Team.Enemy)) target.ReceiveHit(damage);
                    Destroy(gameObject);
                    return;
                }
                // Stick in the world, a little way in.
                transform.position = hit.point - step.normalized * 0.2f;
                stuck = true;
                age = 0f;
                return;
            }
            transform.position = from + step;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        bool IsShooter(Collider c) => shooter != null && c.transform.IsChildOf(shooter.transform);
    }
}
