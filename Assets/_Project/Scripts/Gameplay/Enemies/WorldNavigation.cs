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
    /// Jump links (Milestone 25) join raised tops to the ground below: every drop up to MaxDrop can be jumped down,
    /// and edges up to MaxVault (the player's own vault height) can also be climbed up, unless they're Not Climbable.
    /// They're found along the mesh's outer edges, so new level geometry gets them for free.
    /// </summary>
    public sealed class WorldNavigation : MonoBehaviour
    {
        const float AgentRadius = 0.4f;   // matches the characters' CharacterController
        const float AgentHeight = 2f;
        const float AgentClimb = 0.4f;    // the greybox steps rise 0.25 m each
        const float AgentSlope = 40f;
        const float SampleRadius = 1.5f;

        /// <summary>Rises up to this are walked, not jumped.</summary>
        public const float StepHeight = AgentClimb + 0.2f;
        /// <summary>Highest edge enemies vault onto (matches the player's vault reach).</summary>
        public const float MaxVault = 2.4f;
        /// <summary>Longest drop enemies jump down (well under the 6 m where falls start to hurt).</summary>
        public const float MaxDrop = 4.5f;
        /// <summary>Horizontal length of a jump link: from just inside the top edge to the landing below.</summary>
        public const float MaxLinkReach = 1.6f;
        const float LinkSpacing = 1f;
        const int MaxLinks = 3000;

        static readonly List<NavMeshBuildSource> sources = new();
        static readonly List<NavMeshBuildMarkup> noMarkups = new();

        NavMeshDataInstance instance;
        NavMeshData data;
        readonly List<NavMeshLinkInstance> links = new();

        public bool IsReady { get; private set; }
        /// <summary>Triangles in the built mesh (0 if the build failed).</summary>
        public int TriangleCount { get; private set; }
        /// <summary>Jump links: down-only drops, and two-way vaults.</summary>
        public int DropLinkCount { get; private set; }
        public int VaultLinkCount { get; private set; }

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
            RemoveLinks();
            if (instance.valid) instance.Remove();
        }

        /// <summary>Rebuilds the navigation mesh from the current scene geometry (about a tenth of a second).</summary>
        public void Rebuild()
        {
            RemoveLinks();
            if (instance.valid) instance.Remove();

            var bounds = WorldBounds();
            int excluded = CombatLayers.PlayerMask | CombatLayers.EnemyMask;
            int billboards = LayerMask.NameToLayer(EnvironmentLayers.Billboards);
            if (billboards >= 0) excluded |= 1 << billboards;
            int minimapOnly = LayerMask.NameToLayer(EnvironmentLayers.MinimapOnly);
            if (minimapOnly >= 0) excluded |= 1 << minimapOnly;
            int npcs = LayerMask.NameToLayer("NPC"); // townsfolk walk around (NpcSchedule): never holes in the mesh
            if (npcs >= 0) excluded |= 1 << npcs;

            sources.Clear();
            NavMeshBuilder.CollectSources(bounds, ~excluded, NavMeshCollectGeometry.PhysicsColliders, 0, noMarkups, sources);
            sources.RemoveAll(IsIgnored);

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = AgentClimb;
            settings.agentSlope = AgentSlope;
            settings.minRegionArea = 0.5f; // keep small tops (a 2 m block leaves ~1.4 m² walkable) so jump links can reach them

            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogWarning("[Navigation] Couldn't build a navigation mesh; enemies will walk straight at their target.");
                IsReady = false;
                return;
            }
            instance = NavMesh.AddNavMeshData(data);
            var triangulation = NavMesh.CalculateTriangulation();
            TriangleCount = triangulation.indices.Length / 3;
            IsReady = TriangleCount > 0;
            if (IsReady) BuildJumpLinks(triangulation, ~excluded);
        }

        void RemoveLinks()
        {
            foreach (var link in links)
                if (link.valid) link.Remove();
            links.Clear();
            DropLinkCount = VaultLinkCount = 0;
        }

        // ---------- Jump links ----------

        /// <summary>
        /// Walks the mesh's outer edges (edges used by one triangle only). Where the ground falls away just past an edge
        /// to another part of the mesh, links the top to the landing.
        /// </summary>
        void BuildJumpLinks(NavMeshTriangulation mesh, int worldMask)
        {
            var vertices = mesh.vertices;
            var indices = mesh.indices;
            var edgeUse = new Dictionary<(Vector3Int, Vector3Int), (int count, int a, int b, int opposite)>();
            for (int t = 0; t < indices.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = indices[t + e], b = indices[t + (e + 1) % 3], c = indices[t + (e + 2) % 3];
                    var ka = Key(vertices[a]);
                    var kb = Key(vertices[b]);
                    var key = ka.x < kb.x || (ka.x == kb.x && (ka.z < kb.z || (ka.z == kb.z && ka.y <= kb.y))) ? (ka, kb) : (kb, ka);
                    edgeUse[key] = edgeUse.TryGetValue(key, out var use) ? (use.count + 1, use.a, use.b, use.opposite) : (1, a, b, c);
                }

            var placed = new List<Vector3>();
            foreach (var use in edgeUse.Values)
            {
                if (use.count != 1) continue;
                Vector3 a = vertices[use.a], b = vertices[use.b], c = vertices[use.opposite];
                Vector3 along = b - a;
                along.y = 0f;
                float length = along.magnitude;
                if (length < 0.2f) continue;
                along /= length;
                var outward = new Vector3(along.z, 0f, -along.x);
                if (Vector3.Dot(outward, (a + b) * 0.5f - c) < 0f) outward = -outward;

                int samples = Mathf.Max(1, Mathf.FloorToInt(length / LinkSpacing));
                for (int i = 0; i < samples && links.Count < MaxLinks; i++)
                {
                    Vector3 top = Vector3.Lerp(a, b, (i + 0.5f) / samples);
                    if (TooClose(placed, top)) continue;
                    if (TryLink(top, outward, worldMask)) placed.Add(top);
                }
            }
        }

        bool TryLink(Vector3 top, Vector3 outward, int worldMask)
        {
            // Nothing in the way going out over the edge (a mesh edge at the foot of a wall has the wall right there).
            Vector3 head = top + Vector3.up * 0.6f;
            float reach = AgentRadius * 2f + 0.2f;
            if (Physics.Raycast(head, outward, reach + AgentRadius, worldMask, QueryTriggerInteraction.Ignore)) return false;

            Vector3 probe = top + outward * reach + Vector3.up * 0.5f;
            if (!Physics.Raycast(probe, Vector3.down, out var ground, MaxDrop + 0.6f, worldMask, QueryTriggerInteraction.Ignore)) return false;
            float drop = top.y - ground.point.y;
            if (drop <= StepHeight || drop > MaxDrop) return false;
            if (!NavMesh.SamplePosition(ground.point, out var land, 0.8f, NavMesh.AllAreas) || Mathf.Abs(land.position.y - ground.point.y) > 0.3f) return false;
            Vector3 flat = land.position - top;
            flat.y = 0f;
            if (flat.magnitude > MaxLinkReach) return false;

            // Two-way (vault up too) if it's low enough and the top may be climbed.
            bool vault = drop <= MaxVault &&
                         (!Physics.Raycast(top + Vector3.up * 0.3f, Vector3.down, out var surface, 0.6f, worldMask, QueryTriggerInteraction.Ignore) ||
                          surface.collider.GetComponentInParent<NotClimbable>() == null);
            var link = new NavMeshLinkData
            {
                startPosition = top,
                endPosition = land.position,
                width = 0f,
                costModifier = -1f,
                bidirectional = vault,
                area = 0,
                agentTypeID = 0,
            };
            var instance = NavMesh.AddLink(link);
            if (!instance.valid) return false;
            links.Add(instance);
            if (vault) VaultLinkCount++; else DropLinkCount++;
            return true;
        }

        static bool TooClose(List<Vector3> placed, Vector3 point)
        {
            foreach (var p in placed)
                if ((p - point).sqrMagnitude < LinkSpacing * LinkSpacing * 0.6f) return true;
            return false;
        }

        static Vector3Int Key(Vector3 v) => new(Mathf.RoundToInt(v.x * 100f), Mathf.RoundToInt(v.y * 100f), Mathf.RoundToInt(v.z * 100f));

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

        /// <summary>True if 'to' can be reached on foot from 'from' (jump links included).</summary>
        public bool CanReach(Vector3 from, Vector3 to) => TryGetPath(from, to, new NavMeshPath(), out bool complete) && complete;

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
