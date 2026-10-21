using System.Collections.Generic;
using AdventurerCity.Simulation;
using UnityEngine;

namespace AdventurerCity.View
{
    public enum ToolMode
    {
        None,
        Road,
        Build,
        Demolish
    }

    public sealed class BuildTool : MonoBehaviour
    {
        readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
        readonly List<(int X, int Y)> path = new List<(int X, int Y)>();

        GameRunner runner;
        CameraRig rig;
        GameUi hud;
        Transform ghostRoot;
        int ghostsUsed;
        bool dragging;
        Vector2Int dragStart;

        public ToolMode Mode { get; private set; }
        public BuildingKind Kind { get; private set; }
        public bool Rotated { get; private set; }
        public int SelectedId { get; set; }
        public string HoverMessage { get; private set; }

        public void Init(GameRunner owner, CameraRig cameraRig, GameUi overlay)
        {
            runner = owner;
            rig = cameraRig;
            hud = overlay;
            ghostRoot = new GameObject("Ghosts").transform;
        }

        public void SelectRoad() => SetMode(ToolMode.Road);

        public void SelectDemolish() => SetMode(ToolMode.Demolish);

        public void SelectBuilding(BuildingKind kind)
        {
            Kind = kind;
            SetMode(ToolMode.Build);
        }

        public void Cancel() => SetMode(ToolMode.None);

        public bool Back()
        {
            if (Mode != ToolMode.None)
            {
                Cancel();
                return true;
            }
            if (SelectedId == 0) return false;
            SelectedId = 0;
            return true;
        }

        void SetMode(ToolMode mode)
        {
            Mode = mode;
            dragging = false;
        }

        void Update()
        {
            HoverMessage = null;
            ghostsUsed = 0;
            if (runner.IsWatchingDungeon)
            {
                for (var i = 0; i < ghosts.Count; i++) ghosts[i].gameObject.SetActive(false);
                return;
            }

            if (runner.Menu) return;
            if (Input.GetMouseButtonDown(1)) Back();
            if (GameSettings.Down(GameKey.Rotate)) Rotated = !Rotated;

            var overUi = hud.IsPointerOverUi(Input.mousePosition);
            if ((!overUi || dragging) && rig.TryGetGroundCell(out var cell))
            {
                var world = runner.Sim;
                switch (Mode)
                {
                    case ToolMode.None: UpdateSelect(world, cell); break;
                    case ToolMode.Build: UpdateBuild(world, cell); break;
                    case ToolMode.Road: UpdateRoad(world, cell); break;
                    case ToolMode.Demolish: UpdateDemolish(world, cell); break;
                }
            }

            for (var i = ghostsUsed; i < ghosts.Count; i++)
                ghosts[i].gameObject.SetActive(false);
        }

        void UpdateSelect(World world, Vector2Int cell)
        {
            if (!Input.GetMouseButtonDown(0)) return;
            var building = world.BuildingAt(cell.x, cell.y);
            SelectedId = building?.Id ?? 0;
        }

        void UpdateBuild(World world, Vector2Int cell)
        {
            var def = world.Content.Get(Kind);
            var size = world.Footprint(Kind, 0, 0, Rotated);
            var x = cell.x - size.Width / 2;
            var y = cell.y - size.Depth / 2;
            var ok = world.CanPlace(Kind, x, y, Rotated, out var reason);

            ShowGhost(world.Footprint(Kind, x, y, Rotated), ok, 0.6f);
            HoverMessage = ok ? $"{def.Name}: {ResourceInfo.Format(def.Cost)}" : reason;

            if (ok && Input.GetMouseButtonDown(0))
            {
                world.Enqueue(new PlaceBuildingCommand(Kind, x, y, Rotated));
                GameAudio.Play(Sfx.Place);
            }
        }

        void UpdateRoad(World world, Vector2Int cell)
        {
            if (Input.GetMouseButtonDown(0))
            {
                dragging = true;
                dragStart = cell;
            }

            BuildPath(world, dragging ? dragStart : cell, cell);
            var cost = 0;
            foreach (var (x, y) in path)
            {
                var free = world.Grid[x, y] == CityGrid.Empty;
                var exists = world.Grid[x, y] == CityGrid.Road;
                if (free) cost += SimConstants.RoadGoldCost;
                ShowGhost(new CellRect(x, y, 1, 1), free || exists, 0.1f);
            }
            HoverMessage = $"Дорога: {cost} {ResourceInfo.Name(ResourceType.Gold)}";

            if (dragging && Input.GetMouseButtonUp(0))
            {
                world.Enqueue(new PlaceRoadCommand(path));
                GameAudio.Play(Sfx.Place, 0.6f);
                dragging = false;
            }
        }

        void UpdateDemolish(World world, Vector2Int cell)
        {
            if (!world.Grid.InBounds(cell.x, cell.y)) return;
            var building = world.BuildingAt(cell.x, cell.y);
            if (building != null)
            {
                ShowGhost(world.Footprint(building), false, 3f);
                HoverMessage = "Снести: " + world.Def(building).Name;
            }
            else if (world.Grid.IsRoad(cell.x, cell.y))
            {
                ShowGhost(new CellRect(cell.x, cell.y, 1, 1), false, 0.1f);
                HoverMessage = "Снести дорогу";
            }

            if (Input.GetMouseButtonDown(0))
            {
                world.Enqueue(new DemolishCommand(cell.x, cell.y));
                GameAudio.Play(Sfx.Place, 0.8f);
            }
        }

        void BuildPath(World world, Vector2Int from, Vector2Int to)
        {
            path.Clear();
            var stepX = to.x >= from.x ? 1 : -1;
            for (var x = from.x; x != to.x + stepX; x += stepX)
                AddPathCell(world, x, from.y);
            var stepY = to.y >= from.y ? 1 : -1;
            for (var y = from.y + stepY; y != to.y + stepY; y += stepY)
                AddPathCell(world, to.x, y);
        }

        void AddPathCell(World world, int x, int y)
        {
            if (world.Grid.InBounds(x, y)) path.Add((x, y));
        }

        void ShowGhost(CellRect rect, bool ok, float height)
        {
            if (ghostsUsed == ghosts.Count)
                ghosts.Add(PixelArt.Create("Ghost", ghostRoot, PixelArt.Square(), 30000));

            var ghost = ghosts[ghostsUsed++];
            ghost.gameObject.SetActive(true);
            var color = ok ? Palette.GhostOk : Palette.GhostBad;
            ghost.color = height > 1f ? new Color(color.r, color.g, color.b, 0.3f) : color;
            ghost.transform.localScale = new Vector3(rect.Width * GridMath.CellSize, rect.Depth * GridMath.CellSize, 1f);
            ghost.transform.position = GridMath.RectCenter(rect);
        }
    }
}
