namespace AdventurerCity.Simulation.Combat
{
    public enum HeroClass
    {
        Guard,
        Priest,
        Warrior
    }

    public enum CombatRole
    {
        Tank,
        Healer,
        Damage
    }

    public enum EnemyKind
    {
        Ratman,
        Digger,
        Shaman,
        Grok,
        Skeleton,
        Ghoul,
        BoneMage,
        CryptKeeper,
        Wolf,
        PackLeader,
        Bandit,
        Archer,
        Chieftain
    }

    public readonly struct Stats
    {
        public readonly int Str;
        public readonly int Agi;
        public readonly int Int;
        public readonly int Spi;
        public readonly int Sta;

        public Stats(int str, int agi, int intellect, int spi, int sta)
        {
            Str = str;
            Agi = agi;
            Int = intellect;
            Spi = spi;
            Sta = sta;
        }

        public Stats Grow(in Stats growth, int levels) => new Stats(
            Str + growth.Str * levels,
            Agi + growth.Agi * levels,
            Int + growth.Int * levels,
            Spi + growth.Spi * levels,
            Sta + growth.Sta * levels);
    }

    public sealed class HeroClassDef
    {
        public HeroClass Class;
        public string Name;
        public CombatRole Role;
        public Stats Base;
        public Stats Growth;
        public int Armor;
        public int Weapon;
        public float AttackInterval;
        public bool MagicAttack;
    }

    public sealed class EnemyDef
    {
        public EnemyKind Kind;
        public string Name;
        public int Level;
        public int Hp;
        public int Armor;
        public int Damage;
        public float AttackInterval;
        public bool MagicAttack;
        public float HealAmount;
        public float HealInterval;
        public float HealCast;
        public string AoeName;
        public float AoeDamage;
        public float AoeInterval;
        public float AoeCast;
        public float AoeFirst;
        public EnemyKind SummonKind;
        public int SummonCount;
        public float SummonAtHpRatio = 0.5f;
    }

    public static class CombatData
    {
        public const float Tick = 0.1f;
        public const float GlobalCooldown = 1f;
        public const float DownedSeconds = 15f;
        public const float MaxBattleSeconds = 300f;
        public const float EnemyCritChance = 0.05f;
        public const float CritMultiplier = 1.5f;
        public const float DamageSpread = 0.1f;
        public const float GuardThreatMultiplier = 3f;
        public const float ShieldBashThreatMultiplier = 5f;
        public const float GuardOpeningThreat = 25f;
        public const float DamageDealerDelay = 1f;
        public const float HealThreatPerPoint = 0.5f;
        public const float InjuryHpPenalty = 0.1f;
        public const int XpPerEnemyLevel = 20;
        public const int XpPerLevel = 250;
        public const int MaxLevel = 5;

        static readonly HeroClassDef[] classes =
        {
            new HeroClassDef
            {
                Class = HeroClass.Guard, Name = "Страж", Role = CombatRole.Tank,
                Base = new Stats(8, 4, 2, 4, 10), Growth = new Stats(1, 1, 0, 1, 2),
                Armor = 30, Weapon = 6, AttackInterval = 2.0f
            },
            new HeroClassDef
            {
                Class = HeroClass.Priest, Name = "Жрец", Role = CombatRole.Healer,
                Base = new Stats(2, 3, 7, 10, 6), Growth = new Stats(0, 1, 1, 2, 1),
                Armor = 5, Weapon = 4, AttackInterval = 2.0f, MagicAttack = true
            },
            new HeroClassDef
            {
                Class = HeroClass.Warrior, Name = "Воин", Role = CombatRole.Damage,
                Base = new Stats(10, 7, 2, 2, 7), Growth = new Stats(2, 1, 0, 0, 1),
                Armor = 15, Weapon = 12, AttackInterval = 2.4f
            }
        };

        static readonly EnemyDef[] enemies =
        {
            new EnemyDef { Kind = EnemyKind.Ratman, Name = "Крысолюд", Level = 1, Hp = 90, Armor = 5, Damage = 9, AttackInterval = 1.6f },
            new EnemyDef { Kind = EnemyKind.Digger, Name = "Гоблин-копатель", Level = 2, Hp = 160, Armor = 10, Damage = 15, AttackInterval = 2.0f },
            new EnemyDef
            {
                Kind = EnemyKind.Shaman, Name = "Гоблин-шаман", Level = 2, Hp = 110, Armor = 0, Damage = 9, AttackInterval = 2.0f, MagicAttack = true,
                HealAmount = 35f, HealInterval = 6f, HealCast = 1.5f
            },
            new EnemyDef
            {
                Kind = EnemyKind.Grok, Name = "Бригадир Грок", Level = 3, Hp = 1450, Armor = 20, Damage = 35, AttackInterval = 2.5f,
                AoeName = "Обвал", AoeDamage = 40f, AoeInterval = 15f, AoeCast = 2.5f, AoeFirst = 8f,
                SummonKind = EnemyKind.Ratman, SummonCount = 2
            },
            new EnemyDef { Kind = EnemyKind.Skeleton, Name = "Скелет", Level = 3, Hp = 140, Armor = 15, Damage = 14, AttackInterval = 1.8f },
            new EnemyDef { Kind = EnemyKind.Ghoul, Name = "Упырь", Level = 3, Hp = 180, Armor = 5, Damage = 18, AttackInterval = 2.2f },
            new EnemyDef
            {
                Kind = EnemyKind.BoneMage, Name = "Костяной маг", Level = 3, Hp = 110, Armor = 0, Damage = 12, AttackInterval = 2.0f, MagicAttack = true,
                AoeName = "Волна тлена", AoeDamage = 20f, AoeInterval = 10f, AoeCast = 2f, AoeFirst = 4f
            },
            new EnemyDef
            {
                Kind = EnemyKind.CryptKeeper, Name = "Хранитель склепа", Level = 4, Hp = 2200, Armor = 25, Damage = 40, AttackInterval = 2.6f,
                AoeName = "Проклятие", AoeDamage = 45f, AoeInterval = 14f, AoeCast = 2.5f, AoeFirst = 6f,
                SummonKind = EnemyKind.Skeleton, SummonCount = 2
            },
            new EnemyDef { Kind = EnemyKind.Wolf, Name = "Волк", Level = 1, Hp = 70, Armor = 0, Damage = 10, AttackInterval = 1.4f },
            new EnemyDef
            {
                Kind = EnemyKind.PackLeader, Name = "Вожак стаи", Level = 2, Hp = 700, Armor = 5, Damage = 22, AttackInterval = 1.8f,
                SummonKind = EnemyKind.Wolf, SummonCount = 2
            },
            new EnemyDef { Kind = EnemyKind.Bandit, Name = "Разбойник", Level = 2, Hp = 130, Armor = 10, Damage = 13, AttackInterval = 1.8f },
            new EnemyDef { Kind = EnemyKind.Archer, Name = "Стрелок", Level = 2, Hp = 90, Armor = 0, Damage = 16, AttackInterval = 2.2f },
            new EnemyDef
            {
                Kind = EnemyKind.Chieftain, Name = "Атаман", Level = 3, Hp = 1100, Armor = 15, Damage = 30, AttackInterval = 2.2f,
                AoeName = "Залп", AoeDamage = 30f, AoeInterval = 12f, AoeCast = 2f, AoeFirst = 6f,
                SummonKind = EnemyKind.Bandit, SummonCount = 2
            }
        };

        public static HeroClassDef Get(HeroClass heroClass) => classes[(int)heroClass];

        public static EnemyDef Get(EnemyKind kind) => enemies[(int)kind];
    }

    public sealed class Rng
    {
        uint state;

        public Rng(int seed)
        {
            state = (uint)seed * 2654435761u ^ 0x9E3779B9u;
            if (state == 0) state = 1;
        }

        uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public float Value => (Next() & 0xFFFFFF) / 16777216f;

        public int Range(int min, int maxInclusive) => min + (int)(Next() % (uint)(maxInclusive - min + 1));

        public bool Chance(float probability) => Value < probability;

        public float Spread(float value, float spread) => value * (1f - spread + 2f * spread * Value);
    }
}
