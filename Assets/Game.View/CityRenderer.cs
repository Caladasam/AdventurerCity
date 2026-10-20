using System.Collections.Generic;
using AdventurerCity.Simulation;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AdventurerCity.View
{
    public sealed class CityRenderer : MonoBehaviour
    {
        const int GroundOrder = -32000;
        const int RoadOrder = -31000;
        const float TreeChance = 0.07f;

        sealed class BuildingView
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public Light2D Light;
            public float LightBase;
            public bool LightAtDay;
            public ParticleSystem Smoke;
            public ParticleSystem Sparks;
            public string SpriteKey;
        }

        readonly Dictionary<int, BuildingView> buildingViews = new Dictionary<int, BuildingView>();
        readonly HashSet<int> alive = new HashSet<int>();
        readonly List<int> removed = new List<int>();
        readonly List<SpriteRenderer> roads = new List<SpriteRenderer>();
        readonly List<(SpriteRenderer Renderer, int X, int Y)> trees = new List<(SpriteRenderer, int, int)>();

        GameRunner runner;
        Transform roadsRoot;
        Transform buildingsRoot;
        Transform decorRoot;
        SpriteRenderer ground;
        Light2D sun;
        int renderedVersion = -1;

        public Light2D Sun => sun;

        public void Init(GameRunner owner)
        {
            runner = owner;
            roadsRoot = new GameObject("Roads").transform;
            buildingsRoot = new GameObject("Buildings").transform;
            decorRoot = new GameObject("Decor").transform;
            ground = PixelArt.Create("Ground", null, PixelArt.Grass(), GroundOrder);
            ground.drawMode = SpriteDrawMode.Tiled;
            sun = Effects.Light("Sun", null, Light2D.LightType.Global);
        }

        public void ResetView()
        {
            foreach (var view in buildingViews.Values)
                Destroy(view.Root.gameObject);
            buildingViews.Clear();
            renderedVersion = -1;
        }

        void LateUpdate()
        {
            var world = runner.Sim;
            if (world.Version != renderedVersion)
            {
                LayoutGround(world);
                RebuildRoads(world);
                SyncBuildings(world);
                UpdateTrees(world);
                renderedVersion = world.Version;
            }
            var night = UpdateDaylight(world);
            UpdateBuildings(world, night);
        }

        float UpdateDaylight(World world)
        {
            var hour = world.DayProgress * 24f;
            var day = Mathf.Clamp01(Mathf.Sin((hour - 5.5f) / 13f * Mathf.PI) * 1.6f);
            var dusk = Mathf.Clamp01(1f - Mathf.Abs(day - 0.45f) * 2.5f);
            var nightColor = new Color(0.45f, 0.52f, 0.85f);
            var dayColor = new Color(1f, 0.98f, 0.93f);
            var duskColor = new Color(1f, 0.72f, 0.52f);
            if (runner.IsWatchingDungeon)
            {
                sun.color = new Color(0.62f, 0.6f, 0.75f);
                sun.intensity = 0.42f;
            }
            else
            {
                sun.color = Color.Lerp(Color.Lerp(nightColor, dayColor, day), duskColor, dusk * 0.6f);
                sun.intensity = Mathf.Lerp(0.36f, 1.05f, day);
            }
            runner.Rig.Camera.backgroundColor = Color.Lerp(new Color(0.05f, 0.07f, 0.1f), Palette.Background, day);
            return 1f - day;
        }

        void LayoutGround(World world)
        {
            var size = new Vector2(world.Grid.Width * GridMath.CellSize, world.Grid.Height * GridMath.CellSize);
            ground.size = size;
            ground.transform.position = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
            if (trees.Count == 0) PlantTrees(world);
        }

        void PlantTrees(World world)
        {
            var center = new Vector2(world.Grid.Width * 0.5f, world.Grid.Height * 0.5f);
            for (var y = 0; y < world.Grid.Height; y++)
            for (var x = 0; x < world.Grid.Width; x++)
            {
                var near = Vector2.Distance(new Vector2(x, y), center);
                var h = Hash(x, y);
                if (h > TreeChance * Mathf.Clamp01(near / 14f)) continue;
                var tree = PixelArt.Create("Tree", decorRoot, PixelArt.Tree((x + y) % 2), PixelArt.SortOrder(y));
                tree.transform.position = GridMath.CellCenter(x, y) + new Vector3(Hash(y, x) * 0.4f - 0.2f, -0.45f, 0f);
                trees.Add((tree, x, y));
            }
        }

        void UpdateTrees(World world)
        {
            foreach (var (renderer, x, y) in trees)
                renderer.enabled = world.Grid[x, y] == CityGrid.Empty;
        }

        void RebuildRoads(World world)
        {
            var count = 0;
            foreach (var index in world.State.Roads)
            {
                if (count == roads.Count) roads.Add(PixelArt.Create("Road", roadsRoot, PixelArt.Road(), RoadOrder));
                var road = roads[count++];
                road.gameObject.SetActive(true);
                road.transform.position = GridMath.CellCenter(index % world.Grid.Width, index / world.Grid.Width);
            }
            for (var i = count; i < roads.Count; i++) roads[i].gameObject.SetActive(false);
        }

        void SyncBuildings(World world)
        {
            alive.Clear();
            foreach (var building in world.Buildings)
            {
                alive.Add(building.Id);
                if (buildingViews.ContainsKey(building.Id)) continue;
                var root = new GameObject(world.Def(building).Name).transform;
                root.SetParent(buildingsRoot, false);
                var view = new BuildingView
                {
                    Root = root,
                    Body = PixelArt.Create("Body", root, null),
                    Shadow = PixelArt.Create("Shadow", root, PixelArt.Square())
                };
                view.Shadow.color = new Color(0f, 0f, 0f, 0.28f);
                buildingViews.Add(building.Id, view);
            }

            removed.Clear();
            foreach (var id in buildingViews.Keys)
                if (!alive.Contains(id))
                    removed.Add(id);
            foreach (var id in removed)
            {
                Destroy(buildingViews[id].Root.gameObject);
                buildingViews.Remove(id);
            }
        }

        void UpdateBuildings(World world, float night)
        {
            var selected = runner.Tool.SelectedId;
            var pulse = 0.12f + 0.08f * Mathf.Sin(Time.time * 6f);
            foreach (var building in world.Buildings)
            {
                if (!buildingViews.TryGetValue(building.Id, out var view)) continue;

                var def = world.Def(building);
                var rect = world.Footprint(building);
                var order = PixelArt.SortOrder(rect.Y);
                view.Root.position = GridMath.CellCorner(rect.X, rect.Y);

                var key = building.IsComplete ? $"{building.Kind}{rect.Width}{rect.Depth}{building.Level}" : $"s{rect.Width}{rect.Depth}";
                if (view.SpriteKey != key)
                {
                    view.SpriteKey = key;
                    view.Body.sprite = building.IsComplete
                        ? PixelArt.Building(building.Kind, rect.Width, rect.Depth, building.Level, Palette.Building(building.Kind))
                        : PixelArt.Scaffold(rect.Width, rect.Depth);
                    if (building.IsComplete) AttachEffects(view, building, rect, order);
                }

                var working = building.IsComplete && IsWorking(building, def);
                var color = !building.IsComplete
                    ? new Color(1f, 1f, 1f, 0.55f + 0.45f * (1f - building.ConstructionLeft / (float)def.BuildTicks))
                    : working ? Color.white : Palette.Idle;
                if (building.Id == selected) color = Color.Lerp(color, Color.white, pulse) * (1f + pulse);
                color.a = Mathf.Min(1f, color.a);
                view.Body.color = color;
                view.Body.sortingOrder = order;

                view.Shadow.sortingOrder = order - 1;
                view.Shadow.transform.localScale = new Vector3(rect.Width, rect.Depth * 0.5f, 1f);
                view.Shadow.transform.localPosition = new Vector3(rect.Width * 0.5f + 0.18f, rect.Depth * 0.25f - 0.12f, 0f);

                if (view.Light != null)
                {
                    var lit = working || building.Kind == BuildingKind.House && building.Residents > 0;
                    view.Light.intensity = lit ? view.LightBase * (view.LightAtDay ? 0.35f + 0.65f * night : night) : 0f;
                }
                SetEmission(view.Smoke, working ? (building.Kind == BuildingKind.House ? 1.5f + 2f * night : 4f) : 0f);
                SetEmission(view.Sparks, working ? 1f : 0f);
            }
        }

        void AttachEffects(BuildingView view, Building building, CellRect rect, int order)
        {
            if (view.Light != null) Destroy(view.Light.gameObject);
            if (view.Smoke != null) Destroy(view.Smoke.gameObject);
            if (view.Sparks != null) Destroy(view.Sparks.gameObject);
            view.Light = null;
            view.Smoke = null;
            view.Sparks = null;

            var wall = PixelArt.WallPixels(building.Kind, building.Level) / (float)PixelArt.PixelsPerUnit;
            var center = new Vector3(rect.Width * 0.5f, wall * 0.5f, 0f);
            switch (building.Kind)
            {
                case BuildingKind.House:
                case BuildingKind.Guild:
                case BuildingKind.Storehouse:
                case BuildingKind.Watchtower:
                case BuildingKind.Temple:
                    view.Light = Effects.PointLight("Windows", view.Root, new Color(1f, 0.78f, 0.45f), 1.6f + rect.Width * 0.6f, 1.1f);
                    view.LightBase = 1.1f;
                    break;
                case BuildingKind.Forge:
                case BuildingKind.Bakery:
                    view.Light = Effects.PointLight("Fire", view.Root, new Color(1f, 0.55f, 0.25f), 2.4f + rect.Width * 0.5f, 1.4f);
                    view.LightBase = 1.4f;
                    view.LightAtDay = true;
                    break;
            }
            if (view.Light != null) view.Light.transform.localPosition = center;

            if (PixelArt.HasChimney(building.Kind, building.Level))
            {
                var chimney = new Vector3((rect.Width * PixelArt.PixelsPerUnit - 5f) / PixelArt.PixelsPerUnit, rect.Depth + wall - 0.1f, 0f);
                view.Smoke = Effects.Smoke(view.Root, order + 5);
                view.Smoke.transform.localPosition = chimney;
            }
            if (building.Kind == BuildingKind.Forge)
            {
                view.Sparks = Effects.Sparks(view.Root, order + 6);
                view.Sparks.transform.localPosition = center + new Vector3(0f, 0.1f, 0f);
            }
        }

        static void SetEmission(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            if (ps.name == "Sparks") emission.enabled = rate > 0f;
            else emission.rateOverTime = rate;
        }

        static bool IsWorking(Building building, BuildingDef def)
        {
            if (building.Kind == BuildingKind.House) return building.IsConnected && building.FoodMet && building.FirewoodMet;
            if (def.Recipe != null) return building.IsProducing && !building.Stalled;
            if (def.Workers > 0) return building.IsConnected && building.Workers > 0;
            return building.IsConnected;
        }

        static float Hash(int x, int y)
        {
            var h = (uint)(x * 73856093 ^ y * 19349663);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
