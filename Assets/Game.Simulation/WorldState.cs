using System;
using System.Collections.Generic;

namespace AdventurerCity.Simulation
{
    [Serializable]
    public sealed class Building
    {
        public int Id;
        public BuildingKind Kind;
        public int X;
        public int Y;
        public bool Rotated;
        public int ConstructionLeft;
        public float Progress;
        public int Level = 1;
        public int SatisfiedDays;
        public int UnhappyDays;
        public bool FoodMet = true;
        public bool FirewoodMet = true;
        public bool Paused;
        public bool Priority;

        [NonSerialized] public int Residents;
        [NonSerialized] public bool LimitReached;
        [NonSerialized] public int Workers;
        [NonSerialized] public int RoadDistance = -1;
        [NonSerialized] public float Efficiency;
        [NonSerialized] public bool Stalled;

        public bool IsComplete => ConstructionLeft <= 0;
        public bool IsConnected => RoadDistance >= 0;
        public bool IsProducing => Workers > 0 && !Paused && !LimitReached;
    }

    [Serializable]
    public sealed class WorldState
    {
        public const int CurrentFormat = 2;

        public int Format = CurrentFormat;
        public int Width;
        public int Height;
        public long Tick;
        public int NextId = 1;
        public int NextCitizenId = 1;
        public int RngState = 123456789;
        public int[] Stock;
        public int[] StockLimits;
        public List<Building> Buildings = new List<Building>();
        public List<int> Roads = new List<int>();
        public List<Citizen> Citizens = new List<Citizen>();
        public List<ExpeditionRecord> Expeditions = new List<ExpeditionRecord>();
        public List<ForgeOrder> ForgeOrders = new List<ForgeOrder>();
        public List<SiteState> Sites = new List<SiteState>();
        public List<ScoutRecord> Scouts = new List<ScoutRecord>();
        public List<RaidRecord> Raids = new List<RaidRecord>();
        public List<int> Titles = new List<int>();
        public List<Relation> Relations = new List<Relation>();
        public List<ChronicleEntry> Chronicle = new List<ChronicleEntry>();
        public List<FallenHero> Fallen = new List<FallenHero>();
        public List<Item> Items = new List<Item>();
        public int NextItemId = 1;
        public int ItemsVersion;
        public int Stage;
        public int Fame;
        public List<Visitor> Visitors = new List<Visitor>();
        public CaravanRecord Caravan = new CaravanRecord();
        public int NextExpeditionId = 1;
    }

    [Serializable]
    public sealed class ForgeOrder
    {
        public int HeroId;
        public Combat.GearSlot Slot;
        public int BaseId;
        public int Tier;
        public float Progress;
    }

    [Serializable]
    public sealed class ExpeditionRecord
    {
        public int Id;
        public int Seed;
        public List<int> HeroIds = new List<int>();
        public float Elapsed;
        public int DungeonId = Combat.Dungeons.MineId;
        public bool Heroic;
        public int Potions;
        public List<Combat.CombatOrder> Orders = new List<Combat.CombatOrder>();
    }

    public readonly struct SimEvent
    {
        public readonly long Tick;
        public readonly string Text;

        public SimEvent(long tick, string text)
        {
            Tick = tick;
            Text = text;
        }

        public int Day => (int)(Tick / SimConstants.TicksPerDay) + 1;
    }
}
