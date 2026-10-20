using System.Collections.Generic;

namespace AdventurerCity.Simulation.Combat
{
    public enum HeroClass
    {
        Fighter,
        Cleric,
        Barbarian,
        Paladin,
        Druid,
        Bard,
        Rogue,
        Monk,
        Ranger,
        Wizard,
        Sorcerer,
        Warlock
    }

    public enum CombatRole
    {
        Tank,
        Healer,
        Support,
        Melee,
        Ranged
    }

    public enum PowerStat
    {
        Str,
        Agi,
        Int
    }

    public enum Ability
    {
        None,
        ShieldBash,
        Taunt,
        DefensiveStance,
        Revive,
        Heal,
        ShieldOfFaith,
        Rage,
        RecklessStrike,
        Whirlwind,
        DivineSmite,
        LayOnHands,
        AuraOfProtection,
        Rejuvenation,
        HealingWave,
        EntanglingVines,
        Inspiration,
        ViciousMockery,
        SongOfRest,
        SneakAttack,
        PoisonBlade,
        Evasion,
        FlurryOfBlows,
        StunningStrike,
        PatientDefense,
        HuntersMark,
        AimedShot,
        Volley,
        MagicMissile,
        Fireball,
        Counterspell,
        ChaosBolt,
        ChainLightning,
        BloodMagic,
        EldritchBlast,
        Hex,
        LifeDrain
    }

    public enum AbilityEffect
    {
        Strike,
        MultiStrike,
        AreaStrike,
        ChainStrike,
        RandomStrike,
        WeakenStrike,
        Drain,
        Interrupt,
        Taunt,
        SelfGuard,
        SelfRage,
        SelfEvade,
        SelfEmpower,
        Heal,
        HealOverTime,
        GroupHeal,
        Shield,
        Revive,
        PartyGuard,
        PartyEmpower,
        Mark,
        DamageOverTime,
        AreaStun
    }

    public enum AbilityTrigger
    {
        Ready,
        EnemyCasting,
        EnemyOnAlly,
        SelfHurt,
        AllyHurt,
        AllyCritical,
        AllyDowned,
        GroupHurt,
        TankUnshielded,
        AllyNeedsHot,
        TwoEnemies,
        ThreeEnemies,
        TargetUnmarked,
        TargetClean,
        SelfTargeted,
        NotTargeted
    }

    public sealed class AbilityDef
    {
        public Ability Id;
        public string Name;
        public HeroClass Class;
        public AbilityEffect Effect;
        public AbilityTrigger Trigger;
        public int Priority;
        public float Cooldown;
        public float Cast;
        public float Mana;
        public float Power;
        public float Scaling;
        public float Duration;
        public float Amount;
        public int Hits;
        public bool Magic;
        public float Threat = 1f;
        public bool Interrupts;
        public float Stun;
        public string Description;

        public string Text => Description
            .Replace("{power}", Power.ToString("0.##"))
            .Replace("{max}", (Power + Amount).ToString("0.##"))
            .Replace("{amount}", (Amount * 100f).ToString("0"))
            .Replace("{duration}", Duration.ToString("0"));
    }

    public sealed class HeroClassDef
    {
        public HeroClass Class;
        public string Name;
        public CombatRole Role;
        public PowerStat PowerStat;
        public bool UsesMana;
        public Stats Base;
        public Stats Growth;
        public int Armor;
        public float Weapon;
        public float AttackInterval;
        public bool MagicAttack;
        public Ability[] Abilities;
        public string Description;
    }

    public enum TitleBonus
    {
        Health,
        Damage,
        Healing,
        Dodge,
        Armor
    }

    public sealed class TitleDef
    {
        public int Id;
        public string Name;
        public HeroClass A;
        public HeroClass B;
        public TitleBonus Bonus;
        public float Amount;

        public string BonusText
        {
            get
            {
                switch (Bonus)
                {
                    case TitleBonus.Health: return $"+{Amount * 100f:0}% здоровья";
                    case TitleBonus.Damage: return $"+{Amount * 100f:0}% урона";
                    case TitleBonus.Healing: return $"+{Amount * 100f:0}% лечения";
                    case TitleBonus.Dodge: return $"+{Amount * 100f:0}% уклонения";
                    default: return $"+{Amount:0} брони";
                }
            }
        }
    }

    public static class Classes
    {
        public const int SecondClassLevel = 3;
        public const int ThirdClassLevel = 5;
        public const int FreeSlots = 2;
        public const int TrainingGold = 100;
        public const int TrainingDays = 3;
        public const float MarkBonus = 0.2f;
        public const float WeakenPenalty = 0.25f;
        public const float EvadeBonus = 0.4f;

        static readonly HeroClassDef[] classes =
        {
            new HeroClassDef
            {
                Class = HeroClass.Fighter, Name = "Воин", Role = CombatRole.Tank, PowerStat = PowerStat.Str,
                Base = new Stats(8, 4, 2, 4, 10), Growth = new Stats(1, 1, 0, 1, 2), Armor = 33, Weapon = 6, AttackInterval = 2.0f,
                Abilities = new[] { Ability.ShieldBash, Ability.Taunt, Ability.DefensiveStance },
                Description = "Танк со щитом: держит врагов на себе и прерывает касты."
            },
            new HeroClassDef
            {
                Class = HeroClass.Cleric, Name = "Жрец", Role = CombatRole.Healer, PowerStat = PowerStat.Int, UsesMana = true,
                Base = new Stats(2, 3, 7, 10, 6), Growth = new Stats(0, 1, 1, 2, 1), Armor = 5, Weapon = 4, AttackInterval = 2.0f, MagicAttack = true,
                Abilities = new[] { Ability.Revive, Ability.Heal, Ability.ShieldOfFaith },
                Description = "Лекарь: сильное прямое лечение, щит на танка, поднимает упавших."
            },
            new HeroClassDef
            {
                Class = HeroClass.Barbarian, Name = "Варвар", Role = CombatRole.Melee, PowerStat = PowerStat.Str,
                Base = new Stats(10, 7, 2, 2, 7), Growth = new Stats(2, 1, 0, 0, 1), Armor = 15, Weapon = 13, AttackInterval = 2.4f,
                Abilities = new[] { Ability.Rage, Ability.RecklessStrike, Ability.Whirlwind },
                Description = "Ближний урон: ярость, тяжёлые удары и вихрь по толпе."
            },
            new HeroClassDef
            {
                Class = HeroClass.Paladin, Name = "Паладин", Role = CombatRole.Tank, PowerStat = PowerStat.Str, UsesMana = true,
                Base = new Stats(7, 3, 4, 7, 9), Growth = new Stats(1, 0, 1, 1, 2), Armor = 25, Weapon = 7, AttackInterval = 2.2f,
                Abilities = new[] { Ability.DivineSmite, Ability.LayOnHands, Ability.AuraOfProtection },
                Description = "Танк со светом: бьёт священным уроном, спасает возложением рук, прикрывает отряд аурой."
            },
            new HeroClassDef
            {
                Class = HeroClass.Druid, Name = "Друид", Role = CombatRole.Healer, PowerStat = PowerStat.Int, UsesMana = true,
                Base = new Stats(3, 4, 7, 9, 7), Growth = new Stats(0, 1, 1, 2, 1), Armor = 8, Weapon = 5, AttackInterval = 2.0f, MagicAttack = true,
                Abilities = new[] { Ability.Rejuvenation, Ability.HealingWave, Ability.EntanglingVines },
                Description = "Лекарь природы: лечение со временем, волна на весь отряд, лозы обездвиживают врагов."
            },
            new HeroClassDef
            {
                Class = HeroClass.Bard, Name = "Бард", Role = CombatRole.Support, PowerStat = PowerStat.Agi, UsesMana = true,
                Base = new Stats(3, 6, 5, 8, 7), Growth = new Stats(0, 1, 1, 1, 1), Armor = 10, Weapon = 8.4f, AttackInterval = 1.8f,
                Abilities = new[] { Ability.Inspiration, Ability.ViciousMockery, Ability.SongOfRest },
                Description = "Поддержка: вдохновляет отряд на урон, ослабляет врага насмешкой, подлечивает песней."
            },
            new HeroClassDef
            {
                Class = HeroClass.Rogue, Name = "Плут", Role = CombatRole.Melee, PowerStat = PowerStat.Agi,
                Base = new Stats(5, 11, 3, 2, 6), Growth = new Stats(1, 2, 0, 0, 1), Armor = 12, Weapon = 8.3f, AttackInterval = 1.8f,
                Abilities = new[] { Ability.SneakAttack, Ability.PoisonBlade, Ability.Evasion },
                Description = "Ближний урон из-за спины: удар исподтишка, яд, увёртливость."
            },
            new HeroClassDef
            {
                Class = HeroClass.Monk, Name = "Монах", Role = CombatRole.Melee, PowerStat = PowerStat.Agi,
                Base = new Stats(6, 10, 3, 5, 7), Growth = new Stats(1, 2, 0, 1, 1), Armor = 10, Weapon = 8, AttackInterval = 1.4f,
                Abilities = new[] { Ability.FlurryOfBlows, Ability.StunningStrike, Ability.PatientDefense },
                Description = "Быстрый боец без оружия: шквал ударов, оглушение, уход в оборону."
            },
            new HeroClassDef
            {
                Class = HeroClass.Ranger, Name = "Следопыт", Role = CombatRole.Ranged, PowerStat = PowerStat.Agi,
                Base = new Stats(5, 10, 3, 4, 7), Growth = new Stats(1, 2, 0, 0, 1), Armor = 12, Weapon = 12.7f, AttackInterval = 2.2f,
                Abilities = new[] { Ability.HuntersMark, Ability.AimedShot, Ability.Volley },
                Description = "Дальний урон луком: метит добычу для всего отряда, прицельный выстрел, залп."
            },
            new HeroClassDef
            {
                Class = HeroClass.Wizard, Name = "Волшебник", Role = CombatRole.Ranged, PowerStat = PowerStat.Int, UsesMana = true,
                Base = new Stats(2, 4, 11, 5, 5), Growth = new Stats(0, 1, 2, 1, 1), Armor = 3, Weapon = 9.4f, AttackInterval = 2.2f, MagicAttack = true,
                Abilities = new[] { Ability.MagicMissile, Ability.Fireball, Ability.Counterspell },
                Description = "Учёный маг: огненный шар по группе, волшебная стрела, контрзаклинание."
            },
            new HeroClassDef
            {
                Class = HeroClass.Sorcerer, Name = "Чародей", Role = CombatRole.Ranged, PowerStat = PowerStat.Int, UsesMana = true,
                Base = new Stats(2, 4, 10, 5, 6), Growth = new Stats(0, 1, 2, 1, 1), Armor = 3, Weapon = 8.2f, AttackInterval = 2.0f, MagicAttack = true,
                Abilities = new[] { Ability.ChaosBolt, Ability.ChainLightning, Ability.BloodMagic },
                Description = "Врождённая магия: непредсказуемый сгусток хаоса, цепная молния, магия крови."
            },
            new HeroClassDef
            {
                Class = HeroClass.Warlock, Name = "Колдун", Role = CombatRole.Ranged, PowerStat = PowerStat.Int, UsesMana = true,
                Base = new Stats(2, 3, 9, 7, 7), Growth = new Stats(0, 1, 2, 1, 1), Armor = 5, Weapon = 6.2f, AttackInterval = 2.0f, MagicAttack = true,
                Abilities = new[] { Ability.EldritchBlast, Ability.Hex, Ability.LifeDrain },
                Description = "Магия пакта: мистический заряд, сглаз, вытягивание жизни в пользу союзника."
            }
        };

        static readonly AbilityDef[] abilities =
        {
            new AbilityDef(),
            new AbilityDef
            {
                Id = Ability.ShieldBash, Name = "Удар щитом", Class = HeroClass.Fighter, Effect = AbilityEffect.Strike, Trigger = AbilityTrigger.Ready,
                Priority = 62, Cooldown = 6f, Power = 0.8f, Threat = 5f / 3f, Interrupts = true,
                Description = "×{power} урона, много угрозы, прерывает каст (кроме боссов)"
            },
            new AbilityDef
            {
                Id = Ability.Taunt, Name = "Провокация", Class = HeroClass.Fighter, Effect = AbilityEffect.Taunt, Trigger = AbilityTrigger.EnemyOnAlly,
                Priority = 20, Cooldown = 8f, Description = "Враг, который бьёт союзника, переключается на героя"
            },
            new AbilityDef
            {
                Id = Ability.DefensiveStance, Name = "Оборонительная стойка", Class = HeroClass.Fighter, Effect = AbilityEffect.SelfGuard,
                Trigger = AbilityTrigger.SelfHurt, Priority = 22, Cooldown = 20f, Duration = 6f, Amount = 0.4f,
                Description = "−{amount}% получаемого урона на {duration} с, когда здоровье ниже половины"
            },
            new AbilityDef
            {
                Id = Ability.Revive, Name = "Поднять", Class = HeroClass.Cleric, Effect = AbilityEffect.Revive, Trigger = AbilityTrigger.AllyDowned,
                Priority = 10, Cooldown = 30f, Cast = 3f, Mana = 40f, Description = "Упавший союзник встаёт с 30% здоровья"
            },
            new AbilityDef
            {
                Id = Ability.Heal, Name = "Лечение", Class = HeroClass.Cleric, Effect = AbilityEffect.Heal, Trigger = AbilityTrigger.AllyHurt,
                Priority = 30, Cast = 2f, Mana = 15f, Power = 19.9f, Scaling = 1.01f, Description = "Лечит самого раненого союзника"
            },
            new AbilityDef
            {
                Id = Ability.ShieldOfFaith, Name = "Щит веры", Class = HeroClass.Cleric, Effect = AbilityEffect.Shield, Trigger = AbilityTrigger.TankUnshielded,
                Priority = 45, Cooldown = 10f, Mana = 10f, Power = 15f, Scaling = 0.66f, Description = "Поглощающий щит на танка"
            },
            new AbilityDef
            {
                Id = Ability.Rage, Name = "Ярость", Class = HeroClass.Barbarian, Effect = AbilityEffect.SelfRage, Trigger = AbilityTrigger.Ready,
                Priority = 28, Cooldown = 25f, Duration = 10f, Amount = 0.25f, Description = "+{amount}% урона и −20% получаемого урона на {duration} с"
            },
            new AbilityDef
            {
                Id = Ability.RecklessStrike, Name = "Безрассудный удар", Class = HeroClass.Barbarian, Effect = AbilityEffect.Strike, Trigger = AbilityTrigger.Ready,
                Priority = 60, Cooldown = 6f, Power = 1.63f, Description = "×{power} урона"
            },
            new AbilityDef
            {
                Id = Ability.Whirlwind, Name = "Вихрь", Class = HeroClass.Barbarian, Effect = AbilityEffect.AreaStrike, Trigger = AbilityTrigger.ThreeEnemies,
                Priority = 55, Cooldown = 12f, Power = 0.76f, Description = "×{power} урона по всем врагам, если их 3 и больше"
            },
            new AbilityDef
            {
                Id = Ability.DivineSmite, Name = "Божественная кара", Class = HeroClass.Paladin, Effect = AbilityEffect.Strike, Trigger = AbilityTrigger.Ready,
                Priority = 60, Cooldown = 8f, Mana = 10f, Power = 1.4f, Magic = true, Description = "×{power} священного урона сквозь броню"
            },
            new AbilityDef
            {
                Id = Ability.LayOnHands, Name = "Возложение рук", Class = HeroClass.Paladin, Effect = AbilityEffect.Heal, Trigger = AbilityTrigger.AllyCritical,
                Priority = 15, Cooldown = 30f, Power = 30f, Scaling = 2f, Description = "Мгновенно и сильно лечит союзника при смерти"
            },
            new AbilityDef
            {
                Id = Ability.AuraOfProtection, Name = "Аура защиты", Class = HeroClass.Paladin, Effect = AbilityEffect.PartyGuard, Trigger = AbilityTrigger.TwoEnemies,
                Priority = 26, Cooldown = 20f, Mana = 15f, Duration = 8f, Amount = 0.15f, Description = "−{amount}% получаемого урона всему отряду на {duration} с"
            },
            new AbilityDef
            {
                Id = Ability.Rejuvenation, Name = "Омоложение", Class = HeroClass.Druid, Effect = AbilityEffect.HealOverTime, Trigger = AbilityTrigger.AllyNeedsHot,
                Priority = 32, Mana = 12f, Power = 19.6f, Scaling = 1.18f, Duration = 10f, Description = "Лечит союзника постепенно за {duration} с"
            },
            new AbilityDef
            {
                Id = Ability.HealingWave, Name = "Волна исцеления", Class = HeroClass.Druid, Effect = AbilityEffect.GroupHeal, Trigger = AbilityTrigger.AllyHurt,
                Priority = 31, Cooldown = 3f, Cast = 2.5f, Mana = 18f, Power = 11.8f, Scaling = 0.7f, Description = "Лечит весь отряд, когда кто-то ранен"
            },
            new AbilityDef
            {
                Id = Ability.EntanglingVines, Name = "Опутывающие лозы", Class = HeroClass.Druid, Effect = AbilityEffect.AreaStun, Trigger = AbilityTrigger.TwoEnemies,
                Priority = 29, Cooldown = 25f, Mana = 15f, Duration = 2f, Description = "Все враги, кроме боссов, обездвижены на {duration} с, касты сбиваются"
            },
            new AbilityDef
            {
                Id = Ability.Inspiration, Name = "Вдохновение", Class = HeroClass.Bard, Effect = AbilityEffect.PartyEmpower, Trigger = AbilityTrigger.Ready,
                Priority = 27, Cooldown = 18f, Mana = 15f, Duration = 10f, Amount = 0.241f, Description = "+{amount}% урона всему отряду на {duration} с"
            },
            new AbilityDef
            {
                Id = Ability.ViciousMockery, Name = "Колкая насмешка", Class = HeroClass.Bard, Effect = AbilityEffect.WeakenStrike, Trigger = AbilityTrigger.Ready,
                Priority = 58, Cooldown = 8f, Mana = 8f, Power = 0.96f, Magic = true, Duration = 6f,
                Description = "×{power} урона, враг {duration} с бьёт на 25% слабее"
            },
            new AbilityDef
            {
                Id = Ability.SongOfRest, Name = "Песнь отдыха", Class = HeroClass.Bard, Effect = AbilityEffect.GroupHeal, Trigger = AbilityTrigger.GroupHurt,
                Priority = 35, Cooldown = 12f, Mana = 15f, Power = 6f, Scaling = 0.43f, Description = "Слегка лечит весь отряд"
            },
            new AbilityDef
            {
                Id = Ability.SneakAttack, Name = "Удар исподтишка", Class = HeroClass.Rogue, Effect = AbilityEffect.Strike, Trigger = AbilityTrigger.NotTargeted,
                Priority = 58, Cooldown = 6f, Power = 1.49f, Description = "×{power} урона по врагу, который бьёт не плута"
            },
            new AbilityDef
            {
                Id = Ability.PoisonBlade, Name = "Отравленный клинок", Class = HeroClass.Rogue, Effect = AbilityEffect.DamageOverTime, Trigger = AbilityTrigger.TargetClean,
                Priority = 52, Cooldown = 10f, Power = 0.23f, Duration = 8f, Description = "Яд: ×{power} удара в секунду {duration} с"
            },
            new AbilityDef
            {
                Id = Ability.Evasion, Name = "Увёртливость", Class = HeroClass.Rogue, Effect = AbilityEffect.SelfEvade, Trigger = AbilityTrigger.SelfTargeted,
                Priority = 21, Cooldown = 20f, Duration = 5f, Description = "+40% уклонения на {duration} с, когда враг бьёт плута"
            },
            new AbilityDef
            {
                Id = Ability.FlurryOfBlows, Name = "Шквал ударов", Class = HeroClass.Monk, Effect = AbilityEffect.MultiStrike, Trigger = AbilityTrigger.Ready,
                Priority = 60, Cooldown = 6f, Power = 0.5f, Hits = 3, Description = "Три удара по ×{power}"
            },
            new AbilityDef
            {
                Id = Ability.StunningStrike, Name = "Оглушающий удар", Class = HeroClass.Monk, Effect = AbilityEffect.Strike, Trigger = AbilityTrigger.Ready,
                Priority = 61, Cooldown = 10f, Power = 0.8f, Stun = 2f, Interrupts = true, Description = "×{power} урона, оглушение на 2 с, прерывает каст (кроме боссов)"
            },
            new AbilityDef
            {
                Id = Ability.PatientDefense, Name = "Терпеливая оборона", Class = HeroClass.Monk, Effect = AbilityEffect.SelfEvade, Trigger = AbilityTrigger.SelfTargeted,
                Priority = 21, Cooldown = 20f, Duration = 5f, Description = "+40% уклонения на {duration} с, когда враг бьёт монаха"
            },
            new AbilityDef
            {
                Id = Ability.HuntersMark, Name = "Метка охотника", Class = HeroClass.Ranger, Effect = AbilityEffect.Mark, Trigger = AbilityTrigger.TargetUnmarked,
                Priority = 50, Cooldown = 12f, Duration = 10f, Description = "Помеченный враг получает +20% урона от всех {duration} с"
            },
            new AbilityDef
            {
                Id = Ability.AimedShot, Name = "Прицельный выстрел", Class = HeroClass.Ranger, Effect = AbilityEffect.Strike, Trigger = AbilityTrigger.Ready,
                Priority = 60, Cooldown = 8f, Cast = 1.5f, Power = 2.08f, Description = "×{power} урона после прицеливания"
            },
            new AbilityDef
            {
                Id = Ability.Volley, Name = "Залп", Class = HeroClass.Ranger, Effect = AbilityEffect.AreaStrike, Trigger = AbilityTrigger.ThreeEnemies,
                Priority = 55, Cooldown = 12f, Power = 0.7f, Description = "×{power} урона по всем врагам, если их 3 и больше"
            },
            new AbilityDef
            {
                Id = Ability.MagicMissile, Name = "Волшебная стрела", Class = HeroClass.Wizard, Effect = AbilityEffect.Strike, Trigger = AbilityTrigger.Ready,
                Priority = 60, Cooldown = 4f, Mana = 8f, Power = 1.22f, Magic = true, Description = "×{power} магического урона"
            },
            new AbilityDef
            {
                Id = Ability.Fireball, Name = "Огненный шар", Class = HeroClass.Wizard, Effect = AbilityEffect.AreaStrike, Trigger = AbilityTrigger.TwoEnemies,
                Priority = 55, Cooldown = 14f, Cast = 2f, Mana = 25f, Power = 0.94f, Magic = true, Description = "×{power} магического урона по всем врагам"
            },
            new AbilityDef
            {
                Id = Ability.Counterspell, Name = "Контрзаклинание", Class = HeroClass.Wizard, Effect = AbilityEffect.Interrupt, Trigger = AbilityTrigger.EnemyCasting,
                Priority = 5, Cooldown = 12f, Mana = 15f, Description = "Сбивает каст врага издалека (кроме боссов)"
            },
            new AbilityDef
            {
                Id = Ability.ChaosBolt, Name = "Сгусток хаоса", Class = HeroClass.Sorcerer, Effect = AbilityEffect.RandomStrike, Trigger = AbilityTrigger.Ready,
                Priority = 60, Cooldown = 5f, Mana = 10f, Power = 0.48f, Amount = 1.31f, Magic = true, Description = "От ×{power} до ×{max} магического урона"
            },
            new AbilityDef
            {
                Id = Ability.ChainLightning, Name = "Цепная молния", Class = HeroClass.Sorcerer, Effect = AbilityEffect.ChainStrike, Trigger = AbilityTrigger.TwoEnemies,
                Priority = 55, Cooldown = 10f, Mana = 20f, Power = 0.73f, Hits = 3, Magic = true, Description = "×{power} урона по трём врагам"
            },
            new AbilityDef
            {
                Id = Ability.BloodMagic, Name = "Магия крови", Class = HeroClass.Sorcerer, Effect = AbilityEffect.SelfEmpower, Trigger = AbilityTrigger.Ready,
                Priority = 28, Cooldown = 25f, Duration = 8f, Amount = 0.4f, Description = "Тратит 10% здоровья: +{amount}% урона на {duration} с"
            },
            new AbilityDef
            {
                Id = Ability.EldritchBlast, Name = "Мистический заряд", Class = HeroClass.Warlock, Effect = AbilityEffect.Strike, Trigger = AbilityTrigger.Ready,
                Priority = 60, Cooldown = 3f, Power = 0.83f, Magic = true, Description = "×{power} магического урона, без маны"
            },
            new AbilityDef
            {
                Id = Ability.Hex, Name = "Сглаз", Class = HeroClass.Warlock, Effect = AbilityEffect.DamageOverTime, Trigger = AbilityTrigger.TargetClean,
                Priority = 52, Cooldown = 10f, Mana = 10f, Power = 0.24f, Duration = 10f, Magic = true, Description = "Проклятие: ×{power} удара в секунду {duration} с"
            },
            new AbilityDef
            {
                Id = Ability.LifeDrain, Name = "Вытягивание жизни", Class = HeroClass.Warlock, Effect = AbilityEffect.Drain, Trigger = AbilityTrigger.AllyHurt,
                Priority = 40, Cooldown = 8f, Mana = 12f, Power = 0.68f, Amount = 0.5f, Magic = true,
                Description = "×{power} урона, {amount}% уходит лечением самому раненому союзнику"
            }
        };

        static readonly TitleDef[] titles =
        {
            new TitleDef { Id = 1, Name = "Храмовник", A = HeroClass.Fighter, B = HeroClass.Cleric, Bonus = TitleBonus.Health, Amount = 0.1f },
            new TitleDef { Id = 2, Name = "Ветеран", A = HeroClass.Fighter, B = HeroClass.Barbarian, Bonus = TitleBonus.Armor, Amount = 10f },
            new TitleDef { Id = 3, Name = "Целитель", A = HeroClass.Cleric, B = HeroClass.Druid, Bonus = TitleBonus.Healing, Amount = 0.15f },
            new TitleDef { Id = 4, Name = "Дикий зверь", A = HeroClass.Barbarian, B = HeroClass.Druid, Bonus = TitleBonus.Health, Amount = 0.1f },
            new TitleDef { Id = 5, Name = "Клинок клятвы", A = HeroClass.Paladin, B = HeroClass.Warlock, Bonus = TitleBonus.Damage, Amount = 0.1f },
            new TitleDef { Id = 6, Name = "Глашатай", A = HeroClass.Paladin, B = HeroClass.Bard, Bonus = TitleBonus.Healing, Amount = 0.15f },
            new TitleDef { Id = 7, Name = "Танцор клинков", A = HeroClass.Bard, B = HeroClass.Rogue, Bonus = TitleBonus.Dodge, Amount = 0.05f },
            new TitleDef { Id = 8, Name = "Мистический ловкач", A = HeroClass.Rogue, B = HeroClass.Wizard, Bonus = TitleBonus.Damage, Amount = 0.1f },
            new TitleDef { Id = 9, Name = "Охотник за головами", A = HeroClass.Rogue, B = HeroClass.Ranger, Bonus = TitleBonus.Damage, Amount = 0.1f },
            new TitleDef { Id = 10, Name = "Страж леса", A = HeroClass.Ranger, B = HeroClass.Druid, Bonus = TitleBonus.Dodge, Amount = 0.05f },
            new TitleDef { Id = 11, Name = "Святой кулак", A = HeroClass.Monk, B = HeroClass.Cleric, Bonus = TitleBonus.Armor, Amount = 10f },
            new TitleDef { Id = 12, Name = "Архимаг", A = HeroClass.Wizard, B = HeroClass.Sorcerer, Bonus = TitleBonus.Damage, Amount = 0.1f }
        };

        public static IReadOnlyList<HeroClassDef> All => classes;
        public static IReadOnlyList<TitleDef> Titles => titles;

        public static HeroClassDef Get(HeroClass heroClass) => classes[(int)heroClass];

        public static AbilityDef Get(Ability ability) => abilities[(int)ability];

        public static string RoleName(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Tank: return "танк";
                case CombatRole.Healer: return "лекарь";
                case CombatRole.Support: return "поддержка";
                case CombatRole.Melee: return "ближний урон";
                default: return "дальний урон";
            }
        }

        public static bool HasClass(int classMask, HeroClass heroClass) => (classMask & (1 << (int)heroClass)) != 0;

        public static TitleDef TitleFor(HeroClass primary, int classMask)
        {
            var mask = classMask | 1 << (int)primary;
            foreach (var title in titles)
                if (HasClass(mask, title.A) && HasClass(mask, title.B))
                    return title;
            return null;
        }

        public static int ExtraClassLimit(int level) => level >= ThirdClassLevel ? 2 : level >= SecondClassLevel ? 1 : 0;

        public static int CountClasses(int classMask)
        {
            var count = 0;
            for (var mask = classMask; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        public static List<Ability> Loadout(HeroClass primary, Ability slotA, Ability slotB)
        {
            var list = new List<Ability>(Get(primary).Abilities);
            if (slotA != Ability.None && !list.Contains(slotA)) list.Add(slotA);
            if (slotB != Ability.None && !list.Contains(slotB)) list.Add(slotB);
            list.Sort((a, b) => Get(a).Priority.CompareTo(Get(b).Priority));
            return list;
        }
    }
}
