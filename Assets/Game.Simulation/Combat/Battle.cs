using System;
using System.Collections.Generic;

namespace AdventurerCity.Simulation.Combat
{
    public enum BattleEventKind
    {
        Hit,
        Dodge,
        Heal,
        CastStart,
        Interrupt,
        Taunt,
        Downed,
        Killed,
        Died,
        Revived,
        Summon,
        Collapse
    }

    public readonly struct BattleEvent
    {
        public readonly BattleEventKind Kind;
        public readonly Combatant Source;
        public readonly Combatant Target;
        public readonly float Amount;
        public readonly bool Crit;

        public BattleEvent(BattleEventKind kind, Combatant source, Combatant target, float amount = 0f, bool crit = false)
        {
            Kind = kind;
            Source = source;
            Target = target;
            Amount = amount;
            Crit = crit;
        }
    }

    public enum BattleOutcome
    {
        Ongoing,
        Won,
        Lost,
        Retreated
    }

    public sealed class Battle
    {
        const int GuardBash = 0, GuardTaunt = 1, GuardStance = 2;
        const int PriestRevive = 0, PriestShield = 1;
        const int WarriorCleave = 0, WarriorWhirl = 1;
        const int EnemySpecial = 0;

        readonly List<Combatant> heroes;
        readonly List<Combatant> enemies = new List<Combatant>();
        readonly List<Combatant> reinforcements = new List<Combatant>();
        readonly Rng rng;
        readonly List<string> log;

        public Battle(List<Combatant> heroes, IEnumerable<Combatant> foes, Rng rng, List<string> log)
        {
            this.heroes = heroes;
            this.rng = rng;
            this.log = log;
            enemies.AddRange(foes);

            foreach (var hero in heroes)
            {
                hero.ResetForBattle();
                hero.SwingTimer = rng.Value * 0.5f;
            }
            foreach (var enemy in enemies) PrepareEnemy(enemy);
        }

        public List<BattleEvent> Events { get; } = new List<BattleEvent>();
        public float Time { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public int XpPool { get; private set; }
        public IReadOnlyList<Combatant> Heroes => heroes;
        public IReadOnlyList<Combatant> Enemies => enemies;

        public BattleOutcome Step()
        {
            if (Outcome != BattleOutcome.Ongoing) return Outcome;

            const float dt = CombatData.Tick;
            Time += dt;
            foreach (var unit in heroes) Advance(unit, dt);
            foreach (var unit in enemies) Advance(unit, dt);

            foreach (var hero in heroes)
                if (hero.Active && hero.StunTimer <= 0f)
                    ActHero(hero);
            for (var i = 0; i < enemies.Count; i++)
                if (enemies[i].Active && enemies[i].StunTimer <= 0f)
                    ActEnemy(enemies[i]);

            if (reinforcements.Count > 0)
            {
                foreach (var enemy in reinforcements) PrepareEnemy(enemy);
                enemies.AddRange(reinforcements);
                reinforcements.Clear();
            }

            Outcome = Evaluate();
            return Outcome;
        }

        void PrepareEnemy(Combatant enemy)
        {
            enemy.ResetForBattle();
            enemy.SwingTimer = rng.Value * enemy.AttackInterval * 0.5f;
            if (enemy.Enemy == EnemyKind.Grok) enemy.Cooldowns[EnemySpecial] = 8f;
            var guard = FindHero(HeroClass.Guard);
            if (guard != null) enemy.Threat[guard] = CombatData.GuardOpeningThreat;
            else if (FirstActive(heroes) is Combatant opener) enemy.Threat[opener] = 1f;
        }

        void Advance(Combatant unit, float dt)
        {
            if (unit.Dead) return;
            if (unit.Downed)
            {
                unit.DownedTimer -= dt;
                if (unit.DownedTimer <= 0f)
                {
                    unit.Downed = false;
                    unit.Dead = true;
                    Emit(BattleEventKind.Died, unit, null);
                    Write($"{unit.Name} погибает");
                }
                return;
            }

            unit.SwingTimer -= dt;
            unit.Gcd -= dt;
            unit.StunTimer -= dt;
            unit.StanceTimer -= dt;
            for (var i = 0; i < Combatant.CooldownSlots; i++) unit.Cooldowns[i] -= dt;
            if (unit.MaxMana > 0f) unit.Mana = Math.Min(unit.MaxMana, unit.Mana + unit.ManaRegen * dt);

            if (unit.IsCasting)
            {
                unit.CastTimer -= dt;
                if (unit.CastTimer <= 0f) ResolveCast(unit);
            }
        }

        void ActHero(Combatant hero)
        {
            if (hero.IsCasting) return;
            if (hero.Class == HeroClass.Warrior && Time < CombatData.DamageDealerDelay && FindHero(HeroClass.Guard) != null) return;
            if (hero.Gcd <= 0f && UseAbility(hero)) hero.Gcd = CombatData.GlobalCooldown;
            if (hero.IsCasting || hero.SwingTimer > 0f) return;

            var target = Focus();
            if (target == null) return;
            Hit(hero, target, 1f, hero.MagicAttack, hero.Class == HeroClass.Guard ? CombatData.GuardThreatMultiplier : 1f);
            hero.SwingTimer = hero.AttackInterval;
        }

        bool UseAbility(Combatant hero)
        {
            switch (hero.Class)
            {
                case HeroClass.Guard: return GuardAbility(hero);
                case HeroClass.Priest: return PriestAbility(hero);
                default: return WarriorAbility(hero);
            }
        }

        bool GuardAbility(Combatant guard)
        {
            var cd = guard.Cooldowns;
            if (cd[GuardBash] <= 0f)
            {
                foreach (var enemy in enemies)
                {
                    if (!enemy.Active || !enemy.IsCasting) continue;
                    ShieldBash(guard, enemy);
                    return true;
                }
            }

            if (cd[GuardTaunt] <= 0f)
            {
                foreach (var enemy in enemies)
                {
                    if (!enemy.Active) continue;
                    var target = TopThreat(enemy);
                    if (target == null || target == guard) continue;
                    var max = 0f;
                    foreach (var threat in enemy.Threat.Values) max = Math.Max(max, threat);
                    enemy.Threat[guard] = max * 1.1f + 1f;
                    cd[GuardTaunt] = 8f;
                    Emit(BattleEventKind.Taunt, guard, enemy);
                    Write($"{guard.Name} провоцирует {enemy.Name}");
                    return true;
                }
            }

            if (guard.HpRatio < 0.5f && cd[GuardStance] <= 0f)
            {
                guard.StanceTimer = 6f;
                cd[GuardStance] = 20f;
                Write($"{guard.Name} встаёт в оборонительную стойку");
                return true;
            }

            if (cd[GuardBash] <= 0f)
            {
                var focus = Focus();
                if (focus == null) return false;
                ShieldBash(guard, focus);
                return true;
            }
            return false;
        }

        void ShieldBash(Combatant guard, Combatant target)
        {
            guard.Cooldowns[GuardBash] = 6f;
            if (target.IsCasting)
            {
                target.Cast = CastKind.None;
                Emit(BattleEventKind.Interrupt, guard, target);
                Write($"{guard.Name} прерывает {target.Name}");
            }
            Hit(guard, target, 0.8f, false, CombatData.ShieldBashThreatMultiplier);
        }

        bool PriestAbility(Combatant priest)
        {
            var cd = priest.Cooldowns;
            if (cd[PriestRevive] <= 0f && priest.Mana >= 40f)
            {
                foreach (var hero in heroes)
                {
                    if (!hero.Downed) continue;
                    priest.Mana -= 40f;
                    cd[PriestRevive] = 30f;
                    StartCast(priest, CastKind.Revive, 3f, hero);
                    return true;
                }
            }

            if (priest.Mana >= 15f)
            {
                Combatant wounded = null;
                foreach (var hero in heroes)
                    if (hero.Active && (wounded == null || hero.HpRatio < wounded.HpRatio))
                        wounded = hero;
                if (wounded != null && wounded.HpRatio < 0.7f)
                {
                    priest.Mana -= 15f;
                    StartCast(priest, CastKind.Heal, 2f, wounded);
                    return true;
                }
            }

            var guard = FindHero(HeroClass.Guard);
            if (guard != null && guard.Absorb <= 0f && cd[PriestShield] <= 0f && priest.Mana >= 10f)
            {
                priest.Mana -= 10f;
                cd[PriestShield] = 10f;
                guard.Absorb = 15f + priest.HealPower / 1.5f;
                return true;
            }
            return false;
        }

        bool WarriorAbility(Combatant warrior)
        {
            var focus = Focus();
            if (focus == null) return false;
            var cd = warrior.Cooldowns;

            if (!warrior.ChargeUsed)
            {
                warrior.ChargeUsed = true;
                Hit(warrior, focus, 1.2f, false, 1f);
                if (focus.Active)
                {
                    focus.StunTimer = 1f;
                    focus.Cast = CastKind.None;
                }
                return true;
            }

            if (cd[WarriorWhirl] <= 0f && CountActive(enemies) >= 3)
            {
                cd[WarriorWhirl] = 12f;
                foreach (var enemy in enemies)
                    if (enemy.Active)
                        Hit(warrior, enemy, 0.7f, false, 1f);
                return true;
            }

            if (cd[WarriorCleave] <= 0f)
            {
                cd[WarriorCleave] = 6f;
                Hit(warrior, focus, 1.5f, false, 1f);
                return true;
            }
            return false;
        }

        void ActEnemy(Combatant enemy)
        {
            if (enemy.IsCasting) return;
            var cd = enemy.Cooldowns;

            if (enemy.Enemy == EnemyKind.Shaman && cd[EnemySpecial] <= 0f)
            {
                Combatant wounded = null;
                foreach (var ally in enemies)
                    if (ally.Active && ally.HpRatio < 1f && (wounded == null || ally.HpRatio < wounded.HpRatio))
                        wounded = ally;
                if (wounded != null)
                {
                    cd[EnemySpecial] = 6f;
                    StartCast(enemy, CastKind.ShamanHeal, 1.5f, wounded);
                    return;
                }
            }

            if (enemy.Enemy == EnemyKind.Grok)
            {
                if (!enemy.SummonUsed && enemy.HpRatio <= 0.5f)
                {
                    enemy.SummonUsed = true;
                    reinforcements.Add(Combatant.FromEnemy(EnemyKind.Ratman));
                    reinforcements.Add(Combatant.FromEnemy(EnemyKind.Ratman));
                    Emit(BattleEventKind.Summon, enemy, null);
                    Write($"{enemy.Name} зовёт подмогу");
                }
                if (cd[EnemySpecial] <= 0f)
                {
                    cd[EnemySpecial] = 15f;
                    StartCast(enemy, CastKind.Collapse, 2.5f, null);
                    Write($"{enemy.Name} готовит обвал");
                    return;
                }
            }

            if (enemy.SwingTimer > 0f) return;
            var target = TopThreat(enemy);
            if (target == null) return;
            Hit(enemy, target, 1f, enemy.MagicAttack, 1f);
            enemy.SwingTimer = enemy.AttackInterval;
        }

        void StartCast(Combatant caster, CastKind kind, float seconds, Combatant target)
        {
            caster.Cast = kind;
            caster.CastTimer = seconds;
            caster.CastTarget = target;
            Emit(BattleEventKind.CastStart, caster, target, seconds);
        }

        void ResolveCast(Combatant caster)
        {
            var kind = caster.Cast;
            var target = caster.CastTarget;
            caster.Cast = CastKind.None;
            caster.CastTarget = null;

            switch (kind)
            {
                case CastKind.Heal:
                    if (target == null || !target.Active) return;
                    var amount = rng.Spread(20f + caster.HealPower, CombatData.DamageSpread);
                    var healed = Math.Min(amount, target.MaxHp - target.Hp);
                    target.Hp += healed;
                    AddHealThreat(caster, healed);
                    Emit(BattleEventKind.Heal, caster, target, healed);
                    break;
                case CastKind.Revive:
                    if (target == null || !target.Downed) return;
                    target.Downed = false;
                    target.Hp = target.MaxHp * 0.3f;
                    target.Injured = true;
                    Emit(BattleEventKind.Revived, caster, target);
                    Write($"{caster.Name} поднимает {target.Name}");
                    break;
                case CastKind.ShamanHeal:
                    if (target == null || !target.Active) return;
                    var restored = Math.Min(CombatData.ShamanHeal, target.MaxHp - target.Hp);
                    target.Hp += restored;
                    Emit(BattleEventKind.Heal, caster, target, restored);
                    break;
                case CastKind.Collapse:
                    Emit(BattleEventKind.Collapse, caster, null);
                    Write("Свод рушится на отряд");
                    foreach (var hero in heroes)
                        if (hero.Active)
                            ApplyDamage(caster, hero, CombatData.CollapseDamage, 1f);
                    break;
            }
        }

        void Hit(Combatant attacker, Combatant defender, float multiplier, bool magic, float threatMultiplier)
        {
            if (!magic && rng.Chance(defender.DodgeChance))
            {
                Emit(BattleEventKind.Dodge, attacker, defender);
                return;
            }

            var damage = (attacker.Weapon + attacker.Power) * multiplier;
            if (!magic)
                damage *= 1f - defender.Armor / (defender.Armor + 50f + 10f * attacker.Level);
            damage = rng.Spread(damage, CombatData.DamageSpread);
            var crit = rng.Chance(attacker.CritChance);
            if (crit) damage *= CombatData.CritMultiplier;
            ApplyDamage(attacker, defender, damage, threatMultiplier, crit);
        }

        void ApplyDamage(Combatant attacker, Combatant defender, float damage, float threatMultiplier, bool crit = false)
        {
            if (defender.IsHero)
            {
                if (defender.StanceTimer > 0f) damage *= 0.6f;
                var absorbed = Math.Min(defender.Absorb, damage);
                defender.Absorb -= absorbed;
                damage -= absorbed;
            }

            defender.Hp -= damage;
            if (defender.IsHero) defender.DamageTaken += damage;
            Emit(BattleEventKind.Hit, attacker, defender, damage, crit);
            if (attacker.IsHero && !defender.IsHero)
                defender.Threat[attacker] = (defender.Threat.TryGetValue(attacker, out var t) ? t : 0f) + damage * threatMultiplier;

            if (defender.Hp > 0f) return;
            defender.Hp = 0f;
            defender.Cast = CastKind.None;
            if (defender.IsHero)
            {
                defender.Downed = true;
                defender.DownedTimer = CombatData.DownedSeconds;
                Emit(BattleEventKind.Downed, attacker, defender);
                Write($"{defender.Name} падает");
            }
            else
            {
                defender.Dead = true;
                XpPool += CombatData.XpPerEnemyLevel * defender.Level;
                Emit(BattleEventKind.Killed, attacker, defender);
                Write($"{defender.Name} повержен");
            }
        }

        void AddHealThreat(Combatant healer, float healed)
        {
            var active = CountActive(enemies);
            if (active == 0) return;
            var share = healed * CombatData.HealThreatPerPoint / active;
            foreach (var enemy in enemies)
                if (enemy.Active)
                    enemy.Threat[healer] = (enemy.Threat.TryGetValue(healer, out var t) ? t : 0f) + share;
        }

        Combatant TopThreat(Combatant enemy)
        {
            Combatant best = null;
            var bestThreat = float.MinValue;
            foreach (var pair in enemy.Threat)
            {
                if (!pair.Key.Active || pair.Value <= bestThreat) continue;
                best = pair.Key;
                bestThreat = pair.Value;
            }
            if (best != null) return best;

            best = FindHero(HeroClass.Guard) ?? FindHero(HeroClass.Priest) ?? FirstActive(heroes);
            if (best != null) enemy.Threat[best] = 0.1f;
            return best;
        }

        Combatant Focus()
        {
            Combatant shaman = null;
            Combatant weakest = null;
            foreach (var enemy in enemies)
            {
                if (!enemy.Active) continue;
                if (enemy.Enemy == EnemyKind.Shaman && (shaman == null || enemy.Hp < shaman.Hp)) shaman = enemy;
                if (weakest == null || enemy.Hp < weakest.Hp) weakest = enemy;
            }
            return shaman ?? weakest;
        }

        Combatant FindHero(HeroClass heroClass)
        {
            foreach (var hero in heroes)
                if (hero.Class == heroClass && hero.Active)
                    return hero;
            return null;
        }

        static Combatant FirstActive(List<Combatant> units)
        {
            foreach (var unit in units)
                if (unit.Active)
                    return unit;
            return null;
        }

        static int CountActive(List<Combatant> units)
        {
            var count = 0;
            foreach (var unit in units)
                if (unit.Active)
                    count++;
            return count;
        }

        BattleOutcome Evaluate()
        {
            if (CountActive(enemies) == 0 && reinforcements.Count == 0)
            {
                StandUpFallen();
                return BattleOutcome.Won;
            }

            var standing = CountActive(heroes);
            if (standing == 0)
            {
                foreach (var hero in heroes)
                    if (hero.Downed)
                    {
                        hero.Downed = false;
                        hero.Dead = true;
                    }
                Write("Отряд погиб");
                return BattleOutcome.Lost;
            }

            var fallen = heroes.Count - standing;
            if ((heroes.Count > 1 && fallen >= Math.Max(1, heroes.Count - 1)) || Time >= CombatData.MaxBattleSeconds)
            {
                StandUpFallen();
                Write("Отряд отступает");
                return BattleOutcome.Retreated;
            }
            return BattleOutcome.Ongoing;
        }

        void StandUpFallen()
        {
            foreach (var hero in heroes)
            {
                if (!hero.Downed) continue;
                hero.Downed = false;
                hero.Hp = 1f;
                hero.Injured = true;
            }
        }

        void Emit(BattleEventKind kind, Combatant source, Combatant target, float amount = 0f, bool crit = false) =>
            Events.Add(new BattleEvent(kind, source, target, amount, crit));

        void Write(string text) => log?.Add($"[{Time:0.0} с] {text}");
    }
}
