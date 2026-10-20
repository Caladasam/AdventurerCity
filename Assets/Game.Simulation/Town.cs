using System;

namespace AdventurerCity.Simulation
{
    [Serializable]
    public sealed class Visitor
    {
        public Citizen Hero;
        public int Price;
        public int DaysLeft;
        public bool HasGear;
        public Item Gear = new Item();
    }

    [Serializable]
    public sealed class CaravanRecord
    {
        public bool Present;
        public int DaysLeft;
        public int NextDay;
        public int[] Buy = new int[ResourceInfo.Count];
        public int[] Sell = new int[ResourceInfo.Count];
        public int[] Supply = new int[ResourceInfo.Count];
        public int[] Demand = new int[ResourceInfo.Count];
    }

    public static class Town
    {
        public const int MaxVisitors = 3;
        public const int VisitorStayDays = 5;
        public const float VisitorChance = 0.25f;
        public const float VisitorChancePerFame = 0.02f;
        public const float VisitorChanceMax = 0.6f;
        public const int FamePerVisitorLevel = 4;
        public const int VisitorBasePrice = 60;
        public const int VisitorPricePerLevel = 50;
        public const int VisitorGearPrice = 30;
        public const float VisitorGearChance = 0.5f;
        public const int CaravanStayDays = 2;
        public const int CaravanIntervalMin = 5;
        public const int CaravanIntervalSpread = 3;
        public const float CaravanSellMarkup = 1.4f;
        public const float CaravanBuyMarkdown = 0.6f;

        public static readonly ResourceType[] Tradable =
        {
            ResourceType.Logs, ResourceType.Planks, ResourceType.Grain, ResourceType.Bread, ResourceType.Ore, ResourceType.Gems, ResourceType.Potions
        };

        static readonly int[] baseValue = { 1, 2, 4, 2, 3, 6, 25, 15 };
        static readonly int[] baseSupply = { 0, 40, 30, 40, 40, 20, 4, 5 };
        static readonly int[] baseDemand = { 0, 60, 40, 60, 50, 30, 8, 5 };

        public static int Value(ResourceType type) => baseValue[(int)type];
        public static int Supply(ResourceType type) => baseSupply[(int)type];
        public static int Demand(ResourceType type) => baseDemand[(int)type];
        public static int Lot(ResourceType type) => Value(type) >= 10 ? 1 : 5;

        public static int ItemPrice(Item item)
        {
            switch (item.Rarity)
            {
                case ItemRarity.Unique: return 120;
                case ItemRarity.Rare: return 40 + 15 * item.BaseTier;
                case ItemRarity.Magic: return 15 + 10 * item.BaseTier;
                default: return 3 + 3 * item.BaseTier;
            }
        }
    }
}
