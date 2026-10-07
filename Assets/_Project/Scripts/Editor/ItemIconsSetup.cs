using Beast.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 18 setup (item icons): draws a placeholder icon for every item that has none and assigns it,
    /// plus the gold icon and the material dropped items use in the world. Safe to re-run: icons you've
    /// replaced with your own art are left alone, and existing placeholder PNGs aren't redrawn.
    /// </summary>
    public static class ItemIconsSetup
    {
        const string Root = "Assets/_Project";
        const string IconFolder = Root + "/Art/UI/Icons";
        const string ResourceIconFolder = Root + "/Resources/Icons";
        /// <summary>Loaded at runtime by ItemPickup (Resources), so it ships in builds.</summary>
        public const string GoldIconPath = ResourceIconFolder + "/Icon_Gold.png";
        public const string PickupMaterialPath = Root + "/Resources/Pickup_Icon.mat";

        [MenuItem("Beast/Setup/Run Milestone 18 Setup (Item Icons)", priority = 17)]
        public static void Run()
        {
            EnsureFolder(IconFolder);
            EnsureFolder(ResourceIconFolder);

            int drawn = 0, kept = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;

                string iconPath = $"{IconFolder}/Icon_{item.name}.png";
                if (item.Icon != null && AssetDatabase.GetAssetPath(item.Icon) != iconPath)
                {
                    kept++; // your own icon
                    continue;
                }

                item.Icon = ItemIconGenerator.CreateIcon(iconPath, item);
                EditorUtility.SetDirty(item);
                drawn++;
            }

            ItemIconGenerator.CreateGoldIcon(GoldIconPath);
            CreatePickupMaterial();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Milestone 18 item icons complete: {drawn} placeholder icon(s) assigned" +
                      (kept > 0 ? $", {kept} custom icon(s) kept." : "."));
        }

        /// <summary>Unlit, alpha-clipped and double-sided: dropped items stay readable day and night.</summary>
        static void CreatePickupMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(PickupMaterialPath) != null) return;

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            AssetDatabase.CreateAsset(material, PickupMaterialPath);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
