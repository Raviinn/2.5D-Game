using Beast.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Beast.Gameplay
{
    /// <summary>
    /// Keeps everyone inside the world: invisible walls just inside the edge of the ground, and a safety net that
    /// puts the player back on the last solid ground they stood on if they ever end up below the world
    /// (enemies are sent home). Added automatically to any scene with a Player, built from the "Ground" object.
    /// The walls sit on the Ignore Raycast layer, so the camera and climbing ignore them.
    /// </summary>
    public sealed class WorldEdge : MonoBehaviour
    {
        const int IgnoreRaycastLayer = 2;
        const float WallHeight = 40f;
        const float WallThickness = 2f;
        const float Inset = 0.5f;           // walls stand this far inside the ground's edge
        const float FallBelowGround = 15f;  // this far under the ground counts as fallen out of the world
        const float SafeSampleInterval = 0.25f;

        Transform player;
        PlayerMotor motor;
        PlayerCombat combat;
        Bounds ground;
        bool hasGround;
        float fallLine = -50f;
        Vector3 lastSafe;
        bool hasSafe;
        float nextSample;

        public Bounds Ground => ground;
        public bool HasGround => hasGround;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureFor(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureFor(scene);

        static void EnsureFor(Scene scene)
        {
            if (!scene.isLoaded || GameObject.FindWithTag("Player") == null || FindFirstObjectByType<WorldEdge>() != null) return;
            var go = new GameObject("[World Edge]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<WorldEdge>();
        }

        void Awake()
        {
            var groundObject = GameObject.Find("Ground");
            if (groundObject != null && groundObject.TryGetComponent(out Collider groundCollider))
            {
                ground = groundCollider.bounds;
                hasGround = true;
                fallLine = ground.min.y - FallBelowGround;
                BuildWalls();
            }
            else
            {
                Debug.LogWarning("[World Edge] No 'Ground' object with a collider: no edge walls (the fall safety net still works).");
            }
        }

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null) return;
            player = playerObject.transform;
            motor = playerObject.GetComponent<PlayerMotor>();
            combat = playerObject.GetComponent<PlayerCombat>();
            lastSafe = player.position;
            hasSafe = true;
        }

        void BuildWalls()
        {
            Vector3 c = ground.center;
            float top = ground.max.y;
            float halfX = ground.extents.x - Inset;
            float halfZ = ground.extents.z - Inset;
            float y = top + WallHeight * 0.5f - 1f;

            AddWall("Wall_East", new Vector3(c.x + halfX + WallThickness * 0.5f, y, c.z), new Vector3(WallThickness, WallHeight, halfZ * 2f + WallThickness * 2f));
            AddWall("Wall_West", new Vector3(c.x - halfX - WallThickness * 0.5f, y, c.z), new Vector3(WallThickness, WallHeight, halfZ * 2f + WallThickness * 2f));
            AddWall("Wall_North", new Vector3(c.x, y, c.z + halfZ + WallThickness * 0.5f), new Vector3(halfX * 2f + WallThickness * 2f, WallHeight, WallThickness));
            AddWall("Wall_South", new Vector3(c.x, y, c.z - halfZ - WallThickness * 0.5f), new Vector3(halfX * 2f + WallThickness * 2f, WallHeight, WallThickness));
        }

        void AddWall(string wallName, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(wallName) { layer = IgnoreRaycastLayer };
            wall.transform.SetParent(transform, false);
            wall.transform.position = center;
            wall.AddComponent<BoxCollider>().size = size;
            wall.AddComponent<NotClimbable>();
        }

        /// <summary>True if a point is inside the walled area (with a margin).</summary>
        public bool IsInside(Vector3 point, float margin = 0f) =>
            !hasGround || (Mathf.Abs(point.x - ground.center.x) < ground.extents.x - Inset - margin &&
                           Mathf.Abs(point.z - ground.center.z) < ground.extents.z - Inset - margin);

        void Update()
        {
            if (player == null) return;

            // Remember the last place the player stood on solid ground, well inside the walls.
            if (Time.time >= nextSample && motor != null && motor.IsGrounded && !motor.Suspended &&
                (combat == null || !combat.IsDead) && IsInside(player.position, 1.5f))
            {
                lastSafe = player.position;
                hasSafe = true;
                nextSample = Time.time + SafeSampleInterval;
            }

            if (player.position.y < fallLine && motor != null)
            {
                Vector3 back = hasSafe ? lastSafe : (hasGround ? new Vector3(ground.center.x, ground.max.y + 1f, ground.center.z) : Vector3.up);
                motor.Teleport(back + Vector3.up * 0.3f, player.rotation);
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent("You scramble back to solid ground."));
                Debug.LogWarning($"[World Edge] Player fell out of the world; put back at {back}.");
            }

            foreach (var enemy in EnemyController.Active)
            {
                if (enemy != null && enemy.transform.position.y < fallLine) enemy.Warp(enemy.Home, false);
            }
        }
    }
}
