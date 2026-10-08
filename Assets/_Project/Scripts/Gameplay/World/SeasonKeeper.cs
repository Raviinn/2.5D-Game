using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Milestone 46: turns the Calendar into the world. Each morning it announces a new season or a festival, and it
    /// dresses the land for the season: greener grass in spring, golden in summer, orange leaves in autumn, a pale,
    /// snowy winter where the oaks stand bare and the grass and flowers are gone. Works on runtime copies of the environment
    /// materials, so nothing in the project changes.
    /// </summary>
    public sealed class SeasonKeeper : MonoBehaviour
    {
        struct Tint
        {
            public string Material;
            public Color Spring, Summer, Autumn, Winter;
            public bool HideInWinter;
        }

        static readonly Color Plain = Color.white;
        static readonly Tint[] Tints =
        {
            new() { Material = "Env_Ground", Spring = Plain, Summer = new(1.08f, 1.02f, 0.8f), Autumn = new(1.12f, 0.9f, 0.62f), Winter = new(1.45f, 1.5f, 1.6f) },
            new() { Material = "Env_GrassTuft", Spring = Plain, Summer = new(1.1f, 1.05f, 0.72f), Autumn = new(1.2f, 0.85f, 0.5f), Winter = Plain, HideInWinter = true },
            new() { Material = "Env_Bush", Spring = Plain, Summer = new(1.05f, 1f, 0.85f), Autumn = new(1.15f, 0.75f, 0.45f), Winter = new(0.85f, 0.85f, 0.95f) },
            new() { Material = "Env_Oak", Spring = Plain, Summer = new(1.05f, 1.02f, 0.85f), Autumn = new(1.3f, 0.75f, 0.4f), Winter = new(0.95f, 0.95f, 1.05f) },
            new() { Material = "Env_Pine", Spring = Plain, Summer = Plain, Autumn = new(0.95f, 0.95f, 0.9f), Winter = new(1.15f, 1.2f, 1.3f) },
            new() { Material = "Env_Flowers", Spring = Plain, Summer = Plain, Autumn = new(0.9f, 0.75f, 0.6f), Winter = Plain, HideInWinter = true },
            new() { Material = "Env_Ivy", Spring = Plain, Summer = Plain, Autumn = new(1.25f, 0.7f, 0.45f), Winter = new(0.8f, 0.8f, 0.85f) },
            new() { Material = "Env_MapCanopy", Spring = Plain, Summer = new(1.05f, 1f, 0.8f), Autumn = new(1.2f, 0.8f, 0.5f), Winter = new(0.85f, 0.85f, 0.9f) },
        };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        Texture groundTexture;
        Texture2D snowTexture;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly Dictionary<string, Material> copies = new();
        readonly List<(Renderer renderer, Tint tint)> targets = new();
        Material bareTree;
        Season? shown;

        /// <summary>The season the world is currently dressed for.</summary>
        public Season Shown => shown ?? Season.Spring;

        void OnEnable() => EventBus<DayPassedEvent>.Subscribe(OnDayPassed);
        void OnDisable() => EventBus<DayPassedEvent>.Unsubscribe(OnDayPassed);

        void Start()
        {
            CollectTargets();
            Dress(Calendar.Current);
        }

        // A loaded save can jump to another season without a day passing.
        void Update()
        {
            if (shown != Calendar.Current) Dress(Calendar.Current);
        }

        void OnDayPassed(DayPassedEvent evt)
        {
            var season = Calendar.SeasonOf(evt.Day);
            var previous = Calendar.SeasonOf(evt.Day - 1);
            if (season != previous)
            {
                EventBus<SeasonChangedEvent>.Raise(new SeasonChangedEvent(previous, season));
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent(season switch
                {
                    Season.Summer => "Summer has come. Long days, little rain.",
                    Season.Autumn => "Autumn. The leaves are turning; pumpkins will grow now.",
                    Season.Winter => "Winter. Only frost kale grows in the cold.",
                    _ => "Spring is here. Time to plant.",
                }));
            }
            var festival = Calendar.FestivalOn(evt.Day);
            if (festival == Festival.PlantingFestival)
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent("Today is the Planting Festival: seeds are half price!"));
            else if (festival == Festival.HarvestFair)
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent("Today is the Harvest Fair: crops sell for half as much again!"));
            Dress(season);
        }

        void CollectTargets()
        {
            targets.Clear();
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var material = renderer.sharedMaterial;
                if (material == null) continue;
                string name = material.name.Replace(" (Instance)", string.Empty);
                if (name == "Env_DeadTree" && bareTree == null) bareTree = material;
                foreach (var tint in Tints)
                {
                    if (name != tint.Material) continue;
                    if (!copies.TryGetValue(name, out var copy))
                    {
                        copy = new Material(material) { name = name + " (Season)", hideFlags = HideFlags.DontSave };
                        copies[name] = copy;
                    }
                    renderer.sharedMaterial = copy;
                    targets.Add((renderer, tint));
                    break;
                }
            }
        }

        /// <summary>Re-tints the land for a season (also used by tests).</summary>
        public void Dress(Season season)
        {
            shown = season;
            foreach (var tint in Tints)
            {
                if (!copies.TryGetValue(tint.Material, out var copy)) continue;
                var color = season switch { Season.Summer => tint.Summer, Season.Autumn => tint.Autumn, Season.Winter => tint.Winter, _ => tint.Spring };
                if (copy.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, color);
                else if (copy.HasProperty(ColorId)) copy.SetColor(ColorId, color);
            }
            bool winter = season == Season.Winter;
            // Snow lies on the ground in winter: a generated snow texture instead of the grass (tinting can't make white).
            if (copies.TryGetValue("Env_Ground", out var groundCopy) && groundCopy.HasProperty(BaseMapId))
            {
                groundTexture ??= groundCopy.GetTexture(BaseMapId);
                groundCopy.SetTexture(BaseMapId, winter ? SnowTexture() : groundTexture);
                if (winter) groundCopy.SetColor(groundCopy.HasProperty(BaseColorId) ? BaseColorId : ColorId, Color.white);
            }
            foreach (var (renderer, tint) in targets)
            {
                if (renderer == null) continue;
                if (tint.HideInWinter) renderer.enabled = !winter;
                // Oaks drop their leaves in winter: the bare-tree picture, same size.
                if (tint.Material == "Env_Oak" && bareTree != null)
                    renderer.sharedMaterial = winter ? bareTree : copies[tint.Material];
            }
        }

        /// <summary>A small tiling snow texture: pale blue-white with a few brighter and greyer flecks.</summary>
        Texture2D SnowTexture()
        {
            if (snowTexture != null) return snowTexture;
            const int size = 32;
            snowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Snow (generated)", hideFlags = HideFlags.DontSave,
            };
            var random = new System.Random(7);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                double r = random.NextDouble();
                pixels[i] = r < 0.08 ? new Color32(248, 250, 255, 255) : r < 0.16 ? new Color32(196, 204, 218, 255) : new Color32(226, 231, 240, 255);
            }
            snowTexture.SetPixels32(pixels);
            snowTexture.Apply(false, false);
            return snowTexture;
        }

        void OnDestroy()
        {
            foreach (var copy in copies.Values) if (copy != null) Destroy(copy);
            if (snowTexture != null) Destroy(snowTexture);
        }
    }
}
