namespace AdventurerCity.Simulation.Combat
{
    public enum GearSlot
    {
        Weapon,
        Armor
    }

    public sealed class GearTierDef
    {
        public string WeaponName;
        public string ArmorName;
        public int WeaponBonus;
        public int HealBonus;
        public int ArmorBonus;
        public ResourceAmount[] WeaponCost;
        public ResourceAmount[] ArmorCost;
        public int ForgeTicks;
    }

    public static class GearData
    {
        public const int MaxTier = 2;

        static readonly GearTierDef[] tiers =
        {
            new GearTierDef { WeaponName = "простое", ArmorName = "простая" },
            new GearTierDef
            {
                WeaponName = "железное", ArmorName = "железная",
                WeaponBonus = 1, HealBonus = 3, ArmorBonus = 3,
                WeaponCost = new[] { R(ResourceType.Ore, 10), R(ResourceType.Planks, 10), R(ResourceType.Gold, 60) },
                ArmorCost = new[] { R(ResourceType.Ore, 12), R(ResourceType.Planks, 5), R(ResourceType.Gold, 60) },
                ForgeTicks = 30
            },
            new GearTierDef
            {
                WeaponName = "стальное", ArmorName = "стальная",
                WeaponBonus = 3, HealBonus = 6, ArmorBonus = 6,
                WeaponCost = new[] { R(ResourceType.Ore, 25), R(ResourceType.Planks, 15), R(ResourceType.Gold, 150) },
                ArmorCost = new[] { R(ResourceType.Ore, 30), R(ResourceType.Planks, 10), R(ResourceType.Gold, 150) },
                ForgeTicks = 60
            }
        };

        public static GearTierDef Tier(int tier) => tiers[tier];

        public static ResourceAmount[] Cost(GearSlot slot, int tier) =>
            slot == GearSlot.Weapon ? tiers[tier].WeaponCost : tiers[tier].ArmorCost;

        public static string Name(GearSlot slot, int tier) =>
            slot == GearSlot.Weapon ? tiers[tier].WeaponName + " оружие" : tiers[tier].ArmorName + " броня";

        static ResourceAmount R(ResourceType type, int amount) => new ResourceAmount(type, amount);
    }
}
