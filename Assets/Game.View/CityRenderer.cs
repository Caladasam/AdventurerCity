using System.Collections.Generic;
using AdventurerCity.Simulation;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed class CityRenderer : MonoBehaviour
    {
        readonly Dictionary<int, MeshRenderer> buildingViews = new Dictionary<int, MeshRenderer>();
        readonly HashSet<int> alive = new HashSet<int>();
        readonly List<int> removed = new List<int>();

        GameRunner runner;
        Transform roadsRoot;
        Transform buildingsRoot;
        Transform ground;
        int renderedVersion = -1;

        public void Init(GameRunner owner)
        {
            runner = owner;
            roadsRoot = new GameObject("Roads").transform;
            buildingsRoot = new GameObject("Buildings").transform;
            ground = Materials.CreateCube("Ground", null).transform;
            ground.GetComponent<MeshRenderer>().sharedMaterial = Materials.Get(Palette.Ground);
        }

        public void ResetView()
        {
            foreach (var view in buildingViews.Values)
                Destroy(view.gameObject);
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
                renderedVersion = world.Version;
            }
            UpdateBuildings(world);
        }

        void LayoutGround(World world)
        {
            var size = new Vector3(world.Grid.Width * GridMath.CellSize, 0.2f, world.Grid.Height * GridMath.CellSize);
            ground.localScale = size;
            ground.position = new Vector3(size.x * 0.5f, -0.1f, size.z * 0.5f);
        }

        void RebuildRoads(World world)
        {
            for (var i = roadsRoot.childCount - 1; i >= 0; i--)
                Destroy(roadsRoot.GetChild(i).gameObject);

            var material = Materials.Get(Palette.Road);
            foreach (var index in world.State.Roads)
            {
                var road = Materials.CreateCube("Road", roadsRoot);
                road.GetComponent<MeshRenderer>().sharedMaterial = material;
                road.transform.localScale = new Vector3(GridMath.CellSize, 0.04f, GridMath.CellSize);
                road.transform.position = GridMath.CellCenter(index % world.Grid.Width, index / world.Grid.Width) + Vector3.up * 0.02f;
            }
        }

        void SyncBuildings(World world)
        {
            alive.Clear();
            foreach (var building in world.Buildings)
            {
                alive.Add(building.Id);
                if (buildingViews.ContainsKey(building.Id)) continue;
                var view = Materials.CreateCube(world.Def(building).Name, buildingsRoot);
                buildingViews.Add(building.Id, view.GetComponent<MeshRenderer>());
            }

            removed.Clear();
            foreach (var id in buildingViews.Keys)
                if (!alive.Contains(id))
                    removed.Add(id);
            foreach (var id in removed)
            {
                Destroy(buildingViews[id].gameObject);
                buildingViews.Remove(id);
            }
        }

        void UpdateBuildings(World world)
        {
            foreach (var building in world.Buildings)
            {
                if (!buildingViews.TryGetValue(building.Id, out var view)) continue;

                var def = world.Def(building);
                var rect = world.Footprint(building);
                var height = Height(building);
                Color color;
                if (building.IsComplete)
                {
                    color = Palette.Building(building.Kind);
                    if (!IsWorking(building, def)) color = Color.Lerp(color, Palette.Idle, 0.45f);
                }
                else
                {
                    var built = 1f - building.ConstructionLeft / (float)def.BuildTicks;
                    height = Mathf.Max(0.2f, height * built);
                    color = Palette.Scaffold;
                }

                var t = view.transform;
                t.localScale = new Vector3(rect.Width * GridMath.CellSize - 0.3f, height, rect.Depth * GridMath.CellSize - 0.3f);
                t.position = GridMath.RectCenter(rect) + Vector3.up * (height * 0.5f);
                var material = Materials.Get(color);
                if (view.sharedMaterial != material) view.sharedMaterial = material;
            }
        }

        static bool IsWorking(Building building, BuildingDef def)
        {
            if (building.Kind == BuildingKind.House) return building.IsConnected && building.FoodMet && building.FirewoodMet;
            if (def.Recipe != null) return building.IsProducing && !building.Stalled;
            return building.IsConnected;
        }

        static float Height(Building building)
        {
            switch (building.Kind)
            {
                case BuildingKind.Storehouse: return 2.6f;
                case BuildingKind.House: return 0.8f + building.Level * 1.1f;
                case BuildingKind.Farm: return 0.4f;
                case BuildingKind.Woodcutter: return 1.6f;
                case BuildingKind.Guild: return 3.4f;
                case BuildingKind.Forge: return 2.4f;
                case BuildingKind.Watchtower: return 4.2f;
                default: return 2.2f;
            }
        }
    }
}
