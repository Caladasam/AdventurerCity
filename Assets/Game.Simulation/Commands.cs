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

    public sealed class TogglePriorityCommand : Command
    {
        readonly int buildingId;

        public TogglePriorityCommand(int buildingId)
        {
            this.buildingId = buildingId;
        }

        public override bool Execute(World world, out string error)
        {
            error = null;
            var building = world.GetBuilding(buildingId);
            if (building == null) return false;
            world.SetPriority(building, !building.Priority);
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

    public sealed class LearnClassCommand : Command
    {
        readonly int heroId;
        readonly Combat.HeroClass heroClass;

        public LearnClassCommand(int heroId, Combat.HeroClass heroClass)
        {
            this.heroId = heroId;
            this.heroClass = heroClass;
        }

        public override bool Execute(World world, out string error) => world.LearnClass(heroId, heroClass, out error);
    }

    public sealed class SetAbilitySlotCommand : Command
    {
        readonly int heroId;
        readonly int slot;
        readonly Combat.Ability ability;

        public SetAbilitySlotCommand(int heroId, int slot, Combat.Ability ability)
        {
            this.heroId = heroId;
            this.slot = slot;
            this.ability = ability;
        }

        public override bool Execute(World world, out string error) => world.SetAbilitySlot(heroId, slot, ability, out error);
    }

    public sealed class ResurrectCommand : Command
    {
        readonly int heroId;

        public ResurrectCommand(int heroId)
        {
            this.heroId = heroId;
        }

        public override bool Execute(World world, out string error) => world.Resurrect(heroId, out error);
    }

    public sealed class SendExpeditionCommand : Command
    {
        readonly List<int> heroIds;
        readonly int dungeonId;
        readonly bool heroic;

        public SendExpeditionCommand(IEnumerable<int> heroIds, int dungeonId = Combat.Dungeons.MineId, bool heroic = false)
        {
            this.heroIds = new List<int>(heroIds);
            this.dungeonId = dungeonId;
            this.heroic = heroic;
        }

        public override bool Execute(World world, out string error) => world.SendExpedition(heroIds, dungeonId, heroic, out error);
    }

    public sealed class ScoutCommand : Command
    {
        readonly int siteId;

        public ScoutCommand(int siteId)
        {
            this.siteId = siteId;
        }

        public override bool Execute(World world, out string error) => world.Scout(siteId, out error);
    }

    public sealed class CraftItemCommand : Command
    {
        readonly int baseId;
        readonly int tier;

        public CraftItemCommand(int baseId, int tier)
        {
            this.baseId = baseId;
            this.tier = tier;
        }

        public override bool Execute(World world, out string error) => world.Craft(baseId, tier, out error);
    }

    public sealed class EquipItemCommand : Command
    {
        readonly int heroId;
        readonly int itemId;

        public EquipItemCommand(int heroId, int itemId)
        {
            this.heroId = heroId;
            this.itemId = itemId;
        }

        public override bool Execute(World world, out string error) => world.Equip(heroId, itemId, out error);
    }

    public sealed class UnequipItemCommand : Command
    {
        readonly int heroId;
        readonly ItemSlot slot;

        public UnequipItemCommand(int heroId, ItemSlot slot)
        {
            this.heroId = heroId;
            this.slot = slot;
        }

        public override bool Execute(World world, out string error) => world.Unequip(heroId, slot, out error);
    }

    public sealed class SalvageItemCommand : Command
    {
        readonly int itemId;

        public SalvageItemCommand(int itemId)
        {
            this.itemId = itemId;
        }

        public override bool Execute(World world, out string error) => world.Salvage(itemId, out error);
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

    public sealed class OrderUniqueCommand : Command
    {
        readonly int uniqueId;

        public OrderUniqueCommand(int uniqueId)
        {
            this.uniqueId = uniqueId;
        }

        public override bool Execute(World world, out string error) => world.OrderUnique(uniqueId, out error);
    }

    public sealed class AdvanceStageCommand : Command
    {
        public override bool Execute(World world, out string error) => world.AdvanceStage(out error);
    }

    public sealed class HireVisitorCommand : Command
    {
        readonly int heroId;

        public HireVisitorCommand(int heroId)
        {
            this.heroId = heroId;
        }

        public override bool Execute(World world, out string error) => world.HireVisitor(heroId, out error);
    }

    public sealed class TradeCommand : Command
    {
        readonly ResourceType type;
        readonly int amount;
        readonly bool buy;

        public TradeCommand(ResourceType type, int amount, bool buy)
        {
            this.type = type;
            this.amount = amount;
            this.buy = buy;
        }

        public override bool Execute(World world, out string error) => world.Trade(type, amount, buy, out error);
    }

    public sealed class SellItemCommand : Command
    {
        readonly int itemId;

        public SellItemCommand(int itemId)
        {
            this.itemId = itemId;
        }

        public override bool Execute(World world, out string error) => world.SellItem(itemId, out error);
    }

    public sealed class CombatOrderCommand : Command
    {
        readonly int expeditionId;
        readonly Combat.OrderKind kind;
        readonly int target;

        public CombatOrderCommand(int expeditionId, Combat.OrderKind kind, int target = -1)
        {
            this.expeditionId = expeditionId;
            this.kind = kind;
            this.target = target;
        }

        public override bool Execute(World world, out string error) => world.GiveOrder(expeditionId, kind, target, out error);
    }
}
