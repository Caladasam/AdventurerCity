using System.Collections.Generic;

namespace AdventurerCity.Simulation.Combat
{
    public enum CastKind
    {
        None,
        Heal,
        Revive,
        ShamanHeal,
        Collapse
    }

    public sealed class Combatant
    {
        public const int CooldownSlots = 4;

        public string Name;
        public bool IsHero;
        public HeroClass Class;
        public EnemyKind Enemy;
        public int Level;

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

        public float SwingTimer;
        public float Gcd;
        public float StunTimer;
        public float StanceTimer;
        public float DownedTimer;
        public float Absorb;
        public CastKind Cast;
        public float CastTimer;
        public Combatant CastTarget;
        public readonly float[] Cooldowns = new float[CooldownSlots];
        public bool ChargeUsed;
        public bool SummonUsed;

        public bool Downed;
        public bool Dead;
        public bool Injured;
        public int XpGained;
        public float DamageTaken;

        public readonly Dictionary<Combatant, float> Threat = new Dictionary<Combatant, float>();

        public bool Active => !Downed && !Dead;
        public bool IsCasting => Cast != CastKind.None;
        public float HpRatio => MaxHp <= 0f ? 0f : Hp / MaxHp;

        public static Combatant FromHero(string name, HeroClass heroClass, int level, bool injured = false)
        {
            var def = CombatData.Get(heroClass);
            var stats = def.Base.Grow(def.Growth, level - 1);
            var maxHp = 40f + 8f * stats.Sta;
            if (injured) maxHp *= 1f - CombatData.InjuryHpPenalty;
            var maxMana = def.Role == CombatRole.Healer ? 50f + 10f * stats.Int : 0f;
            return new Combatant
            {
                Name = name,
                IsHero = true,
                Class = heroClass,
                Level = level,
                MaxHp = maxHp,
                Hp = maxHp,
                Armor = def.Armor,
                Weapon = def.Weapon,
                Power = 0.5f * stats.Str,
                CritChance = 0.005f * stats.Agi,
                DodgeChance = 0.004f * stats.Agi,
                AttackInterval = def.AttackInterval,
                MagicAttack = def.MagicAttack,
                MaxMana = maxMana,
                Mana = maxMana,
                ManaRegen = 2f + 0.2f * stats.Spi,
                HealPower = 1.5f * stats.Spi,
                Injured = injured
            };
        }

        public static Combatant FromEnemy(EnemyKind kind)
        {
            var def = CombatData.Get(kind);
            return new Combatant
            {
                Name = def.Name,
                Enemy = kind,
                Level = def.Level,
                MaxHp = def.Hp,
                Hp = def.Hp,
                Armor = def.Armor,
                Weapon = def.Damage,
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
            StanceTimer = 0f;
            Absorb = 0f;
            Cast = CastKind.None;
            CastTarget = null;
            ChargeUsed = false;
            for (var i = 0; i < CooldownSlots; i++) Cooldowns[i] = 0f;
        }
    }
}
