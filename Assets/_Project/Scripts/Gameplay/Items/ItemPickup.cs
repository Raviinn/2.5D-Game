using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A dropped item (or gold) in the world. Pops out, hovers, then flies to the player when close.
    /// Stays put if the bag is full. Uses distance checks, so no trigger colliders or physics layers are needed.
    /// </summary>
    public sealed class ItemPickup : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color GoldColor = new(1f, 0.82f, 0.2f);

        const float ScatterTime = 0.35f;
        const float CollectDelay = 0.6f;
        const float MagnetRange = 2.5f;
        const float MagnetSpeed = 9f;
        const float CollectRange = 0.6f;
        const float HoverHeight = 0.35f;
        const float Lifetime = 180f;

        static Material sharedMaterial;
        static MaterialPropertyBlock block;
        static Inventory playerInventory;

        ItemData item;
        int count;
        int gold;
        float age;
        Vector3 from;
        Vector3 to;
        Transform visual;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => playerInventory = null;

        public static void SpawnItem(Vector3 origin, ItemData item, int count, float scatterRadius) =>
            Spawn(origin, item, count, 0, scatterRadius);

        public static void SpawnGold(Vector3 origin, int gold, float scatterRadius) =>
            Spawn(origin, null, 0, gold, scatterRadius);

        static void Spawn(Vector3 origin, ItemData item, int count, int gold, float scatterRadius)
        {
            var go = new GameObject(item != null ? $"Pickup_{item.DisplayName}" : "Pickup_Gold");
            var pickup = go.AddComponent<ItemPickup>();
            pickup.item = item;
            pickup.count = count;
            pickup.gold = gold;

            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            Vector3 landing = origin + new Vector3(offset.x, 0f, offset.y);
            // Land on whatever is below (Default layer = environment).
            landing.y = Physics.Raycast(landing + Vector3.up * 2f, Vector3.down, out var hit, 10f, 1, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : origin.y - 1f;

            pickup.from = origin;
            pickup.to = landing + Vector3.up * HoverHeight;
            go.transform.position = origin;
            pickup.CreateVisual(item != null ? item.PlaceholderColor : GoldColor, isGold: item == null);
        }

        void CreateVisual(Color color, bool isGold)
        {
            if (sharedMaterial == null) sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            block ??= new MaterialPropertyBlock();

            var shape = GameObject.CreatePrimitive(isGold ? PrimitiveType.Sphere : PrimitiveType.Cube);
            Destroy(shape.GetComponent<Collider>());
            visual = shape.transform;
            visual.SetParent(transform, false);
            visual.localScale = Vector3.one * (isGold ? 0.22f : 0.3f);

            var renderer = shape.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            block.Clear();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            if (age < ScatterTime)
            {
                float t = age / ScatterTime;
                transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1f);
                return;
            }
            if (age > Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            visual.localPosition = Vector3.up * (Mathf.Sin(age * 3f) * 0.08f);
            visual.Rotate(0f, 90f * dt, 0f, Space.World);

            if (age < CollectDelay) return;
            var inventory = FindPlayerInventory();
            if (inventory == null) return;

            Vector3 target = inventory.transform.position;
            float distance = Vector3.Distance(transform.position, target);
            if (distance > MagnetRange) return;
            if (item != null && inventory.SpaceFor(item) <= 0) return; // bag full: stay on the ground

            transform.position = Vector3.MoveTowards(transform.position, target, MagnetSpeed * dt);
            if (distance <= CollectRange) Collect(inventory);
        }

        void Collect(Inventory inventory)
        {
            if (item == null)
            {
                inventory.AddGold(gold);
                Destroy(gameObject);
                return;
            }

            count = inventory.Add(item, count);
            if (count <= 0) Destroy(gameObject);
        }

        static Inventory FindPlayerInventory()
        {
            if (playerInventory == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) player.TryGetComponent(out playerInventory);
            }
            return playerInventory;
        }
    }
}
