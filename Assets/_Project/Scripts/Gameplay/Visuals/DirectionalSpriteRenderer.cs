using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Renders a 2D character in the 3D world:
    /// - the Body quad faces the camera (upright, optionally tilted with the camera's pitch)
    /// - the frame's row is picked from the character's facing relative to the camera (8 directions)
    /// - a Shadow quad faces the sun and only casts shadows, so shadows never go paper-thin
    /// - attack clips follow the AttackExecutor's real timings
    /// - with the "Beast/Sprite Lit" material the sprite receives real per-pixel shadows (minus its own shadow quad);
    ///   with a plain URP/Lit material it falls back to darkening the whole sprite when the sun is blocked
    /// Uses a MaterialPropertyBlock, so one shared material serves every character.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the camera has moved this frame
    public sealed class DirectionalSpriteRenderer : MonoBehaviour
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SelfShadowOffsetId = Shader.PropertyToID("_SelfShadowOffset");
        static readonly RaycastHit[] hitBuffer = new RaycastHit[8];
        /// <summary>Sun checks at knee, chest and head height (fraction of sprite height): partial shade = partial darkening.</summary>
        static readonly float[] shadeSampleHeights = { 0.25f, 0.55f, 0.85f };

        [SerializeField] DirectionalSpriteSheet sheet;
        [SerializeField] MeshRenderer body;
        [SerializeField, Tooltip("Optional shadow-only quad that turns toward the sun.")] MeshRenderer shadow;
        [SerializeField, Range(0f, 1f), Tooltip("0 = stays upright. 1 = fully matches the camera's pitch.")]
        float tiltTowardCamera = 0.3f;
        [SerializeField, Range(0f, 1f), Tooltip("Bends the lighting normal upward so sprites stay evenly lit from every angle.")]
        float normalBend = 0.6f;

        [Header("World Shadows")]
        [SerializeField, Tooltip("Fallback for materials without per-pixel sprite shadows (anything but Beast/Sprite Lit): " +
                                 "the sprite darkens when the sun is blocked, checked at knee, chest and head height.")]
        bool sampleWorldShadow = true;
        [SerializeField, Tooltip("Environment layers that can put the character in shade.")]
        LayerMask shadowOccluders = 1; // Default layer
        [SerializeField, Tooltip("Other characters' shadows darken this sprite too (never its own).")]
        bool charactersCastShade = true;
        [SerializeField, Range(0f, 1f)] float shadowedBrightness = 0.55f;

        ICharacterAnimationSource source;
        Transform facingRoot;
        Transform cameraTransform;
        Transform sun;
        MaterialPropertyBlock block;
        CharacterAnim currentAnim;
        float clipTime;
        float brightness = 1f;
        int occluderMask;
        bool perPixelShadows;
        Texture2D textureOverride;

        /// <summary>0 = facing the camera, 2 = facing screen-right, 4 = facing away, 6 = facing screen-left.</summary>
        public int CurrentDirection { get; private set; }
        public DirectionalSpriteSheet Sheet => sheet;
        /// <summary>The character this sprite belongs to (the object that faces and moves).</summary>
        public Transform Character => facingRoot;
        /// <summary>The clip being shown (null before the first frame).</summary>
        public DirectionalSpriteSheet.Clip CurrentClip { get; private set; }

        /// <summary>
        /// Raised when a character shows a new animation frame: (renderer, animation, frame within the clip).
        /// Sounds listen (footsteps on contact frames, swings on the strike frame).
        /// </summary>
        public static event System.Action<DirectionalSpriteRenderer, CharacterAnim, int> FrameShown;

        int lastEventFrame = -1;
        CharacterAnim lastEventAnim;

        /// <summary>
        /// Draws with this texture instead of the sheet's own (same grid and clips), e.g. the player's
        /// customised placeholder. Null goes back to the sheet's texture.
        /// </summary>
        public void SetTextureOverride(Texture2D texture) => textureOverride = texture;

        void Awake()
        {
            source = GetComponentInParent<ICharacterAnimationSource>();
            facingRoot = source is Component component ? component.transform : transform.parent != null ? transform.parent : transform;
            block = new MaterialPropertyBlock();

            if (sheet == null || body == null)
            {
                Debug.LogError($"[Sprites] {name} needs a sheet and a body renderer.", this);
                enabled = false;
                return;
            }

            var mesh = SpriteQuad.Get(sheet.Pivot, normalBend);
            var scale = new Vector3(sheet.WorldSize.x, sheet.WorldSize.y, 1f);
            SetUpQuad(body, mesh, scale);
            if (shadow != null) SetUpQuad(shadow, mesh, scale);

            // Beast/Sprite Lit receives per-pixel shadows while skipping its own shadow quad. Plain URP/Lit can't,
            // so there receiving is switched off on the material and UpdateBrightness() shades the whole sprite instead.
            perPixelShadows = body.sharedMaterial != null && body.sharedMaterial.HasProperty(SelfShadowOffsetId);
            body.receiveShadows = perPixelShadows;
            body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (shadow != null) shadow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        void Start()
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            sun = FindSun();
            occluderMask = shadowOccluders;
            if (charactersCastShade) occluderMask |= CombatLayers.PlayerMask | CombatLayers.EnemyMask;
        }

        void LateUpdate()
        {
            if (cameraTransform == null) return;

            Vector3 cameraForward = cameraTransform.forward;
            Vector3 flatForward = new Vector3(cameraForward.x, 0f, cameraForward.z);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = cameraTransform.up; // looking straight down
            flatForward.Normalize();

            body.transform.rotation = Quaternion.LookRotation(Vector3.Slerp(flatForward, cameraForward, tiltTowardCamera));
            if (shadow != null)
            {
                Vector3 light = sun != null ? sun.forward : cameraForward;
                light.y = 0f;
                shadow.transform.rotation = Quaternion.LookRotation(light.sqrMagnitude > 0.0001f ? light : flatForward);
            }

            int direction = GetDirection(flatForward);
            CurrentDirection = direction;
            int row = direction;
            bool flip = false;
            if (sheet.MirrorLeft && direction > 4)
            {
                row = 8 - direction; // left views reuse the right views, mirrored
                flip = true;
            }

            UpdateBrightness();

            int column = GetColumn();
            if (column < 0) return;
            int clipFrame = column - CurrentClip.StartColumn;
            if (clipFrame != lastEventFrame || currentAnim != lastEventAnim)
            {
                lastEventFrame = clipFrame;
                lastEventAnim = currentAnim;
                FrameShown?.Invoke(this, currentAnim, clipFrame);
            }
            Apply(body, row, column, flip);
            if (shadow != null) Apply(shadow, row, column, flip);
        }

        /// <summary>A few rays toward the sun per frame; fades so moving into shade isn't a hard pop.</summary>
        void UpdateBrightness()
        {
            float target = 1f;
            if (sampleWorldShadow && !perPixelShadows && sun != null)
            {
                Vector3 toSun = -sun.forward;
                float spriteHeight = sheet.WorldSize.y;
                int blocked = 0;
                foreach (float h in shadeSampleHeights)
                    if (IsSunBlocked(transform.position + Vector3.up * (spriteHeight * h), toSun)) blocked++;
                target = Mathf.Lerp(1f, shadowedBrightness, blocked / (float)shadeSampleHeights.Length);
            }
            brightness = Mathf.MoveTowards(brightness, target, Time.deltaTime * 4f);
        }

        bool IsSunBlocked(Vector3 origin, Vector3 toSun)
        {
            int count = Physics.RaycastNonAlloc(origin, toSun, hitBuffer, 200f, occluderMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hitBuffer[i].transform.IsChildOf(facingRoot)) return true; // never shaded by our own body
            return false;
        }

        int GetDirection(Vector3 cameraFlatForward)
        {
            Vector3 facing = facingRoot.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f) return 0;

            // 0° = facing the camera, +90° = facing screen-right.
            float angle = -Vector3.SignedAngle(-cameraFlatForward, facing, Vector3.up);
            return (Mathf.RoundToInt(angle / 45f) % 8 + 8) % 8;
        }

        int GetColumn()
        {
            var anim = source?.CurrentAnim ?? CharacterAnim.Idle;
            var clip = sheet.Find(anim);
            CurrentClip = clip;
            if (clip == null) return -1;

            if (anim != currentAnim)
            {
                currentAnim = anim;
                clipTime = 0f;
            }
            else
            {
                clipTime += Time.deltaTime * (source?.AnimationSpeed ?? 1f);
            }

            int frame;
            var attacks = source?.Attacks;
            if (anim == CharacterAnim.Attack && attacks != null && attacks.IsAttacking && clip.StartupFrames + clip.ActiveFrames > 0)
            {
                frame = GetAttackFrame(clip, attacks);
            }
            else
            {
                frame = (int)(clipTime * clip.FramesPerSecond);
                frame = clip.Loop ? frame % clip.FrameCount : Mathf.Min(frame, clip.FrameCount - 1);
            }
            return clip.StartColumn + frame;
        }

        /// <summary>Maps attack phases onto the clip's frames so the strike frame shows exactly when the hitbox is live.</summary>
        static int GetAttackFrame(DirectionalSpriteSheet.Clip clip, AttackExecutor attacks)
        {
            var attack = attacks.Current;
            float t = attacks.Elapsed;
            int recoveryFrames = Mathf.Max(0, clip.FrameCount - clip.StartupFrames - clip.ActiveFrames);

            if (t < attack.Startup)
                return Segment(t / attack.Startup, 0, clip.StartupFrames);
            if (t < attack.ActiveEnd)
                return Segment((t - attack.Startup) / attack.Active, clip.StartupFrames, clip.ActiveFrames);
            return Segment((t - attack.ActiveEnd) / Mathf.Max(attack.Recovery, 0.0001f),
                clip.StartupFrames + clip.ActiveFrames, recoveryFrames);
        }

        static int Segment(float normalized, int start, int count)
        {
            if (count <= 0) return Mathf.Max(0, start - 1);
            return start + Mathf.Min(count - 1, (int)(Mathf.Clamp01(normalized) * count));
        }

        void Apply(Renderer target, int row, int column, bool flip)
        {
            var texture = textureOverride != null ? textureOverride : sheet.Texture;
            float width = texture.width;
            float height = texture.height;
            float scaleX = sheet.CellSize.x / width;
            float scaleY = sheet.CellSize.y / height;
            float offsetX = column * sheet.CellSize.x / width;
            float offsetY = (height - (row + 1) * sheet.CellSize.y) / height; // row 0 is the top of the image

            var st = flip
                ? new Vector4(-scaleX, scaleY, offsetX + scaleX, offsetY)
                : new Vector4(scaleX, scaleY, offsetX, offsetY);

            // Get first so other writers (e.g. CombatantFlash's emission) are preserved.
            target.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, texture);
            block.SetVector(BaseMapStId, st);
            block.SetColor(BaseColorId, new Color(brightness, brightness, brightness, 1f));
            target.SetPropertyBlock(block);
        }

        static void SetUpQuad(MeshRenderer quad, Mesh mesh, Vector3 scale)
        {
            quad.GetComponent<MeshFilter>().sharedMesh = mesh;
            quad.transform.localScale = scale;
            quad.transform.localPosition = Vector3.zero;
        }

        static Transform FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun.transform;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return light.transform;
            return null;
        }

        // Only when selected: in edit mode the sprite itself is drawn (Editor/SpriteEditPreview).
        void OnDrawGizmosSelected()
        {
            // Sprites only render in Play Mode; this keeps characters visible and selectable in the Scene view.
            if (sheet == null) return;
            var size = sheet.WorldSize;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * (size.y * (0.5f - sheet.Pivot.y)), new Vector3(size.x, size.y, size.x * 0.5f));
        }
    }
}
