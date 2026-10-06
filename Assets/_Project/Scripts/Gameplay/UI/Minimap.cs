using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Beast.Gameplay
{
    /// <summary>
    /// Top-right minimap (M toggles a large map). A top-down orthographic camera renders the world
    /// (characters excluded) into a small texture; markers are drawn on top: player arrow, enemies,
    /// NPCs with quest markers, the contracts board, the bed, north, and the tracked quest's target
    /// (pinned to the edge when it's off the map). The map rotates with the camera, like Genshin's.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        [SerializeField, Min(64)] int resolution = 256;
        [SerializeField, Tooltip("World metres shown across the minimap.")] float smallWorldSize = 45f;
        [SerializeField, Tooltip("World metres shown across the large map (M).")] float largeWorldSize = 140f;
        [SerializeField] float cameraHeight = 80f;
        [SerializeField] Color background = new(0.1f, 0.12f, 0.1f);

        static readonly Color EnemyColor = new(0.95f, 0.3f, 0.25f);
        static readonly Color NpcColor = new(1f, 0.85f, 0.35f);
        static readonly Color BoardColor = new(0.85f, 0.65f, 0.4f);
        static readonly Color BedColor = new(0.7f, 0.8f, 1f);

        Camera mapCamera;
        RenderTexture texture;
        Transform player;
        Transform mainCamera;
        QuestTracker tracker;
        GameStateService state;
        InputAction mapAction;
        bool large;
        Rect mapRect;

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
            if (Camera.main != null) mainCamera = Camera.main.transform;
            tracker = FindFirstObjectByType<QuestTracker>();
            state = Services.Get<GameStateService>();
            mapAction = Services.Get<InputService>().Map;
            CreateCamera();
        }

        void CreateCamera()
        {
            texture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32) { name = "Minimap" };

            var cameraObject = new GameObject("MinimapCamera");
            cameraObject.transform.SetParent(transform, false);
            mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = background;
            mapCamera.nearClipPlane = 1f;
            mapCamera.farClipPlane = cameraHeight + 50f;
            mapCamera.allowHDR = false;
            mapCamera.allowMSAA = false;
            mapCamera.depth = -10f;
            mapCamera.targetTexture = texture;

            // Characters are drawn as markers instead (their sprites are edge-on from above).
            // Billboard environment sprites are edge-on too; they draw a flat "MinimapOnly" canopy instead.
            int hidden = CombatLayers.PlayerMask | CombatLayers.EnemyMask;
            int npcLayer = LayerMask.NameToLayer("NPC");
            if (npcLayer >= 0) hidden |= 1 << npcLayer;
            int billboardLayer = LayerMask.NameToLayer(EnvironmentLayers.Billboards);
            if (billboardLayer >= 0) hidden |= 1 << billboardLayer;
            mapCamera.cullingMask = ~hidden;

            // ...and those canopies must never show in the main view.
            int mapOnlyLayer = LayerMask.NameToLayer(EnvironmentLayers.MinimapOnly);
            if (mapOnlyLayer >= 0 && Camera.main != null) Camera.main.cullingMask &= ~(1 << mapOnlyLayer);

            // Cheap render: no shadows or post-processing on the map.
            var data = mapCamera.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
        }

        void OnDestroy()
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        void Update()
        {
            if (mapAction.WasPressedThisFrame() && state.Current == GameState.Playing) large = !large;
        }

        void LateUpdate()
        {
            if (mapCamera == null || player == null) return;

            // Only render while playing (menus and dialogue cover the screen anyway).
            mapCamera.enabled = state.Current == GameState.Playing;
            mapCamera.orthographicSize = (large ? largeWorldSize : smallWorldSize) * 0.5f;
            float yaw = mainCamera != null ? mainCamera.eulerAngles.y : 0f;
            mapCamera.transform.SetPositionAndRotation(player.position + Vector3.up * EffectiveHeight, Quaternion.Euler(90f, yaw, 0f));
        }

        /// <summary>
        /// Distance fog is global, so the map camera stays inside the fog-free range (still well above roofs and treetops);
        /// otherwise the map would be washed out.
        /// </summary>
        float EffectiveHeight => RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear
            ? Mathf.Clamp(RenderSettings.fogStartDistance - 4f, 18f, cameraHeight)
            : cameraHeight;

        void OnGUI()
        {
            if (mapCamera == null || player == null || state.Current != GameState.Playing) return;
            UITheme.Begin();

            if (large)
            {
                UITheme.Backdrop(0.45f);
                float size = Mathf.Min(UITheme.Height - 160f, 760f);
                mapRect = new Rect((UITheme.Width - size) * 0.5f, (UITheme.Height - size) * 0.5f, size, size);
            }
            else
            {
                float size = GameHud.MinimapSize;
                mapRect = new Rect(UITheme.Width - size - GameHud.MinimapMargin, GameHud.MinimapMargin, size, size);
            }

            UITheme.Panel(new Rect(mapRect.x - 6f, mapRect.y - 6f, mapRect.width + 12f, mapRect.height + 12f));
            GUI.DrawTexture(mapRect, texture);

            DrawMarkers();
            DrawPlayerArrow();

            if (large)
            {
                GUI.Label(new Rect(mapRect.x, mapRect.y - 44f, mapRect.width, 34f), "Map", UITheme.Title);
                DrawLegend(new Rect(mapRect.x, mapRect.yMax + 14f, mapRect.width, 24f));
            }
            else
            {
                UITheme.KeyHint(mapRect.x + 4f, mapRect.yMax - 28f, "M", "Map", 0.8f);
            }
        }

        void DrawLegend(Rect row)
        {
            float x = row.x;
            x = LegendEntry(x, row.y, UITheme.ArrowIcon, Color.white, "You");
            x = LegendEntry(x, row.y, UITheme.DiamondIcon, UITheme.Gold, "Tracked quest");
            x = LegendEntry(x, row.y, UITheme.CircleIcon, NpcColor, "People");
            x = LegendEntry(x, row.y, UITheme.CircleIcon, EnemyColor, "Enemies");
            x = LegendEntry(x, row.y, UITheme.BoxIcon, BoardColor, "Contracts");
            x = LegendEntry(x, row.y, UITheme.BoxIcon, BedColor, "Bed");
            UITheme.KeyHint(Mathf.Max(x, row.xMax - 120f), row.y, "M", "Close", 0.9f);
        }

        static float LegendEntry(float x, float y, Texture2D icon, Color color, string label)
        {
            UITheme.DrawIcon(new Rect(x, y + 5f, 14f, 14f), icon, color);
            float width = UITheme.Small.CalcSize(new GUIContent(label)).x;
            UITheme.ShadowLabel(new Rect(x + 20f, y, width + 4f, 24f), label, UITheme.Small, UITheme.Text);
            return x + width + 40f;
        }

        void DrawMarkers()
        {
            // North
            Marker(player.position + Vector3.forward * 1000f, null, "N", Color.white, 18f, clampToEdge: true);

            foreach (var interactable in Interactable.Active)
            {
                switch (interactable)
                {
                    case DialogueSpeaker speaker:
                        char quest = tracker != null ? speaker.QuestMarker(tracker.Log) : '\0';
                        if (quest != '\0') Marker(speaker.transform.position, null, quest.ToString(), UITheme.Gold, 22f, false);
                        else Marker(speaker.transform.position, UITheme.CircleIcon, null, NpcColor, 12f, false);
                        break;
                    case ContractBoard board:
                        Marker(board.transform.position, UITheme.BoxIcon, null, BoardColor, 14f, false);
                        break;
                    case SleepSpot bed:
                        Marker(bed.transform.position, UITheme.BoxIcon, null, BedColor, 14f, false);
                        break;
                }
            }

            foreach (var enemy in EnemyController.Active)
                if (!enemy.Combatant.IsDead && enemy.Data != null && !enemy.Data.IsTrainingDummy)
                    Marker(enemy.transform.position, UITheme.CircleIcon, null, EnemyColor, 9f, false);

            if (tracker != null && tracker.HasTarget)
                Marker(tracker.TargetPosition, UITheme.DiamondIcon, null, UITheme.Gold, 16f, clampToEdge: true);
        }

        /// <summary>Draws an icon or a letter where a world position falls on the map. Off-map: hidden, or pinned to the edge.</summary>
        void Marker(Vector3 world, Texture2D icon, string glyph, Color color, float size, bool clampToEdge)
        {
            Vector3 viewport = mapCamera.WorldToViewportPoint(world);
            bool inside = viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
            if (!inside && !clampToEdge) return;

            if (!inside)
            {
                // Pin along the line from the map centre toward the target.
                var direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                float scale = 0.45f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y), 0.0001f);
                viewport = new Vector3(0.5f + direction.x * scale, 0.5f + direction.y * scale, viewport.z);
            }

            float x = mapRect.x + viewport.x * mapRect.width;
            float y = mapRect.y + (1f - viewport.y) * mapRect.height;
            var rect = new Rect(x - size * 0.5f, y - size * 0.5f, size, size);
            if (icon != null)
            {
                UITheme.DrawIcon(new Rect(rect.x + 1f, rect.y + 1f, size, size), icon, new Color(0f, 0f, 0f, 0.6f));
                UITheme.DrawIcon(rect, icon, color);
            }
            else
            {
                UITheme.ShadowLabel(new Rect(x - 15f, y - 15f, 30f, 30f), $"<b>{glyph}</b>", UITheme.BodyCenter, color);
            }
        }

        void DrawPlayerArrow()
        {
            // Map "up" is the camera's forward; rotate the arrow by the player's yaw relative to it.
            float cameraYaw = mainCamera != null ? mainCamera.eulerAngles.y : 0f;
            float angle = player.eulerAngles.y - cameraYaw;
            var center = mapRect.center;
            var matrix = GUI.matrix;
            UITheme.RotateAround(angle, center);
            const float size = 20f;
            UITheme.DrawIcon(new Rect(center.x - size * 0.5f + 1f, center.y - size * 0.5f + 1f, size, size), UITheme.ArrowIcon, new Color(0f, 0f, 0f, 0.7f));
            UITheme.DrawIcon(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), UITheme.ArrowIcon, Color.white);
            GUI.matrix = matrix;
        }
    }
}
