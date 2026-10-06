using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Layer names used by environment art (created by the Milestone 9 setup).</summary>
    public static class EnvironmentLayers
    {
        /// <summary>Camera-facing environment sprites (trees, bushes, grass). Hidden from the minimap: edge-on from above.</summary>
        public const string Billboards = "EnvBillboard";
        /// <summary>Flat top-down shapes (tree canopies) that only the minimap camera renders.</summary>
        public const string MinimapOnly = "MinimapOnly";
    }

    /// <summary>
    /// A static 2D sprite standing in the 3D world (trees, bushes, grass), matching the characters' look:
    /// the Body quad turns to face the camera around the vertical axis, and an optional Shadow quad turns to
    /// face the sun and only casts shadows, so the shadow keeps its shape from every camera angle.
    /// One driver updates every billboard, so hundreds of them cost a single Update call.
    /// </summary>
    [ExecuteAlways]
    public sealed class BillboardSprite : MonoBehaviour
    {
        static readonly List<BillboardSprite> active = new();
        static Transform sun;

        [SerializeField] Transform body;
        [SerializeField, Tooltip("Optional shadow-only quad that turns toward the sun.")] Transform shadow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            sun = null;
        }

        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            BillboardDriver.Ensure();
        }

        void OnDisable() => active.Remove(this);

        /// <summary>Called once per frame by the driver, after the camera has moved.</summary>
        internal static void UpdateAll(Vector3 cameraForward)
        {
            Vector3 flat = new(cameraForward.x, 0f, cameraForward.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            var facing = Quaternion.LookRotation(flat.normalized);

            if (sun == null) sun = FindSun();
            Vector3 light = sun != null ? sun.forward : flat;
            light.y = 0f;
            var shadowFacing = Quaternion.LookRotation(light.sqrMagnitude > 0.0001f ? light.normalized : flat.normalized);

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var billboard = active[i];
                if (billboard == null) { active.RemoveAt(i); continue; }
                if (billboard.body != null) billboard.body.rotation = facing;
                if (billboard.shadow != null) billboard.shadow.rotation = shadowFacing;
            }
        }

        static Transform FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun.transform;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return light.transform;
            return null;
        }
    }

    /// <summary>Hidden helper that turns every BillboardSprite once per frame (in Play Mode and in the Scene view).</summary>
    [ExecuteAlways, DefaultExecutionOrder(100)]
    sealed class BillboardDriver : MonoBehaviour
    {
        static BillboardDriver instance;

        internal static void Ensure()
        {
            if (instance != null) return;
            instance = FindFirstObjectByType<BillboardDriver>();
            if (instance != null) return;
            // In Play Mode it lives and dies with the scene; in the editor it's never saved into the scene.
            var go = new GameObject("[BillboardDriver]")
            {
                hideFlags = Application.isPlaying ? HideFlags.HideInHierarchy : HideFlags.HideAndDontSave,
            };
            instance = go.AddComponent<BillboardDriver>();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void LateUpdate()
        {
            Camera cam = Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView != null && sceneView.camera != null) cam = sceneView.camera;
            }
#endif
            if (cam != null) BillboardSprite.UpdateAll(cam.transform.forward);
        }
    }
}
