using System;
using System.Collections.Generic;

namespace AdventurerCity.Simulation
{
    public sealed class FlowEntry
    {
        public string Label;
        public int Count;
        public float PerDay;
    }

    public sealed class ResourceFlow
    {
        public readonly List<FlowEntry> Sources = new List<FlowEntry>();
        public readonly List<FlowEntry> Sinks = new List<FlowEntry>();

        public float Produced { get; private set; }
        public float Consumed { get; private set; }
        public float Net => Produced - Consumed;

        internal void Produce(string label, float perDay) => Add(Sources, label, perDay, true);

        internal void Consume(string label, float perDay) => Add(Sinks, label, perDay, false);

        void Add(List<FlowEntry> list, string label, float perDay, bool produced)
        {
            if (perDay <= 0f) return;
            if (produced) Produced += perDay;
            else Consumed += perDay;

            foreach (var entry in list)
            {
                if (entry.Label != label) continue;
                entry.Count++;
                entry.PerDay += perDay;
                return;
            }
            list.Add(new FlowEntry { Label = label, Count = 1, PerDay = perDay });
        }
    }

    public static class Economy
    {
        public static ResourceFlow[] Compute(World world)
        {
            var flows = new ResourceFlow[ResourceInfo.Count];
            for (var i = 0; i < flows.Length; i++) flows[i] = new ResourceFlow();

            foreach (var building in world.Buildings)
            {
                if (!building.IsComplete) continue;
                var def = world.Def(building);
                if (def.Recipe == null || building.Workers == 0 || building.Paused) continue;

                var cycles = CyclesPerDay(world, building, true);
                foreach (var output in def.Recipe.Outputs)
                    flows[(int)output.Type].Produce(def.Name, cycles * output.Amount);
                foreach (var input in def.Recipe.Inputs)
                    flows[(int)input.Type].Consume(def.Name, cycles * input.Amount);
            }

            var strictBread = 0f;
            var flexibleFood = 0f;
            foreach (var house in world.Buildings)
            {
                if (house.Kind != BuildingKind.House || !house.IsComplete || !house.IsConnected || house.Residents == 0)
                    continue;

                var level = world.HouseLevel(house);
                var food = house.Residents * SimConstants.FoodPerResident;
                if (level.NeedsBread) strictBread += food;
                else flexibleFood += food;
                if (level.NeedsFirewood)
                    flows[(int)ResourceType.Logs].Consume("Дрова для домов", SimConstants.FirewoodPerHouse);
                flows[(int)ResourceType.Gold].Produce("Налоги", house.Residents * level.TaxPerResident);
            }

            foreach (var building in world.Buildings)
            {
                var upkeep = world.Def(building).Upkeep;
                if (upkeep > 0 && building.IsComplete) flows[(int)ResourceType.Gold].Consume("Содержание зданий", upkeep);
            }

            foreach (var citizen in world.Citizens)
            {
                if (!citizen.IsHero) continue;
                flows[(int)ResourceType.Gold].Consume("Жалованье героев", world.HeroWage(citizen));
                if (!citizen.OnExpedition)
                    flows[(int)ResourceType.Bread].Consume("Паёк героев", SimConstants.HeroBreadPerDay);
            }

            var grain = flows[(int)ResourceType.Grain];
            var grainForFood = world.Stock[ResourceType.Grain] >= flexibleFood
                ? flexibleFood
                : Math.Min(flexibleFood, Math.Max(0f, grain.Produced - grain.Consumed));
            grain.Consume("Еда жителей", grainForFood);
            flows[(int)ResourceType.Bread].Consume("Еда жителей", strictBread + flexibleFood - grainForFood);

            return flows;
        }

        public static float CyclesPerDay(World world, Building building, bool ignoreLimit = false)
        {
            var def = world.Def(building);
            if (def.Recipe == null || !building.IsComplete || building.Workers == 0 || building.Paused) return 0f;
            if (building.LimitReached && !ignoreLimit) return 0f;
            return SimConstants.TicksPerDay * building.Efficiency * building.Workers / def.Workers / def.Recipe.CycleTicks;
        }

        public static float DaysLeft(int stock, ResourceFlow flow) =>
            flow.Net >= 0f ? float.PositiveInfinity : stock / -flow.Net;
    }
}
