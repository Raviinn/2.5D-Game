using System.Collections.Generic;
using Beast.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 23: characters show as their sprites outside Play Mode (they used to be cyan boxes). Each character's
    /// idle frame is drawn straight into the Scene and Game view cameras, facing the camera and turned the way the
    /// character faces, so you can place NPCs and enemies by eye. Nothing is written to the scene: the real sprite
    /// quad is only set up when the game runs. Turn it off with Beast → Show Character Sprites in Edit Mode.
    /// </summary>
    [InitializeOnLoad]
    static class SpriteEditPreview
    {
        const string PrefKey = "Beast.SpriteEditPreview";
        const string MenuPath = "Beast/Show Character Sprites in Edit Mode";
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static readonly List<DirectionalSpriteRenderer> renderers = new();
        static MaterialPropertyBlock block;
        static double nextRefresh;

        static SpriteEditPreview()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        static bool Enabled => EditorPrefs.GetBool(PrefKey, true);

        [MenuItem(MenuPath, priority = 200)]
        static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !Enabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (Application.isPlaying || !Enabled) return;
            if (camera.cameraType is not (CameraType.SceneView or CameraType.Game)) return;

            if (EditorApplication.timeSinceStartup >= nextRefresh)
            {
                nextRefresh = EditorApplication.timeSinceStartup + 1.0;
                renderers.Clear();
                renderers.AddRange(Object.FindObjectsByType<DirectionalSpriteRenderer>(FindObjectsSortMode.None));
            }
            block ??= new MaterialPropertyBlock();

            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.isActiveAndEnabled) continue;
                var so = new SerializedObject(renderer);
                var sheet = renderer.Sheet;
                var body = so.FindProperty("body").objectReferenceValue as MeshRenderer;
                if (sheet == null || sheet.Texture == null || body == null || body.sharedMaterial == null) continue;
                float bend = so.FindProperty("normalBend").floatValue;
                var idle = sheet.Find(CharacterAnim.Idle);
                if (idle == null) continue;

                // Upright, facing the camera; the row follows the character's facing, as in the game.
                Vector3 flat = camera.transform.forward;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.0001f) flat = camera.transform.up;
                flat.Normalize();
                var root = renderer.transform.parent != null ? renderer.transform.parent : renderer.transform;
                Vector3 facing = root.forward;
                facing.y = 0f;
                int direction = facing.sqrMagnitude < 0.0001f ? 0 : (Mathf.RoundToInt(-Vector3.SignedAngle(-flat, facing, Vector3.up) / 45f) % 8 + 8) % 8;
                int row = direction;
                bool flip = false;
                if (sheet.MirrorLeft && direction > 4)
                {
                    row = 8 - direction;
                    flip = true;
                }

                var texture = sheet.Texture;
                float scaleX = sheet.CellSize.x / (float)texture.width;
                float scaleY = sheet.CellSize.y / (float)texture.height;
                float offsetX = idle.StartColumn * sheet.CellSize.x / (float)texture.width;
                float offsetY = (texture.height - (row + 1) * sheet.CellSize.y) / (float)texture.height;
                block.Clear();
                block.SetTexture(BaseMapId, texture);
                block.SetVector(BaseMapStId, flip ? new Vector4(-scaleX, scaleY, offsetX + scaleX, offsetY) : new Vector4(scaleX, scaleY, offsetX, offsetY));
                block.SetColor(BaseColorId, Color.white);

                var mesh = SpriteQuad.Get(sheet.Pivot, bend);
                var matrix = Matrix4x4.TRS(renderer.transform.position, Quaternion.LookRotation(flat), new Vector3(sheet.WorldSize.x, sheet.WorldSize.y, 1f));
                Graphics.DrawMesh(mesh, matrix, body.sharedMaterial, renderer.gameObject.layer, camera, 0, block, ShadowCastingMode.Off, false);
            }
        }
    }
}
