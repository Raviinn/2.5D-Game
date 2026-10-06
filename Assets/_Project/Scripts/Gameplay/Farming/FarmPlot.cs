using System;
using Beast.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Beast.Gameplay
{
    /// <summary>
    /// A grid of farm tiles (origin = the plot's corner, extending along +X and +Z).
    /// The tile in front of the player is the interaction target: till → plant → water → harvest.
    /// Crops grow on each new day they were watered, wilt then die when neglected, and everything is saved.
    /// </summary>
    public sealed class FarmPlot : Interactable, ISaveable
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "homestead.field";
        [SerializeField, Min(1)] int width = 6;
        [SerializeField, Min(1)] int depth = 6;
        [SerializeField, Min(0.25f)] float tileSize = 1f;
        [SerializeField, Tooltip("How far in front of the player the targeted tile is.")] float reach = 1.1f;

        [Header("Visuals")]
        [SerializeField, Tooltip("Alpha-clipped sprite material (Sprite_Lit).")] Material cropMaterial;
        [SerializeField, Tooltip("Optional furrowed-soil texture for tilled tiles. The dry/wet colours tint it.")] Texture2D soilTexture;
        [SerializeField] Color drySoil = new(0.46f, 0.31f, 0.19f);
        [SerializeField] Color wetSoil = new(0.26f, 0.17f, 0.1f);

        struct Tile
        {
            public bool Tilled;
            public CropData Crop;
            public int Growth;      // watered days grown
            public bool Watered;    // watered today
            public int DryDays;     // unwatered days in a row
            public bool Dead;
        }

        [Serializable]
        sealed class State
        {
            public bool[] tilled;
            public string[] crops;
            public int[] growth;
            public bool[] watered;
            public int[] dryDays;
            public bool[] dead;
        }

        static Material soilMaterial;
        static Texture2D highlightTexture;

        Tile[] tiles;
        MeshRenderer[] soilRenderers;
        MeshRenderer[] cropRenderers;
        MeshRenderer highlight;
        MaterialPropertyBlock block;
        Transform cameraTransform;
        PlayerStats playerStats;
        int targetTile = -1;

        public string SaveId => saveId;
        /// <summary>Centre of the field in world space (quest waypoints, minimap).</summary>
        public Vector3 WorldCenter => transform.TransformPoint(new Vector3(width * tileSize * 0.5f, 0f, depth * tileSize * 0.5f));
        /// <summary>Holding Interact works tile after tile while walking along a row.</summary>
        public override bool RepeatsWhileHeld => true;

        void Awake()
        {
            tiles = new Tile[width * depth];
            block = new MaterialPropertyBlock();
            BuildVisuals();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus<DayPassedEvent>.Subscribe(OnDayPassed);
            EventBus<WeatherChangedEvent>.Subscribe(OnWeatherChanged);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus<DayPassedEvent>.Unsubscribe(OnDayPassed);
            EventBus<WeatherChangedEvent>.Unsubscribe(OnWeatherChanged);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start()
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            var player = GameObject.FindWithTag("Player");
            if (player != null) playerStats = player.GetComponent<PlayerStats>();
        }

        // ---------- Interaction ----------

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            targetTile = TileAt(interactor.transform.position + interactor.transform.forward * reach);
            if (targetTile < 0) return false;

            Describe(targetTile, interactor, out prompt.Text, out prompt.CanInteract, out prompt.ActionKey);
            prompt.Distance = Vector3.Distance(interactor.transform.position, TileWorldCenter(targetTile));
            return true;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            int index = TileAt(interactor.transform.position + interactor.transform.forward * reach);
            if (index < 0) return;

            ref var tile = ref tiles[index];
            if (!tile.Tilled)
            {
                tile.Tilled = true;
            }
            else if (tile.Crop == null)
            {
                var seed = interactor.TryGetComponent(out PlayerFarmer farmer) ? farmer.SelectedSeed : null;
                var crop = CropCatalog.ForSeed(seed);
                if (crop == null) return;
                if (playerStats != null && Random.value < playerStats.Get(StatType.SeedSaveChance))
                    EventBus<HudMessageEvent>.Raise(new HudMessageEvent("Seed saved!"));
                else if (!interactor.Inventory.Remove(seed, 1))
                    return;
                tile = new Tile { Tilled = true, Crop = crop };
            }
            else if (tile.Dead)
            {
                tile.Crop = null;
                tile.Dead = false;
            }
            else if (IsRipe(tile))
            {
                Harvest(ref tile, interactor.Inventory);
            }
            else if (!tile.Watered)
            {
                tile.Watered = true;
            }
            if (tile.Tilled && IsRaining) tile.Watered = true; // soil worked in the rain is already wet
            RefreshTile(index);
        }

        void Describe(int index, PlayerInteractor interactor, out string text, out bool canInteract, out string action)
        {
            var tile = tiles[index];
            canInteract = true;
            action = !tile.Tilled ? "till" : tile.Crop == null ? "plant" : tile.Dead ? "clear" : IsRipe(tile) ? "harvest" : "water";

            if (!tile.Tilled)
            {
                text = "Till soil";
            }
            else if (tile.Crop == null)
            {
                var farmer = interactor.GetComponent<PlayerFarmer>();
                var seed = farmer != null ? farmer.SelectedSeed : null;
                if (seed == null)
                {
                    text = "Tilled soil (no seeds)";
                    canInteract = false;
                }
                else
                {
                    text = $"Plant {seed.DisplayName} (×{interactor.Inventory.CountOf(seed)})" +
                           (farmer.HasSeedChoice ? "   ·   V: switch seeds" : string.Empty);
                }
            }
            else if (tile.Dead)
            {
                text = $"Clear dead {tile.Crop.DisplayName}";
            }
            else if (IsRipe(tile))
            {
                text = $"Harvest {tile.Crop.DisplayName}";
            }
            else
            {
                string progress = $"day {tile.Growth}/{tile.Crop.TotalGrowDays}";
                if (!tile.Watered)
                {
                    text = IsWilted(tile) ? $"Water wilting {tile.Crop.DisplayName}!" : $"Water {tile.Crop.DisplayName} ({progress})";
                }
                else
                {
                    text = $"{tile.Crop.DisplayName}: watered, growing ({progress})";
                    canInteract = false;
                }
            }
        }

        void Harvest(ref Tile tile, Inventory inventory)
        {
            var crop = tile.Crop;
            int amount = Random.Range(crop.ProduceMin, Mathf.Max(crop.ProduceMin, crop.ProduceMax) + 1);
            if (playerStats != null && Random.value < playerStats.Get(StatType.ExtraYieldChance)) amount++;
            int leftover = inventory.Add(crop.Produce, amount);
            if (leftover == amount)
            {
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent("Bag is full"));
                return;
            }
            // Partly full bag: what didn't fit drops at the player's feet instead of vanishing.
            if (leftover > 0) ItemPickup.SpawnItem(inventory.transform.position, crop.Produce, leftover, 0.8f);
            EventBus<CropHarvestedEvent>.Raise(new CropHarvestedEvent(crop, amount));

            if (crop.RegrowDays > 0)
            {
                tile.Growth = Mathf.Max(0, crop.TotalGrowDays - crop.RegrowDays);
                tile.DryDays = 0;
            }
            else
            {
                tile.Crop = null;
                tile.Growth = 0;
            }
        }

        // ---------- Daily growth ----------

        void OnDayPassed(DayPassedEvent evt)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                ref var tile = ref tiles[i];
                if (tile.Crop != null && !tile.Dead && !IsRipe(tile))
                {
                    if (tile.Watered)
                    {
                        tile.Growth++;
                        tile.DryDays = 0;
                    }
                    else if (++tile.DryDays >= tile.Crop.DieAfterDryDays + DroughtTolerance)
                    {
                        tile.Dead = true;
                    }
                }
                tile.Watered = false; // soil dries overnight...
                RefreshTile(i);
            }
            // ...unless the new day is rainy. Asks the weather for this day directly, so it doesn't matter
            // whether the weather system has handled midnight yet.
            if (Services.TryGet(out WeatherSystem weather) && weather.Roll(evt.Day) == Weather.Rain) WaterAll();
        }

        void OnWeatherChanged(WeatherChangedEvent evt)
        {
            if (evt.Current == Weather.Rain) WaterAll();
        }

        bool IsRaining => Services.TryGet(out WeatherSystem weather) && weather.IsRaining;

        /// <summary>Rain: every tilled tile counts as watered today.</summary>
        void WaterAll()
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                if (!tiles[i].Tilled || tiles[i].Watered) continue;
                tiles[i].Watered = true;
                RefreshTile(i);
            }
        }

        static bool IsRipe(in Tile tile) => tile.Crop != null && !tile.Dead && tile.Growth >= tile.Crop.TotalGrowDays;

        bool IsWilted(in Tile tile) =>
            tile.Crop != null && !tile.Dead && !tile.Watered && !IsRipe(tile) && tile.DryDays >= tile.Crop.WiltAfterDryDays + DroughtTolerance;

        /// <summary>Extra dry days from the player's Farming perks.</summary>
        int DroughtTolerance => playerStats != null ? Mathf.RoundToInt(playerStats.Get(StatType.DroughtTolerance)) : 0;

        // ---------- Grid ----------

        int TileAt(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            int x = Mathf.FloorToInt(local.x / tileSize);
            int z = Mathf.FloorToInt(local.z / tileSize);
            if (x < 0 || z < 0 || x >= width || z >= depth || Mathf.Abs(local.y) > 2f) return -1;
            return z * width + x;
        }

        Vector3 TileLocalCenter(int index) =>
            new((index % width + 0.5f) * tileSize, 0f, (index / width + 0.5f) * tileSize);

        Vector3 TileWorldCenter(int index) => transform.TransformPoint(TileLocalCenter(index));

        // ---------- Visuals ----------

        void BuildVisuals()
        {
            if (soilMaterial == null)
            {
                soilMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
                soilMaterial.SetFloat("_Smoothness", 0f);
            }
            if (soilTexture != null) soilMaterial.SetTexture(BaseMapId, soilTexture);
            var cropMesh = SpriteQuad.Get(new Vector2(0.5f, 0f), 0.6f);

            soilRenderers = new MeshRenderer[tiles.Length];
            cropRenderers = new MeshRenderer[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                Vector3 center = TileLocalCenter(i);
                soilRenderers[i] = CreateFlatQuad($"Soil_{i}", center + Vector3.up * 0.03f, tileSize * 0.94f, soilMaterial);

                var crop = new GameObject($"Crop_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                crop.transform.SetParent(transform, false);
                crop.transform.localPosition = center + Vector3.up * 0.03f;
                crop.GetComponent<MeshFilter>().sharedMesh = cropMesh;
                var cropRenderer = crop.GetComponent<MeshRenderer>();
                cropRenderer.sharedMaterial = cropMaterial;
                cropRenderer.enabled = false;
                cropRenderers[i] = cropRenderer;
            }

            highlight = CreateFlatQuad("TileHighlight", Vector3.up * 0.05f, tileSize, cropMaterial);
            block.Clear();
            block.SetTexture(BaseMapId, GetHighlightTexture());
            block.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));
            block.SetColor(BaseColorId, new Color(1f, 0.95f, 0.6f));
            block.SetColor(EmissionColorId, new Color(0.5f, 0.45f, 0.2f));
            highlight.SetPropertyBlock(block);
        }

        MeshRenderer CreateFlatQuad(string objectName, Vector3 localPosition, float size, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = objectName;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * size;

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.enabled = false;
            return renderer;
        }

        void RefreshTile(int index)
        {
            var tile = tiles[index];

            var soil = soilRenderers[index];
            soil.enabled = tile.Tilled;
            if (tile.Tilled)
            {
                soil.GetPropertyBlock(block);
                block.SetColor(BaseColorId, tile.Watered ? wetSoil : drySoil);
                soil.SetPropertyBlock(block);
            }

            var cropRenderer = cropRenderers[index];
            var crop = tile.Crop;
            cropRenderer.enabled = crop != null && crop.Sheet != null;
            if (!cropRenderer.enabled) return;

            int frame = tile.Dead ? crop.DeadFrame : IsRipe(tile) ? crop.RipeFrame : IsWilted(tile) ? crop.WiltedFrame : crop.StageFor(tile.Growth);
            float frameWidth = crop.FrameSize / (float)crop.Sheet.width;

            cropRenderer.transform.localScale = Vector3.one * crop.WorldSize;
            cropRenderer.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, crop.Sheet);
            block.SetVector(BaseMapStId, new Vector4(frameWidth, 1f, frame * frameWidth, 0f));
            cropRenderer.SetPropertyBlock(block);
        }

        void RefreshAll()
        {
            for (int i = 0; i < tiles.Length; i++) RefreshTile(i);
        }

        void LateUpdate()
        {
            if (cameraTransform != null)
            {
                Vector3 forward = cameraTransform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    var rotation = Quaternion.LookRotation(forward);
                    foreach (var crop in cropRenderers)
                        if (crop.enabled) crop.transform.rotation = rotation;
                }
            }

            highlight.enabled = IsFocused && targetTile >= 0;
            if (highlight.enabled) highlight.transform.localPosition = TileLocalCenter(targetTile) + Vector3.up * 0.05f;
        }

        static Texture2D GetHighlightTexture()
        {
            if (highlightTexture != null) return highlightTexture;

            const int size = 32;
            highlightTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = x < 2 || y < 2 || x >= size - 2 || y >= size - 2
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
            highlightTexture.SetPixels32(pixels);
            highlightTexture.Apply();
            return highlightTexture;
        }

        // ---------- Save ----------

        public string CaptureState()
        {
            int n = tiles.Length;
            var state = new State
            {
                tilled = new bool[n], crops = new string[n], growth = new int[n],
                watered = new bool[n], dryDays = new int[n], dead = new bool[n],
            };
            for (int i = 0; i < n; i++)
            {
                var tile = tiles[i];
                state.tilled[i] = tile.Tilled;
                state.crops[i] = tile.Crop != null ? tile.Crop.Id : string.Empty;
                state.growth[i] = tile.Growth;
                state.watered[i] = tile.Watered;
                state.dryDays[i] = tile.DryDays;
                state.dead[i] = tile.Dead;
            }
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Services.TryGet(out GameDatabase database);

            int count = Mathf.Min(tiles.Length, state.tilled?.Length ?? 0);
            Array.Clear(tiles, 0, tiles.Length);
            for (int i = 0; i < count; i++)
            {
                CropData crop = null;
                string id = state.crops[i];
                if (!string.IsNullOrEmpty(id) && (database == null || !database.TryGet(id, out crop)))
                    Debug.LogWarning($"[Farm] Saved crop '{id}' no longer exists; tile cleared.");

                tiles[i] = new Tile
                {
                    Tilled = state.tilled[i],
                    Crop = crop,
                    Growth = state.growth[i],
                    Watered = state.watered[i],
                    DryDays = state.dryDays[i],
                    Dead = crop != null && state.dead[i],
                };
            }
            RefreshAll();
        }

        void OnDrawGizmos()
        {
            // Shows the plot's footprint in the Scene view.
            Gizmos.color = new Color(0.5f, 0.35f, 0.15f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int x = 0; x <= width; x++)
                Gizmos.DrawLine(new Vector3(x * tileSize, 0.05f, 0f), new Vector3(x * tileSize, 0.05f, depth * tileSize));
            for (int z = 0; z <= depth; z++)
                Gizmos.DrawLine(new Vector3(0f, 0.05f, z * tileSize), new Vector3(width * tileSize, 0.05f, z * tileSize));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
