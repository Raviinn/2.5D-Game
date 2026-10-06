using System.Collections.Generic;
using Beast.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Beast.Gameplay
{
    /// <summary>
    /// Builds a navigation mesh from the scene's solid colliders when a scene with enemies loads, so enemies can
    /// path around buildings, walls, trees and the climbing course. Nothing is baked into the project: change the
    /// level, and the next load (or Rebuild()) picks it up. Characters, triggers and loose physics objects are ignored.
    /// Added automatically to any scene with an EnemyController; registered as a service.
    /// </summary>
    public sealed class WorldNavigation : MonoBehaviour
    {
        const float AgentRadius = 0.4f;   // matches the characters' CharacterController
        const float AgentHeight = 2f;
        const float AgentClimb = 0.4f;    // the greybox steps rise 0.25 m each
        const float AgentSlope = 40f;
        const float SampleRadius = 1.5f;

        static readonly List<NavMeshBuildSource> sources = new();
        static readonly List<NavMeshBuildMarkup> noMarkups = new();

        NavMeshDataInstance instance;
        NavMeshData data;

        public bool IsReady { get; private set; }
        /// <summary>Triangles in the built mesh (0 if the build failed).</summary>
        public int TriangleCount { get; private set; }

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
            if (!scene.isLoaded || Services.TryGet(out WorldNavigation _)) return;
            if (FindFirstObjectByType<EnemyController>() == null) return;
            var go = new GameObject("[Navigation]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<WorldNavigation>();
        }

        void Awake()
        {
            Services.Register(this);
            Rebuild();
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            if (instance.valid) instance.Remove();
        }

        /// <summary>Rebuilds the navigation mesh from the current scene geometry (about a tenth of a second).</summary>
        public void Rebuild()
        {
            if (instance.valid) instance.Remove();

            var bounds = WorldBounds();
            int excluded = CombatLayers.PlayerMask | CombatLayers.EnemyMask;
            int billboards = LayerMask.NameToLayer(EnvironmentLayers.Billboards);
            if (billboards >= 0) excluded |= 1 << billboards;
            int minimapOnly = LayerMask.NameToLayer(EnvironmentLayers.MinimapOnly);
            if (minimapOnly >= 0) excluded |= 1 << minimapOnly;

            sources.Clear();
            NavMeshBuilder.CollectSources(bounds, ~excluded, NavMeshCollectGeometry.PhysicsColliders, 0, noMarkups, sources);
            sources.RemoveAll(IsIgnored);

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = AgentClimb;
            settings.agentSlope = AgentSlope;

            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogWarning("[Navigation] Couldn't build a navigation mesh; enemies will walk straight at their target.");
                IsReady = false;
                return;
            }
            instance = NavMesh.AddNavMeshData(data);
            TriangleCount = NavMesh.CalculateTriangulation().indices.Length / 3;
            IsReady = TriangleCount > 0;
        }

        static bool IsIgnored(NavMeshBuildSource source)
        {
            if (source.component is not Collider collider) return false;
            if (collider.isTrigger || collider is CharacterController) return true;
            var body = collider.attachedRigidbody;
            return body != null && !body.isKinematic; // loose objects (dropped items) move around
        }

        static Bounds WorldBounds()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.isTrigger) continue;
                if (!any) { bounds = collider.bounds; any = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            bounds.Expand(new Vector3(2f, 4f, 2f));
            return bounds;
        }

        /// <summary>Nearest point on the navigation mesh within a short reach (false if there's none, e.g. on a ledge).</summary>
        public bool TrySnap(Vector3 position, out Vector3 snapped)
        {
            if (IsReady && NavMesh.SamplePosition(position, out var hit, SampleRadius, NavMesh.AllAreas))
            {
                snapped = hit.position;
                return true;
            }
            snapped = position;
            return false;
        }

        /// <summary>
        /// Path from 'from' toward 'to'. Complete = the target can be reached on foot; otherwise the path ends at the
        /// closest reachable point (e.g. the foot of a wall the target climbed). False if no path exists at all.
        /// </summary>
        public bool TryGetPath(Vector3 from, Vector3 to, NavMeshPath path, out bool complete)
        {
            complete = false;
            if (!TrySnap(from, out var start)) return false;
            bool targetOnMesh = TrySnap(to, out var end);
            if (!targetOnMesh) end = to;
            if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid)
            {
                // The target isn't on the mesh at all (hanging, mid-air): head for the closest walkable point below it.
                if (!NavMesh.FindClosestEdge(end, out var edge, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(start, edge.position, NavMesh.AllAreas, path)) return false;
                return path.corners.Length > 0;
            }
            complete = targetOnMesh && path.status == NavMeshPathStatus.PathComplete;
            return path.corners.Length > 0;
        }
    }
}
