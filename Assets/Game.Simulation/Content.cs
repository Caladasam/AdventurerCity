using System;
using System.Collections.Generic;

namespace AdventurerCity.Simulation
{
    public enum BuildingKind
    {
        Storehouse,
        House,
        Woodcutter,
        Farm,
        Sawmill,
        Bakery,
        Guild
    }

    public sealed class Recipe
    {
        public ResourceAmount[] Inputs = Array.Empty<ResourceAmount>();
        public ResourceAmount[] Outputs = Array.Empty<ResourceAmount>();
        public int CycleTicks;
    }

    public sealed class BuildingDef
    {
        public BuildingKind Kind;
        public string Name;
        public int Width;
        public int Depth;
        public ResourceAmount[] Cost;
        public int BuildTicks;
        public int Workers;
        public Recipe Recipe;
        public int StorageRadius;
    }

    public sealed class HouseLevelDef
    {
        public string Name;
        public int Capacity;
        public int TaxPerResident;
        public bool NeedsFirewood;
        public bool NeedsBread;
        public int UpgradePlanks;
        public int UpgradeOre;
    }

    public sealed class ContentLibrary
    {
        readonly Dictionary<BuildingKind, BuildingDef> byKind = new Dictionary<BuildingKind, BuildingDef>();
        readonly List<BuildingDef> buildings = new List<BuildingDef>();

        ContentLibrary(IEnumerable<BuildingDef> defs, IReadOnlyList<HouseLevelDef> houseLevels)
        {
            foreach (var def in defs)
            {
                byKind.Add(def.Kind, def);
                buildings.Add(def);
            }
            HouseLevels = houseLevels;
        }

        public IReadOnlyList<BuildingDef> Buildings => buildings;
        public IReadOnlyList<HouseLevelDef> HouseLevels { get; }

        public BuildingDef Get(BuildingKind kind) => byKind[kind];

        public static ContentLibrary CreateDefault()
        {
            var defs = new[]
            {
                new BuildingDef
                {
                    Kind = BuildingKind.Storehouse, Name = "Склад", Width = 3, Depth = 3,
                    Cost = Cost(R(ResourceType.Planks, 20), R(ResourceType.Gold, 20)), BuildTicks = 20,
                    StorageRadius = 12
                },
                new BuildingDef
                {
                    Kind = BuildingKind.House, Name = "Жилой дом", Width = 2, Depth = 2,
                    Cost = Cost(R(ResourceType.Logs, 10)), BuildTicks = 10
                },
                new BuildingDef
                {
                    Kind = BuildingKind.Woodcutter, Name = "Лесоруб", Width = 2, Depth = 2,
                    Cost = Cost(R(ResourceType.Logs, 5), R(ResourceType.Planks, 5)), BuildTicks = 15, Workers = 2,
                    Recipe = new Recipe { Outputs = Cost(R(ResourceType.Logs, 2)), CycleTicks = 10 }
                },
                new BuildingDef
                {
                    Kind = BuildingKind.Farm, Name = "Ферма", Width = 3, Depth = 3,
                    Cost = Cost(R(ResourceType.Planks, 10), R(ResourceType.Logs, 5)), BuildTicks = 20, Workers = 3,
                    Recipe = new Recipe { Outputs = Cost(R(ResourceType.Grain, 4)), CycleTicks = 15 }
                },
                new BuildingDef
                {
                    Kind = BuildingKind.Sawmill, Name = "Лесопилка", Width = 3, Depth = 2,
                    Cost = Cost(R(ResourceType.Logs, 10), R(ResourceType.Gold, 20)), BuildTicks = 20, Workers = 2,
                    Recipe = new Recipe
                    {
                        Inputs = Cost(R(ResourceType.Logs, 2)), Outputs = Cost(R(ResourceType.Planks, 1)), CycleTicks = 10
                    }
                },
                new BuildingDef
                {
                    Kind = BuildingKind.Bakery, Name = "Пекарня", Width = 2, Depth = 2,
                    Cost = Cost(R(ResourceType.Planks, 15), R(ResourceType.Gold, 30)), BuildTicks = 20, Workers = 2,
                    Recipe = new Recipe
                    {
                        Inputs = Cost(R(ResourceType.Grain, 2)), Outputs = Cost(R(ResourceType.Bread, 3)), CycleTicks = 15
                    }
                },
                new BuildingDef
                {
                    Kind = BuildingKind.Guild, Name = "Гильдия", Width = 3, Depth = 3,
                    Cost = Cost(R(ResourceType.Planks, 30), R(ResourceType.Logs, 20), R(ResourceType.Gold, 50)), BuildTicks = 30
                }
            };

            var houseLevels = new[]
            {
                new HouseLevelDef { Name = "Хижина", Capacity = 2, TaxPerResident = 1, UpgradePlanks = 5 },
                new HouseLevelDef { Name = "Дом", Capacity = 4, TaxPerResident = 2, NeedsFirewood = true, UpgradePlanks = 10, UpgradeOre = 5 },
                new HouseLevelDef { Name = "Добротный дом", Capacity = 6, TaxPerResident = 3, NeedsFirewood = true, NeedsBread = true }
            };

            return new ContentLibrary(defs, houseLevels);
        }

        static ResourceAmount R(ResourceType type, int amount) => new ResourceAmount(type, amount);

        static ResourceAmount[] Cost(params ResourceAmount[] amounts) => amounts;
    }
}
