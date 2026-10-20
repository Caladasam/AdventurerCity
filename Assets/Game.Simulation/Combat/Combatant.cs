using System.Collections.Generic;

namespace AdventurerCity.Simulation.Combat
{
    public enum CastKind
    {
        None,
        Ability,
        EnemyHeal,
        EnemyAoe,
        EnemyShield
    }

    public sealed class Combatant
    {
        public const int CooldownSlots = 6;

        public string Name;
        public bool IsHero;
        public HeroClass Class;
        public CombatRole Role;
        public EnemyKind Enemy;
        public int Level;
        public List<Ability> Abilities = new List<Ability>();
        public TitleDef Title;

        public float MaxHp;
        public float Hp;
        public int Armor;
        public float Weapon;
        public float Power;
        public float CritChance;
        public float DodgeChance;
        public float AttackInterval;
        public bool MagicAttack;
        public float MaxMana;
        public float Mana;
        public float ManaRegen;
        public float HealPower;
        public float DamageBonus;

        public float SwingTimer;
        public float Gcd;
        public float StunTimer;
        public float DownedTimer;
        public float Absorb;
        public CastKind Cast;
        public Ability CastAbility;
        public float CastTimer;
        public Combatant CastTarget;
        public readonly float[] Cooldowns = new float[CooldownSlots];
        public bool SummonUsed;

        public float GuardTimer;
        public float GuardAmount;
        public float EmpowerTimer;
        public float EmpowerAmount;
        public float EvadeTimer;
        public float HotTimer;
        public float HotPerSecond;
        public float DotTimer;
        public float DotPerSecond;
        public Combatant DotSource;
        public float MarkTimer;
        public float WeakenTimer;

        public bool Downed;
        public bool Dead;
        public bool Injured;
        public int XpGained;
        public float DamageTaken;
        public float Scale = 1f;
        public float DownedScale = 1f;
        public float CooldownReduction;
        public float Lifesteal;
        public float Thorns;
        public float Execute;
        public float HpScale = 1f;
        public int Phase;
        public float AoeSpeed = 1f;

        public readonly Dictionary<Combatant, float> Threat = new Dictionary<Combatant, float>();

        public bool Active => !Downed && !Dead;
        public bool IsCasting => Cast != CastKind.None;
        public float HpRatio => MaxHp <= 0f ? 0f : Hp / MaxHp;
        public float EffectiveDodge => DodgeChance + (EvadeTimer > 0f ? Classes.EvadeBonus : 0f);

        public static Combatant FromHero(string name, HeroClass heroClass, int level, bool injured = false, int weaponTier = 0, int armorTier = 0,
            List<Ability> abilities = null, TitleDef title = null)
        {
            var def = Classes.Get(heroClass);
            abilities = abilities ?? Classes.Loadout(heroClass, Ability.None, Ability.None);
            var stats = def.Base.Grow(def.Growth, level - 1);
            var maxHp = 40f + 8f * stats.Sta;
            if (injured) maxHp *= 1f - CombatData.InjuryHpPenalty;

            var usesMana = def.UsesMana;
            foreach (var ability in abilities)
                if (Classes.Get(ability).Mana > 0f)
                    usesMana = true;
            var maxMana = usesMana ? 50f + 10f * stats.Int : 0f;

            var weapon = GearData.Tier(weaponTier);
            var armor = GearData.Tier(armorTier);
            var healer = def.Role == CombatRole.Healer;
            var power = def.PowerStat == PowerStat.Str ? stats.Str : def.PowerStat == PowerStat.Agi ? stats.Agi : stats.Int;
            var hero = new Combatant
            {
                Name = name,
                IsHero = true,
                Class = heroClass,
                Role = def.Role,
                Level = level,
                Abilities = abilities,
                Title = title,
                MaxHp = maxHp,
                Armor = def.Armor + armor.ArmorBonus,
                Weapon = def.Weapon + (healer ? 0 : weapon.WeaponBonus),
                Power = 0.5f * power,
                CritChance = 0.005f * stats.Agi,
                DodgeChance = 0.004f * stats.Agi,
                AttackInterval = def.AttackInterval,
                MagicAttack = def.MagicAttack,
                MaxMana = maxMana,
                ManaRegen = CombatData.ManaRegenBase + CombatData.ManaRegenPerSpirit * stats.Spi,
                HealPower = 1.5f * stats.Spi + (healer ? weapon.HealBonus : 0),
                Injured = injured
            };

            if (level >= CombatData.VeteranLevel)
            {
                switch (def.Role)
                {
                    case CombatRole.Tank:
                        hero.MaxHp *= 1.1f;
                        hero.Armor += 2;
                        break;
                    case CombatRole.Healer:
                        hero.HealPower *= 1.15f;
                        break;
                    case CombatRole.Support:
                        hero.DamageBonus += 0.05f;
                        hero.HealPower *= 1.1f;
                        break;
                    default:
                        hero.DamageBonus += 0.1f;
                        break;
                }
            }
            if (level >= CombatData.LegendLevel)
            {
                hero.MaxHp *= 1.1f;
                hero.DamageBonus += 0.1f;
                hero.HealPower *= 1.1f;
                hero.DownedScale = 1.5f;
            }

            if (title != null)
            {
                switch (title.Bonus)
                {
                    case TitleBonus.Health: hero.MaxHp *= 1f + title.Amount; break;
                    case TitleBonus.Damage: hero.DamageBonus += title.Amount; break;
                    case TitleBonus.Healing: hero.HealPower *= 1f + title.Amount; break;
                    case TitleBonus.Dodge: hero.DodgeChance += title.Amount; break;
                    case TitleBonus.Armor: hero.Armor += (int)title.Amount; break;
                }
            }
            hero.Hp = hero.MaxHp;
            hero.Mana = hero.MaxMana;
            return hero;
        }

        public static Combatant FromEnemy(EnemyKind kind, float scale = 1f, float hpScale = 0f)
        {
            var def = CombatData.Get(kind);
            if (hpScale <= 0f) hpScale = scale;
            return new Combatant
            {
                Name = def.Name,
                Enemy = kind,
                Level = def.Level,
                Scale = scale,
                HpScale = hpScale,
                MaxHp = def.Hp * hpScale,
                Hp = def.Hp * hpScale,
                Armor = def.Armor,
                Weapon = def.Damage * scale,
                CritChance = CombatData.EnemyCritChance,
                AttackInterval = def.AttackInterval,
                MagicAttack = def.MagicAttack
            };
        }

        public void RestoreFraction(float fraction)
        {
            if (!Active) return;
            Hp = System.Math.Min(MaxHp, Hp + MaxHp * fraction);
            Mana = System.Math.Min(MaxMana, Mana + MaxMana * fraction);
        }

        public void ResetForBattle()
        {
            Gcd = 0f;
            StunTimer = 0f;
            Absorb = 0f;
            Cast = CastKind.None;
            CastAbility = Ability.None;
            CastTarget = null;
            GuardTimer = 0f;
            EmpowerTimer = 0f;
            EvadeTimer = 0f;
            HotTimer = 0f;
            DotTimer = 0f;
            DotSource = null;
            MarkTimer = 0f;
            WeakenTimer = 0f;
            for (var i = 0; i < CooldownSlots; i++) Cooldowns[i] = 0f;
        }
    }
}
