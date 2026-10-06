using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 9 setup: placeholder environment art for World_Test.
    /// - textures the greybox (timber-framed houses with roofs, doors, windows and chimneys; stone tower and steps;
    ///   wooden ramp, bed and notice board; tilled field)
    /// - builds "Environment_Dressing": village square, dirt roads, field fence, well, crates, barrels, an anvil,
    ///   a forest ring, scattered trees (blighted toward the bandit camp), bushes, rocks, grass and flowers,
    ///   all placed around gameplay spaces (NPCs, enemies, field, doors and roads stay clear)
    /// - lighting and distance fog
    /// Safe to re-run: the dressing is rebuilt (same seed, same result); textures and materials you edited are kept.
    /// Put hand-placed props OUTSIDE Environment_Dressing, or they'll be replaced on the next run.
    /// </summary>
    public static class EnvironmentSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string MaterialFolder = EnvironmentArtGenerator.ArtRoot + "/Materials";
        const string DressingName = "Environment_Dressing";
        const string HolderName = "Dress";
        const int Seed = 90210;

        enum Kind { Opaque, Cutout, MapUnlit }

        sealed class MaterialSpec
        {
            public string Name, Texture;
            public Kind Kind;
            public Color Color = Color.white;
            public Vector2 Tiling = Vector2.one;
            /// <summary>Cutout sprites: how far toward the sun they look past themselves (a bit over half their width).</summary>
            public float SelfShadowOffset = 1f;
        }

        static readonly MaterialSpec[] MaterialSpecs =
        {
            new() { Name = "Env_Ground", Texture = "Ground_Grass", Tiling = new Vector2(40f, 40f) },
            new() { Name = "Env_Dirt", Texture = "Ground_Dirt" },
            new() { Name = "Env_Tilled", Texture = "Ground_Tilled" },
            new() { Name = "Env_Cobble", Texture = "Ground_Cobble" },
            new() { Name = "Env_Stone", Texture = "Wall_Stone" },
            new() { Name = "Env_Timber", Texture = "Wall_Timber" },
            new() { Name = "Env_Ivy", Texture = "Wall_Ivy" },
            new() { Name = "Env_Wood", Texture = "Wood_Planks" },
            new() { Name = "Env_Roof", Texture = "Roof_Tiles" },
            new() { Name = "Env_Rock", Texture = "Rock" },
            new() { Name = "Env_Door", Texture = "Door" },
            new() { Name = "Env_Window", Texture = "Window" },
            new() { Name = "Env_Notice", Texture = "Notice" },
            new() { Name = "Env_Cloth", Color = new Color(0.42f, 0.17f, 0.14f) },
            new() { Name = "Env_Linen", Color = new Color(0.82f, 0.78f, 0.68f) },
            new() { Name = "Env_Iron", Color = new Color(0.2f, 0.2f, 0.22f) },
            new() { Name = "Env_Water", Color = new Color(0.12f, 0.18f, 0.22f) },
            new() { Name = "Env_Oak", Texture = "Sprite_Oak", Kind = Kind.Cutout, SelfShadowOffset = 3f },
            new() { Name = "Env_Pine", Texture = "Sprite_Pine", Kind = Kind.Cutout, SelfShadowOffset = 2.2f },
            new() { Name = "Env_DeadTree", Texture = "Sprite_DeadTree", Kind = Kind.Cutout, SelfShadowOffset = 2.6f },
            new() { Name = "Env_Bush", Texture = "Sprite_Bush", Kind = Kind.Cutout, SelfShadowOffset = 1.2f },
            new() { Name = "Env_GrassTuft", Texture = "Sprite_GrassTuft", Kind = Kind.Cutout, SelfShadowOffset = 0.3f },
            new() { Name = "Env_Flowers", Texture = "Sprite_Flowers", Kind = Kind.Cutout, SelfShadowOffset = 0.3f },
            new() { Name = "Env_MapCanopy", Texture = "Map_Canopy", Kind = Kind.MapUnlit, Color = new Color(0.2f, 0.33f, 0.17f) },
            new() { Name = "Env_MapCanopyDead", Texture = "Map_Canopy", Kind = Kind.MapUnlit, Color = new Color(0.32f, 0.28f, 0.33f) },
        };

        enum TreeKind { Oak, Pine, Dead }

        // Filled per run.
        static Dictionary<string, Material> materials;
        static Mesh billboardQuad, flatQuad;
        static Mesh[] rocks;
        static int billboardLayer, minimapLayer;
        static readonly List<Bounds> keepClear = new();
        static readonly List<(Vector3 a, Vector3 b, float radius)> roads = new();
        static readonly List<Vector3> trees = new();
        static Vector3 squareCenter;
        /// <summary>Direction from the square toward the bandit camp (blight grows that way); zero if there are no bandits.</summary>
        static Vector3 campDirection;
        static float squareRadius;
        static System.Random random;

        [MenuItem("Beast/Setup/Run Milestone 9 Setup (Environment Art)", priority = 8)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test scene not found. Run the Milestone 1–8 setups first.");
                return;
            }

            billboardLayer = CombatPrototypeSetup.EnsureLayer(EnvironmentLayers.Billboards);
            minimapLayer = CombatPrototypeSetup.EnsureLayer(EnvironmentLayers.MinimapOnly);
            if (billboardLayer < 0 || minimapLayer < 0) return;

            var textures = EnvironmentArtGenerator.EnsureTextures();
            var materialPaths = EnsureMaterials(textures);
            string quadPath = AssetDatabase.GetAssetPath(EnvironmentArtGenerator.SaveMesh("BillboardQuad", EnvironmentArtGenerator.BuildBillboardQuad));
            string flatPath = AssetDatabase.GetAssetPath(EnvironmentArtGenerator.SaveMesh("FlatQuad", EnvironmentArtGenerator.BuildFlatQuad));
            var rockPaths = new string[3];
            for (int i = 0; i < rockPaths.Length; i++)
            {
                int seed = 100 + i;
                rockPaths[i] = AssetDatabase.GetAssetPath(EnvironmentArtGenerator.SaveMesh($"Rock_{i}", m => EnvironmentArtGenerator.BuildRock(m, seed)));
            }
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);

            // Opening a scene unloads assets; reload everything by path so the scene stores valid references.
            materials = new Dictionary<string, Material>();
            foreach (var pair in materialPaths) materials[pair.Key] = AssetDatabase.LoadAssetAtPath<Material>(pair.Value);
            billboardQuad = AssetDatabase.LoadAssetAtPath<Mesh>(quadPath);
            flatQuad = AssetDatabase.LoadAssetAtPath<Mesh>(flatPath);
            rocks = new Mesh[rockPaths.Length];
            for (int i = 0; i < rockPaths.Length; i++) rocks[i] = AssetDatabase.LoadAssetAtPath<Mesh>(rockPaths[i]);
            random = new System.Random(Seed);

            var old = GameObject.Find(DressingName);
            if (old != null) Object.DestroyImmediate(old);

            FindSquare();
            DressGreybox(textures);
            CollectKeepClear();

            var dressing = new GameObject(DressingName).transform;
            BuildSquareAndRoads(dressing);
            BuildFence(dressing);
            BuildProps(dressing);
            BuildTrees(dressing);
            BuildBushesAndRocks(dressing);
            BuildGroundCover(dressing);
            SetUpLighting();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Milestone 9 environment art complete: {trees.Count} trees, roads, props and ground cover. " +
                      $"Textures are in {EnvironmentArtGenerator.TextureFolder} (edit or replace them freely). Press Play.");
        }

        // ---------- Materials ----------

        /// <summary>Creates any missing Env_* material (yours are kept) and returns name → asset path. Also used by the climbing setup.</summary>
        internal static Dictionary<string, string> EnsureMaterials(Dictionary<string, Texture2D> textures)
        {
            EnvironmentArtGenerator.EnsureFolder(MaterialFolder);
            var paths = new Dictionary<string, string>();
            foreach (var spec in MaterialSpecs)
            {
                string path = $"{MaterialFolder}/{spec.Name}.mat";
                paths[spec.Name] = path;
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue; // keep your tuning

                var texture = spec.Texture != null && textures.TryGetValue(spec.Texture, out var t) ? t : null;
                var material = spec.Kind == Kind.MapUnlit
                    ? new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                    : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", spec.Color);
                if (texture != null)
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetTextureScale("_BaseMap", spec.Tiling);
                }

                if (spec.Kind != Kind.MapUnlit)
                {
                    material.SetFloat("_Smoothness", 0f);
                    material.SetFloat("_SpecularHighlights", 0f);
                    material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    material.SetFloat("_EnvironmentReflections", 0f);
                    material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }
                if (spec.Kind is Kind.Cutout or Kind.MapUnlit)
                {
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", 0.5f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.SetFloat("_Cull", (float)CullMode.Off);
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                }
                if (spec.Kind == Kind.Cutout)
                {
                    // Same rule as character sprites: each billboard's sun-facing shadow quad would shade its own body.
                    // Plain URP/Lit can't tell, so it doesn't receive; Beast/Sprite Lit (below) receives and skips its own quad.
                    material.SetFloat("_ReceiveShadows", 0f);
                    material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                    SpriteShadowSetup.Convert(material, spec.SelfShadowOffset);
                }
                AssetDatabase.CreateAsset(material, path);
            }
            return paths;
        }

        // ---------- Greybox dressing ----------

        static void FindSquare()
        {
            var points = new List<Vector3>();
            foreach (var speaker in Object.FindObjectsByType<DialogueSpeaker>(FindObjectsSortMode.None)) points.Add(speaker.transform.position);
            foreach (var board in Object.FindObjectsByType<ContractBoard>(FindObjectsSortMode.None)) points.Add(board.transform.position);
            foreach (var shop in Object.FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None)) points.Add(shop.transform.position);

            squareCenter = new Vector3(2f, 0f, 3f);
            if (points.Count > 0)
            {
                squareCenter = Vector3.zero;
                foreach (var p in points) squareCenter += p;
                squareCenter /= points.Count;
            }
            squareCenter.y = 0f;
            squareRadius = 5f;
            foreach (var p in points) squareRadius = Mathf.Max(squareRadius, Flat(p - squareCenter).magnitude + 2.5f);
            squareRadius = Mathf.Min(squareRadius, 9f);

            var camp = Vector3.zero;
            int bandits = 0;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (enemy.Data == null || !enemy.Data.IsTrainingDummy) { camp += enemy.transform.position; bandits++; }
            campDirection = bandits > 0 ? Flat(camp / bandits - squareCenter).normalized : Vector3.zero;
        }

        static void DressGreybox(Dictionary<string, Texture2D> textures)
        {
            var ground = GameObject.Find("Ground");
            if (ground != null && ground.TryGetComponent(out MeshRenderer groundRenderer))
            {
                groundRenderer.sharedMaterial = materials["Env_Ground"];
                // Plane is 10 m per unit of scale; keep the grass at ~2.5 m per tile whatever the ground size.
                var tiling = new Vector2(ground.transform.lossyScale.x * 10f / 2.5f, ground.transform.lossyScale.z * 10f / 2.5f);
                materials["Env_Ground"].SetTextureScale("_BaseMap", tiling);
                EditorUtility.SetDirty(materials["Env_Ground"]);
            }

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                // Re-runs destroy the previous details ("Dress" holders) while this list is being walked.
                if (t == null || t.name == HolderName || (t.parent != null && t.parent.name == HolderName)) continue;
                string n = t.name;
                if (n.StartsWith("House_")) DressHouse(t);
                else if (n == "Tower") DressTower(t);
                else if (n.StartsWith("Step_")) DressBox(t.gameObject, "Env_Stone", 2f);
                else if (n == "Ramp") DressBox(t.gameObject, "Env_Wood", 2f);
                else if (n == "FieldBase") DressBox(t.gameObject, "Env_Dirt", 2f);
                else if (n == "Bed") DressBed(t);
                else if (t.TryGetComponent(out ContractBoard _)) DressBoard(t);
            }

            // Tilled tiles get a furrowed texture; the dry/wet tint now multiplies it.
            foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(plot);
                so.FindProperty("soilTexture").objectReferenceValue = textures["Ground_Tilled"];
                so.FindProperty("drySoil").colorValue = new Color(1f, 0.95f, 0.88f);
                so.FindProperty("wetSoil").colorValue = new Color(0.55f, 0.48f, 0.42f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void DressBox(GameObject go, string material, float metresPerTile)
        {
            if (!go.TryGetComponent(out MeshFilter filter) || !go.TryGetComponent(out MeshRenderer renderer)) return;
            var scale = go.transform.lossyScale;
            filter.sharedMesh = EnvironmentArtGenerator.SaveMesh($"Box_{go.name}", m => EnvironmentArtGenerator.BuildWorldUvBox(m, scale, metresPerTile));
            renderer.sharedMaterial = materials[material];
        }

        /// <summary>
        /// A child that cancels the parent's (non-uniform) scale, so details can be placed in metres
        /// relative to the object's centre. Rebuilt on every run.
        /// </summary>
        static Transform Holder(Transform parent)
        {
            var existing = parent.Find(HolderName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var holder = new GameObject(HolderName).transform;
            holder.SetParent(parent, false);
            var s = parent.lossyScale;
            holder.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            return holder;
        }

        /// <summary>The object's face (±X or ±Z, in its own space) that points most toward the village square.</summary>
        static Vector3 FaceTowardSquare(Transform t)
        {
            var local = t.InverseTransformDirection(Flat(squareCenter - t.position));
            return Mathf.Abs(local.x) > Mathf.Abs(local.z) ? new Vector3(Mathf.Sign(local.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z));
        }

        static void DressHouse(Transform house)
        {
            DressBox(house.gameObject, "Env_Timber", 3f);
            var holder = Holder(house);
            var s = house.lossyScale;
            float roofHeight = Mathf.Min(s.x, s.z) * 0.45f + 0.6f;

            var roof = Part("Roof", holder, new Vector3(0f, s.y * 0.5f, 0f), Quaternion.identity, Vector3.one,
                EnvironmentArtGenerator.SaveMesh($"Roof_{house.name}", m => EnvironmentArtGenerator.BuildGableRoof(m, s.x + 0.7f, s.z + 0.7f, roofHeight, 2f)),
                materials["Env_Roof"], materials["Env_Wood"]);
            roof.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            roof.AddComponent<MeshCollider>(); // solid, so the camera can't slip inside the roof above the walls

            // Chimney on one slope.
            bool ridgeAlongX = s.x >= s.z;
            float along = (ridgeAlongX ? s.x : s.z) * 0.22f;
            float across = (ridgeAlongX ? s.z : s.x) * 0.18f;
            var chimneyPosition = ridgeAlongX ? new Vector3(along, 0f, across) : new Vector3(across, 0f, along);
            chimneyPosition.y = s.y * 0.5f + roofHeight * 0.6f + 0.4f;
            Block("Chimney", holder, chimneyPosition, new Vector3(0.6f, 1.6f, 0.6f), "Env_Stone", 1.5f, collider: false);

            // Door on the face toward the square, windows beside it and on the side walls.
            var front = FaceTowardSquare(house);
            float frontExtent = Mathf.Abs(front.x) > 0f ? s.x * 0.5f : s.z * 0.5f;
            float frontWidth = Mathf.Abs(front.x) > 0f ? s.z : s.x;
            var side = new Vector3(front.z, 0f, -front.x);
            float bottom = -s.y * 0.5f;

            Decal("Door", holder, front * (frontExtent + 0.02f) + Vector3.up * (bottom + 1.0f), front, new Vector2(1.1f, 2f), "Env_Door");
            if (s.y >= 2.4f)
            {
                float windowOffset = Mathf.Max(1.35f, frontWidth * 0.3f);
                if (frontWidth * 0.5f > windowOffset + 0.5f)
                {
                    Decal("Window_FrontA", holder, front * (frontExtent + 0.02f) + side * windowOffset + Vector3.up * (bottom + 1.6f), front, new Vector2(0.9f, 0.9f), "Env_Window");
                    Decal("Window_FrontB", holder, front * (frontExtent + 0.02f) - side * windowOffset + Vector3.up * (bottom + 1.6f), front, new Vector2(0.9f, 0.9f), "Env_Window");
                }
                float sideExtent = Mathf.Abs(front.x) > 0f ? s.z * 0.5f : s.x * 0.5f;
                Decal("Window_SideA", holder, side * (sideExtent + 0.02f) + Vector3.up * (bottom + 1.6f), side, new Vector2(0.9f, 0.9f), "Env_Window");
                Decal("Window_SideB", holder, -side * (sideExtent + 0.02f) + Vector3.up * (bottom + 1.6f), -side, new Vector2(0.9f, 0.9f), "Env_Window");
            }

            // Doorstep, so the door reads as an entrance.
            Block("Doorstep", holder, front * (frontExtent + 0.35f) + Vector3.up * (bottom + 0.06f), Abs(side * 1.5f + front * 0.7f) + Vector3.up * 0.12f, "Env_Stone", 1.5f, collider: false);
        }

        static void DressTower(Transform tower)
        {
            DressBox(tower.gameObject, "Env_Stone", 2f);
            var holder = Holder(tower);
            var s = tower.lossyScale;
            float top = s.y * 0.5f + 0.3f;
            float hx = s.x * 0.5f - 0.25f, hz = s.z * 0.5f - 0.25f;
            var merlon = new Vector3(0.5f, 0.6f, 0.5f);
            foreach (var p in new[] { new Vector3(-hx, top, -hz), new Vector3(hx, top, -hz), new Vector3(-hx, top, hz), new Vector3(hx, top, hz),
                         new Vector3(0f, top, -hz), new Vector3(0f, top, hz), new Vector3(-hx, top, 0f), new Vector3(hx, top, 0f) })
                Block("Merlon", holder, p, merlon, "Env_Stone", 1.5f, collider: true);
            Decal("Window", holder, FaceTowardSquare(tower) * (Mathf.Abs(FaceTowardSquare(tower).x) > 0f ? s.x * 0.5f + 0.02f : s.z * 0.5f + 0.02f) + Vector3.up * (s.y * 0.25f),
                FaceTowardSquare(tower), new Vector2(0.6f, 1f), "Env_Window");
        }

        static void DressBed(Transform bed)
        {
            DressBox(bed.gameObject, "Env_Wood", 1f);
            var holder = Holder(bed);
            var s = bed.lossyScale;
            bool alongZ = s.z >= s.x;
            var length = alongZ ? Vector3.forward : Vector3.right;
            float bedLength = alongZ ? s.z : s.x;
            float width = alongZ ? s.x : s.z;
            var blanketSize = alongZ ? new Vector3(width * 0.96f, 0.08f, bedLength * 0.7f) : new Vector3(bedLength * 0.7f, 0.08f, width * 0.96f);
            Block("Blanket", holder, -length * bedLength * 0.15f + Vector3.up * (s.y * 0.5f + 0.04f), blanketSize, "Env_Cloth", 1f, collider: false);
            var pillowSize = alongZ ? new Vector3(width * 0.7f, 0.12f, 0.35f) : new Vector3(0.35f, 0.12f, width * 0.7f);
            Block("Pillow", holder, length * (bedLength * 0.5f - 0.25f) + Vector3.up * (s.y * 0.5f + 0.06f), pillowSize, "Env_Linen", 1f, collider: false);
        }

        static void DressBoard(Transform board)
        {
            DressBox(board.gameObject, "Env_Wood", 1f);
            var holder = Holder(board);
            var s = board.lossyScale;
            float groundY = -board.position.y;
            float postHeight = board.position.y + s.y * 0.5f + 0.15f;
            foreach (float x in new[] { -(s.x * 0.5f + 0.08f), s.x * 0.5f + 0.08f })
                Block("Post", holder, new Vector3(x, groundY + postHeight * 0.5f, 0f), new Vector3(0.14f, postHeight, 0.14f), "Env_Wood", 1f, collider: false);
            // The board is thin along Z: post the notices on whichever Z face looks toward the square.
            var front = new Vector3(0f, 0f, board.InverseTransformDirection(Flat(squareCenter - board.position)).z >= 0f ? 1f : -1f);
            float face = s.z * 0.5f + 0.015f;
            Decal("Notice_A", holder, front * face + new Vector3(-0.35f, 0.25f, 0f), front, new Vector2(0.4f, 0.5f), "Env_Notice");
            Decal("Notice_B", holder, front * face + new Vector3(0.3f, 0.35f, 0f), front, new Vector2(0.4f, 0.5f), "Env_Notice");
            Decal("Notice_C", holder, front * face + new Vector3(0f, -0.35f, 0f), front, new Vector2(0.4f, 0.5f), "Env_Notice");
        }

        // ---------- Keep-clear areas ----------

        static void CollectKeepClear()
        {
            keepClear.Clear();
            roads.Clear();
            trees.Clear();
            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.name == "Ground") continue;
                var b = collider.bounds;
                b.Expand(new Vector3(1.5f, 0f, 1.5f));
                keepClear.Add(b);
            }
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.name == "Ground" || renderer.bounds.size.x > 40f) continue;
                var b = renderer.bounds;
                b.Expand(new Vector3(1f, 0f, 1f));
                keepClear.Add(b);
            }
            // People and fights need room.
            foreach (var interactable in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
                if (interactable is not FarmPlot) KeepCircle(interactable.transform.position, 3.5f);
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                KeepCircle(enemy.transform.position, enemy.Data != null && enemy.Data.IsTrainingDummy ? 3f : 7f);
            var player = GameObject.FindWithTag("Player");
            if (player != null) KeepCircle(player.transform.position, 6f);
            KeepCircle(squareCenter, squareRadius + 1f);
        }

        static void KeepCircle(Vector3 center, float radius) =>
            keepClear.Add(new Bounds(new Vector3(center.x, 0f, center.z), new Vector3(radius * 2f, 50f, radius * 2f)));

        static bool IsClear(Vector3 p, float margin, bool allowRoads = false)
        {
            foreach (var b in keepClear)
                if (p.x > b.min.x - margin && p.x < b.max.x + margin && p.z > b.min.z - margin && p.z < b.max.z + margin) return false;
            if (!allowRoads)
                foreach (var (a, b, radius) in roads)
                    if (DistanceToSegment(Flat(p), Flat(a), Flat(b)) < radius + margin) return false;
            return true;
        }

        // ---------- Square and roads ----------

        static void BuildSquareAndRoads(Transform dressing)
        {
            var parent = Group("Roads", dressing);
            // The square must never cover the field (its soil tiles sit at the same height).
            bool hasField = FieldGate(out var field).HasValue;
            bool Blocked(Vector3 offset)
            {
                if (!hasField) return false;
                var p = squareCenter + offset;
                return p.x > field.min.x - 0.4f && p.x < field.max.x + 0.4f && p.z > field.min.z - 0.4f && p.z < field.max.z + 0.4f;
            }
            var square = Part("VillageSquare", parent, squareCenter + Vector3.up * 0.03f, Quaternion.identity, Vector3.one,
                EnvironmentArtGenerator.SaveMesh("VillageSquare", m => EnvironmentArtGenerator.BuildDisc(m, squareRadius, 2f, 7, Blocked)), materials["Env_Cobble"]);
            square.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var targets = new List<(string name, Vector3 point)>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "Door" && t.parent != null && t.parent.name == HolderName)
                    targets.Add(($"Road_{t.parent.parent.name}", t.position - t.forward * 1.6f)); // decals face outward along -forward
                if (t.name == "Ramp")
                {
                    var forward = Flat(t.forward).normalized * (t.lossyScale.z * 0.5f + 1.2f);
                    var a = t.position + forward;
                    var b = t.position - forward;
                    targets.Add(("Road_Ramp", Flat(a - squareCenter).sqrMagnitude < Flat(b - squareCenter).sqrMagnitude ? a : b));
                }
            }
            var lowStep = GameObject.Find("Step_0");
            var highStep = GameObject.Find("Step_1");
            if (lowStep != null && highStep != null)
                targets.Add(("Road_Steps", lowStep.transform.position + Flat(lowStep.transform.position - highStep.transform.position).normalized * 1.6f));

            var gate = FieldGate(out _);
            if (gate.HasValue) targets.Add(("Road_Field", gate.Value + Flat(squareCenter - gate.Value).normalized * 0.6f));

            // The road out of town runs past the bandit camp to the forest edge.
            var outward = campDirection.sqrMagnitude > 0.01f ? campDirection : Vector3.forward;
            targets.Add(("Road_OutOfTown", squareCenter + outward * 64f));

            int index = 0;
            foreach (var (name, point) in targets)
            {
                var target = Flat(point);
                if (name == "Road_OutOfTown") target = ClampToWorld(target, 49f);
                var start = squareCenter + Flat(target - squareCenter).normalized * (squareRadius * 0.75f);
                var line = Wobble(start, target, 0.45f, index);
                float y = 0.012f + index * 0.002f; // overlapping roads never z-fight
                for (int i = 0; i < line.Count; i++) line[i] = new Vector3(line[i].x, y, line[i].z);
                int seed = index;
                float width = name == "Road_OutOfTown" ? 3.2f : 2.2f;
                var road = Part(name, parent, Vector3.zero, Quaternion.identity, Vector3.one,
                    EnvironmentArtGenerator.SaveMesh(name, m => EnvironmentArtGenerator.BuildRibbon(m, line, width, 2f, seed)), materials["Env_Dirt"]);
                road.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                for (int i = 1; i < line.Count; i++) roads.Add((line[i - 1], line[i], width * 0.5f + 0.4f));
                index++;
            }
        }

        static List<Vector3> Wobble(Vector3 from, Vector3 to, float amplitude, int seed)
        {
            var points = new List<Vector3>();
            float length = Flat(to - from).magnitude;
            int segments = Mathf.Max(2, Mathf.CeilToInt(length / 2f));
            var side = Vector3.Cross(Vector3.up, Flat(to - from).normalized);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float sway = Mathf.Sin(t * Mathf.PI * 2f + seed) * amplitude * Mathf.Sin(t * Mathf.PI); // pinned at both ends
                points.Add(Vector3.Lerp(from, to, t) + side * sway);
            }
            return points;
        }

        // ---------- Field fence ----------

        /// <summary>The gap in the field fence on the side facing the village square.</summary>
        static Vector3? FieldGate(out (Vector3 min, Vector3 max, FarmPlot plot) field)
        {
            field = default;
            var plot = Object.FindFirstObjectByType<FarmPlot>();
            if (plot == null) return null;
            var so = new SerializedObject(plot);
            int width = so.FindProperty("width").intValue;
            int depth = so.FindProperty("depth").intValue;
            float tile = so.FindProperty("tileSize").floatValue;
            const float margin = 0.6f;
            var origin = plot.transform.position;
            var min = origin + new Vector3(-margin, 0f, -margin);
            var max = origin + new Vector3(width * tile + margin, 0f, depth * tile + margin);
            field = (min, max, plot);

            var center = (min + max) * 0.5f;
            var toSquare = Flat(squareCenter - center);
            if (Mathf.Abs(toSquare.x) * (max.z - min.z) > Mathf.Abs(toSquare.z) * (max.x - min.x))
                return new Vector3(toSquare.x > 0f ? max.x : min.x, 0f, center.z);
            return new Vector3(center.x, 0f, toSquare.z > 0f ? max.z : min.z);
        }

        static void BuildFence(Transform dressing)
        {
            var gate = FieldGate(out var field);
            if (!gate.HasValue) return;
            var parent = Group("FieldFence", dressing);
            var (min, max, _) = field;
            var corners = new[] { new Vector3(min.x, 0f, min.z), new Vector3(max.x, 0f, min.z), new Vector3(max.x, 0f, max.z), new Vector3(min.x, 0f, max.z) };
            const float gateHalf = 1.1f;
            for (int side = 0; side < 4; side++)
            {
                var a = corners[side];
                var b = corners[(side + 1) % 4];
                float length = Vector3.Distance(a, b);
                int spans = Mathf.Max(1, Mathf.RoundToInt(length / 1.6f));
                for (int i = 0; i < spans; i++) // the end corner is the next side's first post
                {
                    var post = Vector3.Lerp(a, b, i / (float)spans);
                    if (Flat(post - gate.Value).magnitude < gateHalf) continue;
                    Block("Post", parent, post + Vector3.up * 0.5f, new Vector3(0.14f, 1f, 0.14f), "Env_Wood", 1f, collider: true);
                    var next = Vector3.Lerp(a, b, (i + 1) / (float)spans);
                    var mid = (post + next) * 0.5f;
                    if (Flat(mid - gate.Value).magnitude < gateHalf + 0.2f || Flat(next - gate.Value).magnitude < gateHalf) continue;
                    var rotation = Quaternion.LookRotation(next - post);
                    foreach (float h in new[] { 0.42f, 0.78f })
                    {
                        var rail = Block("Rail", parent, mid + Vector3.up * h, new Vector3(0.06f, 0.08f, Vector3.Distance(post, next)), "Env_Wood", 1f, collider: true);
                        rail.transform.rotation = rotation;
                    }
                }
            }
            keepClear.Add(new Bounds((min + max) * 0.5f, new Vector3(max.x - min.x + 3f, 50f, max.z - min.z + 3f)));
        }

        // ---------- Props ----------

        static void BuildProps(Transform dressing)
        {
            var parent = Group("Props", dressing);

            // Well: on the square's edge, as far as possible from people and the roads.
            Vector3 best = squareCenter;
            float bestScore = float.MinValue;
            var people = new List<Vector3>();
            foreach (var interactable in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
                if (interactable is not FarmPlot) people.Add(interactable.transform.position);
            for (int i = 0; i < 24; i++)
            {
                float angle = i / 24f * Mathf.PI * 2f;
                var candidate = squareCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (squareRadius * 0.6f);
                float score = float.MaxValue;
                foreach (var p in people) score = Mathf.Min(score, Flat(p - candidate).magnitude);
                foreach (var (a, b, radius) in roads) score = Mathf.Min(score, DistanceToSegment(Flat(candidate), Flat(a), Flat(b)) - radius + 2f);
                var player = GameObject.FindWithTag("Player");
                if (player != null) score = Mathf.Min(score, Flat(player.transform.position - candidate).magnitude - 1f);
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            BuildWell(parent, best);

            // Merchant: crates and barrels behind the stall; blacksmith: an anvil on a stump.
            foreach (var shop in Object.FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None))
            {
                var p = shop.transform.position;
                var away = Flat(p - squareCenter).normalized;
                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                var side = Vector3.Cross(Vector3.up, away);
                var spot = Flat(p) + away * 1.7f;
                bool forge = shop.name.Contains("Smith") || shop.name.Contains("Forge") || shop.name.Contains("Blacksmith");
                if (forge)
                {
                    var stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stump.name = "AnvilStump";
                    Place(stump, parent, spot + side * 1.1f + Vector3.up * 0.3f, new Vector3(0.6f, 0.3f, 0.6f), "Env_Wood");
                    Block("Anvil", parent, spot + side * 1.1f + Vector3.up * 0.78f, new Vector3(0.75f, 0.3f, 0.32f), "Env_Iron", 1f, collider: true)
                        .transform.rotation = Quaternion.LookRotation(side);
                    Barrel(parent, spot - side * 1.1f);
                }
                else
                {
                    Block("Crate", parent, spot + side * 1.0f + Vector3.up * 0.4f, Vector3.one * 0.8f, "Env_Wood", 0.8f, collider: true)
                        .transform.rotation = Quaternion.Euler(0f, 12f, 0f);
                    Block("Crate", parent, spot + side * 1.1f + Vector3.up * 1.15f, Vector3.one * 0.7f, "Env_Wood", 0.8f, collider: true)
                        .transform.rotation = Quaternion.Euler(0f, -8f, 0f);
                    Block("Crate", parent, spot + side * 1.95f + Vector3.up * 0.35f, Vector3.one * 0.7f, "Env_Wood", 0.8f, collider: true);
                    Barrel(parent, spot - side * 1.0f);
                    Barrel(parent, spot - side * 1.75f + away * 0.4f);
                }
                KeepCircle(spot, 2.5f);
            }
        }

        static void BuildWell(Transform parent, Vector3 at)
        {
            var well = Group("Well", parent);
            well.position = Flat(at);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            Place(ring, well, well.position + Vector3.up * 0.45f, new Vector3(1.6f, 0.45f, 1.6f), "Env_Stone");
            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            Place(water, well, well.position + Vector3.up * 0.86f, new Vector3(1.3f, 0.02f, 1.3f), "Env_Water");
            foreach (float x in new[] { -0.7f, 0.7f })
                Block("Post", well, new Vector3(x, 1.25f, 0f), new Vector3(0.12f, 1.6f, 0.12f), "Env_Wood", 1f, collider: false);
            Block("Beam", well, Vector3.up * 1.95f, new Vector3(1.6f, 0.1f, 0.1f), "Env_Wood", 1f, collider: false);
            Part("Roof", well, Vector3.up * 2.0f, Quaternion.identity, Vector3.one,
                EnvironmentArtGenerator.SaveMesh("Roof_Well", m => EnvironmentArtGenerator.BuildGableRoof(m, 2.0f, 1.5f, 0.7f, 2f)),
                materials["Env_Roof"], materials["Env_Wood"]).AddComponent<MeshCollider>();
            KeepCircle(at, 2f);
        }

        static void Barrel(Transform parent, Vector3 at)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            Place(barrel, parent, Flat(at) + Vector3.up * 0.45f, new Vector3(0.62f, 0.45f, 0.62f), "Env_Wood");
        }

        // ---------- Trees ----------

        static void BuildTrees(Transform dressing)
        {
            var ring = Group("Forest", dressing);
            var scattered = Group("Trees", dressing);

            // Forest ring: closes off the edges of the 100 m test ground.
            for (float gx = -48f; gx <= 48f; gx += 4f)
            {
                for (float gz = -48f; gz <= 48f; gz += 4f)
                {
                    var p = new Vector3(gx + Jitter(1.6f), 0f, gz + Jitter(1.6f));
                    float edge = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
                    if (edge < 33f || edge > 49.5f || Next() < 0.12f) continue;
                    if (!IsClear(p, 1f)) continue;
                    Tree(ring, p, PickTree(p, inRing: true), edge > 44f);
                }
            }

            // A few trees inside, away from everything that matters.
            int placed = 0;
            for (int attempt = 0; attempt < 400 && placed < 24; attempt++)
            {
                var p = new Vector3(Range(-31f, 31f), 0f, Range(-31f, 31f));
                if (!IsClear(p, 2.5f) || TooClose(p, 5f)) continue;
                Tree(scattered, p, PickTree(p, inRing: false), false);
                placed++;
            }
        }

        /// <summary>More blighted, dead trees toward the bandit camp (the war-poisoned side).</summary>
        static TreeKind PickTree(Vector3 p, bool inRing)
        {
            float blight = 0.06f + 0.4f * Mathf.Clamp01(Vector3.Dot(Flat(p - squareCenter), campDirection) / 40f);
            float roll = Next();
            if (roll < blight) return TreeKind.Dead;
            return roll < blight + (inRing ? 0.5f : 0.3f) ? TreeKind.Pine : TreeKind.Oak;
        }

        static void Tree(Transform parent, Vector3 position, TreeKind kind, bool farEdge)
        {
            float height = kind switch
            {
                TreeKind.Pine => Range(6f, 7.8f),
                TreeKind.Dead => Range(4.4f, 5.6f),
                _ => Range(4.6f, 6.2f),
            };
            float aspect = kind switch { TreeKind.Pine => 0.5f, TreeKind.Dead => 0.7f, _ => 0.8f };
            string material = kind switch { TreeKind.Pine => "Env_Pine", TreeKind.Dead => "Env_DeadTree", _ => "Env_Oak" };

            var root = new GameObject($"Tree_{kind}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var trunk = root.AddComponent<CapsuleCollider>();
            trunk.radius = kind == TreeKind.Pine ? 0.25f : 0.32f;
            trunk.height = 3f;
            trunk.center = new Vector3(0f, 1.5f, 0f);

            var size = new Vector3(height * aspect * (Next() < 0.5f ? -1f : 1f), height, 1f); // random mirror for variety
            var body = Sprite("Body", root.transform, size, material, ShadowCastingMode.Off);
            var shadow = farEdge ? null : Sprite("Shadow", root.transform, size, material, ShadowCastingMode.ShadowsOnly);

            // Flat canopy for the minimap (the billboard is edge-on from above).
            float canopy = Mathf.Abs(size.x) * (kind == TreeKind.Dead ? 0.6f : 0.9f);
            var map = Part("MapCanopy", root.transform, Vector3.up * 6f, Quaternion.identity, new Vector3(canopy, 1f, canopy), flatQuad,
                materials[kind == TreeKind.Dead ? "Env_MapCanopyDead" : "Env_MapCanopy"]);
            map.layer = minimapLayer;
            var mapRenderer = map.GetComponent<MeshRenderer>();
            mapRenderer.shadowCastingMode = ShadowCastingMode.Off;
            mapRenderer.receiveShadows = false;

            Billboard(root, body.transform, shadow != null ? shadow.transform : null);
            trees.Add(position);
        }

        // ---------- Bushes, rocks, ground cover ----------

        static void BuildBushesAndRocks(Transform dressing)
        {
            var bushes = Group("Bushes", dressing);
            int placed = 0;
            for (int attempt = 0; attempt < 500 && placed < 34; attempt++)
            {
                var p = new Vector3(Range(-36f, 36f), 0f, Range(-36f, 36f));
                if (!IsClear(p, 0.8f)) continue;
                float height = Range(0.9f, 1.4f);
                var root = new GameObject("Bush");
                root.transform.SetParent(bushes, false);
                root.transform.position = p;
                var size = new Vector3(height * 40f / 28f * (Next() < 0.5f ? -1f : 1f), height, 1f);
                var body = Sprite("Body", root.transform, size, "Env_Bush", ShadowCastingMode.Off);
                var shadow = Sprite("Shadow", root.transform, size, "Env_Bush", ShadowCastingMode.ShadowsOnly);
                Billboard(root, body.transform, shadow.transform);
                placed++;
            }

            var rockGroup = Group("Rocks", dressing);
            placed = 0;
            for (int attempt = 0; attempt < 500 && placed < 30; attempt++)
            {
                var p = new Vector3(Range(-46f, 46f), 0f, Range(-46f, 46f));
                bool outer = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) > 32f;
                float size = outer ? Range(1.4f, 3.2f) : Range(0.5f, 1.5f);
                if (!IsClear(p, size)) continue;
                var rock = new GameObject("Rock", typeof(MeshFilter), typeof(MeshRenderer));
                rock.transform.SetParent(rockGroup, false);
                rock.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Range(0f, 360f), 0f));
                rock.transform.localScale = new Vector3(size, size * Range(0.6f, 1f), size * Range(0.8f, 1.2f));
                var mesh = rocks[random.Next(rocks.Length)];
                rock.GetComponent<MeshFilter>().sharedMesh = mesh;
                rock.GetComponent<MeshRenderer>().sharedMaterial = materials["Env_Rock"];
                if (size > 0.9f)
                {
                    var collider = rock.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = true;
                }
                KeepCircle(p, size * 0.6f);
                placed++;
            }
        }

        static void BuildGroundCover(Transform dressing)
        {
            var cover = Group("GroundCover", dressing);
            // Ground cover may sit right up to walls, but never on roads, the square or the field.
            var hard = new List<Bounds>(keepClear);
            keepClear.Clear();
            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.name == "Ground" || collider.transform.IsChildOf(dressing)) continue;
                var b = collider.bounds;
                b.Expand(new Vector3(0.3f, 0f, 0.3f));
                keepClear.Add(b);
            }
            KeepCircle(squareCenter, squareRadius + 0.3f);
            if (FieldGate(out var field).HasValue)
                keepClear.Add(new Bounds((field.min + field.max) * 0.5f, new Vector3(field.max.x - field.min.x + 0.6f, 50f, field.max.z - field.min.z + 0.6f)));

            int tufts = 0, flowers = 0;
            for (int attempt = 0; attempt < 2400 && (tufts < 420 || flowers < 70); attempt++)
            {
                var p = new Vector3(Range(-47f, 47f), 0f, Range(-47f, 47f));
                if (!IsClear(p, 0f)) continue;
                bool flower = flowers < 70 && Next() < 0.16f;
                if (!flower && tufts >= 420) continue;
                float height = flower ? Range(0.35f, 0.5f) : Range(0.32f, 0.55f);
                var size = new Vector3(height * 16f / 12f * (Next() < 0.5f ? -1f : 1f), height, 1f);
                var tuft = Sprite(flower ? "Flowers" : "Grass", cover, size, flower ? "Env_Flowers" : "Env_GrassTuft", ShadowCastingMode.Off);
                tuft.transform.position = p;
                Billboard(tuft, tuft.transform, null);
                if (flower) flowers++;
                else tufts++;
            }
            keepClear.Clear();
            keepClear.AddRange(hard);
        }

        // ---------- Lighting ----------

        static void SetUpLighting()
        {
            // Overcast-gritty daylight: warm sun, cool shadows, distance haze that hides the world's edge.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.6f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.43f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.6f, 0.63f, 0.62f);
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 110f;

            Light sun = RenderSettings.sun;
            if (sun == null)
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (sun != null)
            {
                sun.color = new Color(1f, 0.94f, 0.84f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                RenderSettings.sun = sun;
            }

            var main = Camera.main;
            if (main != null)
            {
                main.cullingMask &= ~(1 << minimapLayer);
                EditorUtility.SetDirty(main);
            }
        }

        // ---------- Builders ----------

        static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>A mesh object at a position/rotation in the parent's space.</summary>
        /// <summary>
        /// Gives every roof a collider (scenes dressed before roofs had them). Without one the camera could slip
        /// inside a roof above the walls and fill the screen. Safe to run again.
        /// </summary>
        [MenuItem("Beast/Setup/Repair/Add Roof Colliders", priority = 100)]
        public static void AddRoofColliders()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            int added = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.name != "Roof" || renderer.GetComponent<Collider>() != null) continue;
                renderer.gameObject.AddComponent<MeshCollider>();
                added++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Roof colliders: {added} added.");
        }

        static GameObject Part(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        /// <summary>A textured box (world-UV mesh) at a position in the parent's space (metres in holders).</summary>
        static GameObject Block(string name, Transform parent, Vector3 localPosition, Vector3 size, string material, float metresPerTile, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            string key = $"Box_{name}_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}";
            go.GetComponent<MeshFilter>().sharedMesh = EnvironmentArtGenerator.SaveMesh(key, m => EnvironmentArtGenerator.BuildWorldUvBox(m, size, metresPerTile));
            go.GetComponent<MeshRenderer>().sharedMaterial = materials[material];
            return go;
        }

        /// <summary>A thin textured quad (doors, windows, notices) whose visible side faces 'outward'.</summary>
        static void Decal(string name, Transform holder, Vector3 localPosition, Vector3 outward, Vector2 size, string material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(holder, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(-outward); // a Quad's visible side faces its local -Z
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[material];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Place(GameObject primitive, Transform parent, Vector3 worldPosition, Vector3 scale, string material)
        {
            primitive.transform.SetParent(parent, true);
            primitive.transform.position = worldPosition;
            primitive.transform.localScale = scale;
            primitive.GetComponent<MeshRenderer>().sharedMaterial = materials[material];
        }

        static GameObject Sprite(string name, Transform parent, Vector3 size, string material, ShadowCastingMode shadows)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = billboardLayer;
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = billboardQuad;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[material];
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = renderer.sharedMaterial.shader.name == SpriteShadowSetup.ShaderName;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        static void Billboard(GameObject host, Transform body, Transform shadow)
        {
            var billboard = host.AddComponent<BillboardSprite>();
            var so = new SerializedObject(billboard);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Math ----------

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static float Next() => (float)random.NextDouble();
        static float Range(float min, float max) => min + (max - min) * Next();
        static float Jitter(float amount) => (Next() * 2f - 1f) * amount;

        static bool TooClose(Vector3 p, float distance)
        {
            foreach (var tree in trees)
                if (Flat(tree - p).sqrMagnitude < distance * distance) return true;
            return false;
        }

        static Vector3 ClampToWorld(Vector3 p, float extent)
        {
            float scale = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) / extent;
            return scale > 1f ? p / scale : p;
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
