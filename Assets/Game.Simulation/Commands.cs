using System.Collections.Generic;

namespace AdventurerCity.Simulation
{
    public abstract class Command
    {
        public abstract bool Execute(World world, out string error);
    }

    public sealed class PlaceBuildingCommand : Command
    {
        readonly BuildingKind kind;
        readonly int x;
        readonly int y;
        readonly bool rotated;

        public PlaceBuildingCommand(BuildingKind kind, int x, int y, bool rotated)
        {
            this.kind = kind;
            this.x = x;
            this.y = y;
            this.rotated = rotated;
        }

        public override bool Execute(World world, out string error)
        {
            if (!world.CanPlace(kind, x, y, rotated, out error)) return false;
            world.Place(kind, x, y, rotated);
            return true;
        }
    }

    public sealed class PlaceRoadCommand : Command
    {
        readonly List<(int X, int Y)> cells;

        public PlaceRoadCommand(IEnumerable<(int X, int Y)> cells)
        {
            this.cells = new List<(int X, int Y)>(cells);
        }

        public override bool Execute(World world, out string error)
        {
            error = null;
            var placed = 0;
            var wanted = 0;
            foreach (var (x, y) in cells)
            {
                if (world.Grid.IsRoad(x, y)) continue;
                wanted++;
                if (!world.Stock.Has(ResourceType.Gold, SimConstants.RoadGoldCost)) break;
                if (!world.AddRoad(x, y)) continue;
                world.Stock.Take(ResourceType.Gold, SimConstants.RoadGoldCost);
                placed++;
            }

            if (placed < wanted)
                error = placed == 0 ? "Дорогу не проложить: нет места или золота" : $"Проложено {placed} из {wanted} клеток дороги";
            return placed == wanted;
        }
    }

    public sealed class TogglePauseCommand : Command
    {
        readonly int buildingId;

        public TogglePauseCommand(int buildingId)
        {
            this.buildingId = buildingId;
        }

        public override bool Execute(World world, out string error)
        {
            error = null;
            var building = world.GetBuilding(buildingId);
            if (building == null) return false;
            world.SetPaused(building, !building.Paused);
            return true;
        }
    }

    public sealed class SetStockLimitCommand : Command
    {
        readonly ResourceType type;
        readonly int limit;

        public SetStockLimitCommand(ResourceType type, int limit)
        {
            this.type = type;
            this.limit = limit;
        }

        public override bool Execute(World world, out string error)
        {
            error = null;
            world.SetStockLimit(type, limit);
            return true;
        }
    }

    public sealed class HireHeroCommand : Command
    {
        readonly Combat.HeroClass heroClass;

        public HireHeroCommand(Combat.HeroClass heroClass)
        {
            this.heroClass = heroClass;
        }

        public override bool Execute(World world, out string error) => world.HireHero(heroClass, out error);
    }

    public sealed class SendExpeditionCommand : Command
    {
        readonly List<int> heroIds;

        public SendExpeditionCommand(IEnumerable<int> heroIds)
        {
            this.heroIds = new List<int>(heroIds);
        }

        public override bool Execute(World world, out string error) => world.SendExpedition(heroIds, out error);
    }

    public sealed class DemolishCommand : Command
    {
        readonly int x;
        readonly int y;

        public DemolishCommand(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override bool Execute(World world, out string error) => world.Demolish(x, y, out error);
    }
}
