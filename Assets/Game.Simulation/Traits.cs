using System;
using System.Collections.Generic;

namespace AdventurerCity.Simulation
{
    public enum HeroTrait
    {
        None,
        Brave,
        Coward,
        Greedy,
        Scholar,
        Sturdy,
        Frail,
        Sharpshooter,
        Devout,
        Tireless,
        Lazy,
        Deathtouched
    }

    public sealed class TraitDef
    {
        public HeroTrait Trait;
        public string Name;
        public string Description;
        public bool Negative;
        public float Damage;
        public float Health;
        public float Dodge;
        public float Crit;
        public float Healing;
        public float DownedScale = 1f;
        public float Xp;
        public float Loot;
        public int Wage;
        public int InjuryRecovery;
        public int TrainingDays;
    }

    [Serializable]
    public sealed class Relation
    {
        public int A;
        public int B;
        public int Value;
    }

    [Serializable]
    public sealed class ChronicleEntry
    {
        public int HeroId;
        public int Day;
        public string Text;
    }

    [Serializable]
    public sealed class FallenHero
    {
        public Citizen Hero;
        public int Day;
        public string Cause;
    }

    public static class Traits
    {
        public const int FriendThreshold = 5;
        public const int RivalThreshold = -5;
        public const float BondDamage = 0.05f;
        public const int RelationMax = 10;
        public const int ChroniclePerHero = 30;
        public const int ResurrectGoldPerLevel = 100;
        public const int ResurrectOrePerLevel = 5;
        public const int ResurrectInjuryDays = 5;
        public const int TempleInjuryRecovery = 1;

        static readonly TraitDef[] defs =
        {
            new TraitDef { Trait = HeroTrait.None, Name = "" },
            new TraitDef
            {
                Trait = HeroTrait.Brave, Name = "Храбрый", Damage = 0.1f, DownedScale = 0.7f,
                Description = "+10% урона, но при смерти держится на 30% меньше"
            },
            new TraitDef
            {
                Trait = HeroTrait.Coward, Name = "Трус", Negative = true, Damage = -0.1f, Dodge = 0.08f, DownedScale = 1.5f,
                Description = "−10% урона, +8% уклонения, при смерти держится дольше"
            },
            new TraitDef { Trait = HeroTrait.Greedy, Name = "Жадный", Loot = 0.1f, Wage = 1, Description = "+10% добычи отряда, жалованье на 1 золото больше" },
            new TraitDef { Trait = HeroTrait.Scholar, Name = "Учёный", Xp = 0.15f, TrainingDays = -1, Description = "+15% опыта, учит классы на день быстрее" },
            new TraitDef { Trait = HeroTrait.Sturdy, Name = "Крепкий", Health = 0.15f, Description = "+15% здоровья" },
            new TraitDef { Trait = HeroTrait.Frail, Name = "Хрупкий", Negative = true, Health = -0.1f, Description = "−10% здоровья" },
            new TraitDef { Trait = HeroTrait.Sharpshooter, Name = "Меткий", Crit = 0.06f, Description = "+6% шанса крита" },
            new TraitDef { Trait = HeroTrait.Devout, Name = "Набожный", Healing = 0.15f, Description = "+15% силы лечения" },
            new TraitDef { Trait = HeroTrait.Tireless, Name = "Неутомимый", InjuryRecovery = 1, Description = "Ранения заживают вдвое быстрее" },
            new TraitDef { Trait = HeroTrait.Lazy, Name = "Лентяй", Negative = true, Xp = -0.15f, Description = "−15% опыта" },
            new TraitDef
            {
                Trait = HeroTrait.Deathtouched, Name = "Видевший смерть", Health = -0.1f, Damage = 0.1f,
                Description = "Вернулся с того света: −10% здоровья, +10% урона"
            }
        };

        static readonly (HeroTrait, HeroTrait)[] conflicts =
        {
            (HeroTrait.Brave, HeroTrait.Coward),
            (HeroTrait.Sturdy, HeroTrait.Frail),
            (HeroTrait.Scholar, HeroTrait.Lazy)
        };

        public static TraitDef Get(HeroTrait trait) => defs[(int)trait];

        public static IReadOnlyList<TraitDef> All => defs;

        public static bool Conflict(HeroTrait a, HeroTrait b)
        {
            if (a == b) return true;
            foreach (var (x, y) in conflicts)
                if (a == x && b == y || a == y && b == x)
                    return true;
            return false;
        }

        public static IEnumerable<HeroTrait> Of(Citizen hero)
        {
            if (hero.Trait1 != HeroTrait.None) yield return hero.Trait1;
            if (hero.Trait2 != HeroTrait.None) yield return hero.Trait2;
            if (hero.Trait3 != HeroTrait.None) yield return hero.Trait3;
        }

        public static string BondName(int value) =>
            value >= FriendThreshold ? "друзья" : value <= RivalThreshold ? "соперники" : value > 0 ? "приятели" : value < 0 ? "не ладят" : "знакомы";
    }
}
