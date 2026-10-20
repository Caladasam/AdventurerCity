using System.Collections.Generic;

namespace AdventurerCity.Simulation.Combat
{
    public enum RoomKind
    {
        Combat,
        Trap,
        Stash,
        Boss
    }

    public enum RoomAffix
    {
        None,
        Tough,
        Fierce,
        Reinforced,
        Weakened
    }

    public sealed class RoomTemplate
    {
        public string Name;
        public RoomKind Kind;
        public EnemyKind[] Enemies = new EnemyKind[0];
        public int LootMin;
        public int LootMax;
        public int GoldMin;
        public int GoldMax;
        public float TrapDamage;

        public bool IsCombat => Kind == RoomKind.Combat || Kind == RoomKind.Boss;
    }

    public sealed class DungeonDef
    {
        public int Id;
        public string Name;
        public RoomTemplate[] Pool;
        public RoomTemplate Boss;
        public ResourceType LootType;
        public int Distance;
        public float TravelSeconds => 20f * Distance;
        public int RoomsBeforeBoss = 4;
        public int MinCombatRooms = 3;
        public int ItemLevel = 1;
    }

    public static class Dungeons
    {
        public const int MineId = 1;
        public const int CatacombsId = 2;
        public const int WolfDenId = 3;
        public const int BanditCampId = 4;
        public const float HeroicScale = 1.5f;
        public const int HeroicLootMultiplier = 2;
        public const int StandardParty = 3;
        public static float PartyHpExponent = 0.67f;
        public static float PartyDamageExponent = 0.67f;
        public static float AffixChance = 0.5f;
        public static float ToughHp = 1.4f;
        public static float FierceDamage = 1.3f;
        public static float WeakenedHp = 0.7f;

        public static string AffixName(RoomAffix affix)
        {
            switch (affix)
            {
                case RoomAffix.Tough: return "крепкие враги";
                case RoomAffix.Fierce: return "свирепые враги";
                case RoomAffix.Reinforced: return "подкрепление";
                case RoomAffix.Weakened: return "потрёпанные враги";
                default: return null;
            }
        }

        static readonly DungeonDef[] all =
        {
            new DungeonDef
            {
                Id = MineId, Name = "Заброшенная шахта", LootType = ResourceType.Ore, Distance = 1, ItemLevel = 3,
                Pool = new[]
                {
                    Combat("Крысиное гнездо", 2, 4, 10, 20, EnemyKind.Ratman, EnemyKind.Ratman, EnemyKind.Ratman),
                    Combat("Забой", 3, 5, 15, 25, EnemyKind.Digger, EnemyKind.Digger, EnemyKind.Ratman),
                    Combat("Капище", 3, 5, 15, 25, EnemyKind.Digger, EnemyKind.Shaman),
                    new RoomTemplate { Name = "Просевший свод", Kind = RoomKind.Trap, TrapDamage = 15f },
                    new RoomTemplate { Name = "Заброшенная кладовая", Kind = RoomKind.Stash, LootMin = 5, LootMax = 10, GoldMin = 20, GoldMax = 40 }
                },
                Boss = new RoomTemplate
                {
                    Name = "Логово бригадира", Kind = RoomKind.Boss, Enemies = new[] { EnemyKind.Grok },
                    LootMin = 15, LootMax = 15, GoldMin = 100, GoldMax = 100
                }
            },
            new DungeonDef
            {
                Id = CatacombsId, Name = "Затопленные катакомбы", LootType = ResourceType.Gems, Distance = 2, ItemLevel = 7,
                Pool = new[]
                {
                    Combat("Склеп", 1, 2, 20, 35, EnemyKind.Skeleton, EnemyKind.Skeleton, EnemyKind.Ghoul),
                    Combat("Оссуарий", 1, 2, 20, 35, EnemyKind.Skeleton, EnemyKind.Skeleton, EnemyKind.Skeleton),
                    Combat("Ритуальный зал", 2, 3, 25, 40, EnemyKind.Ghoul, EnemyKind.BoneMage),
                    new RoomTemplate { Name = "Обрушенная галерея", Kind = RoomKind.Trap, TrapDamage = 25f },
                    new RoomTemplate { Name = "Тайник жреца", Kind = RoomKind.Stash, LootMin = 2, LootMax = 4, GoldMin = 40, GoldMax = 70 }
                },
                Boss = new RoomTemplate
                {
                    Name = "Усыпальница", Kind = RoomKind.Boss, Enemies = new[] { EnemyKind.CryptKeeper },
                    LootMin = 8, LootMax = 8, GoldMin = 200, GoldMax = 200
                }
            },
            new DungeonDef
            {
                Id = WolfDenId, Name = "Волчье логово", LootType = ResourceType.Gold, Distance = 1, ItemLevel = 2, RoomsBeforeBoss = 2, MinCombatRooms = 2,
                Pool = new[]
                {
                    Combat("Волчья тропа", 0, 0, 10, 20, EnemyKind.Wolf, EnemyKind.Wolf, EnemyKind.Wolf),
                    Combat("Лёжка", 0, 0, 10, 20, EnemyKind.Wolf, EnemyKind.Wolf)
                },
                Boss = new RoomTemplate
                {
                    Name = "Логово вожака", Kind = RoomKind.Boss, Enemies = new[] { EnemyKind.PackLeader },
                    GoldMin = 60, GoldMax = 60
                }
            },
            new DungeonDef
            {
                Id = BanditCampId, Name = "Лагерь разбойников", LootType = ResourceType.Bread, Distance = 2, ItemLevel = 5, RoomsBeforeBoss = 3, MinCombatRooms = 2,
                Pool = new[]
                {
                    Combat("Дозор", 0, 0, 15, 25, EnemyKind.Bandit, EnemyKind.Archer),
                    Combat("Костёр", 0, 0, 20, 30, EnemyKind.Bandit, EnemyKind.Bandit, EnemyKind.Archer),
                    new RoomTemplate { Name = "Склад награбленного", Kind = RoomKind.Stash, LootMin = 10, LootMax = 20, GoldMin = 20, GoldMax = 40 }
                },
                Boss = new RoomTemplate
                {
                    Name = "Шатёр атамана", Kind = RoomKind.Boss, Enemies = new[] { EnemyKind.Chieftain },
                    LootMin = 20, LootMax = 20, GoldMin = 150, GoldMax = 150
                }
            }
        };

        public static IReadOnlyList<DungeonDef> All => all;

        public static DungeonDef Get(int id)
        {
            foreach (var dungeon in all)
                if (dungeon.Id == id)
                    return dungeon;
            return all[0];
        }

        static RoomTemplate Combat(string name, int lootMin, int lootMax, int goldMin, int goldMax, params EnemyKind[] enemies) =>
            new RoomTemplate
            {
                Name = name, Kind = RoomKind.Combat, Enemies = enemies,
                LootMin = lootMin, LootMax = lootMax, GoldMin = goldMin, GoldMax = goldMax
            };
    }
}
