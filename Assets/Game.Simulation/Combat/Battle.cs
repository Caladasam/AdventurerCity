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
        EnemyAoe,
        Ability,
        Enrage
    }

    public readonly struct BattleEvent
    {
        public readonly BattleEventKind Kind;
        public readonly Combatant Source;
        public readonly Combatant Target;
        public readonly float Amount;
        public readonly bool Crit;
        public readonly Ability Ability;

        public BattleEvent(BattleEventKind kind, Combatant source, Combatant target, float amount = 0f, bool crit = false, Ability ability = Ability.None)
        {
            Kind = kind;
            Source = source;
            Target = target;
            Amount = amount;
            Crit = crit;
            Ability = ability;
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
        const int EnemyHealSlot = 0, EnemyAoeSlot = 1;

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

        float GroupScale => heroes.Count > Dungeons.StandardParty ? Dungeons.StandardParty / (float)heroes.Count : 1f;

        public BattleOutcome Step()
        {
            if (Outcome != BattleOutcome.Ongoing) return Outcome;

            const float dt = CombatData.Tick;
            var calm = Time < CombatData.EnrageStart;
            Time += dt;
            if (calm && Time >= CombatData.EnrageStart)
            {
                Emit(BattleEventKind.Enrage, null, null);
                Write("Враги звереют: урон растёт с каждой секундой");
            }
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
            var def = CombatData.Get(enemy.Enemy);
            if (def.AoeDamage > 0f) enemy.Cooldowns[EnemyAoeSlot] = def.AoeFirst;
            var tank = FindRole(CombatRole.Tank);
            if (tank != null) enemy.Threat[tank] = CombatData.GuardOpeningThreat;
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
            unit.GuardTimer -= dt;
            unit.EmpowerTimer -= dt;
            unit.EvadeTimer -= dt;
            unit.MarkTimer -= dt;
            unit.WeakenTimer -= dt;
            for (var i = 0; i < Combatant.CooldownSlots; i++) unit.Cooldowns[i] -= dt;
            if (unit.MaxMana > 0f) unit.Mana = Math.Min(unit.MaxMana, unit.Mana + unit.ManaRegen * dt);

            if (unit.HotTimer > 0f)
            {
                unit.HotTimer -= dt;
                unit.Hp = Math.Min(unit.MaxHp, unit.Hp + unit.HotPerSecond * dt);
            }
            if (unit.DotTimer > 0f)
            {
                unit.DotTimer -= dt;
                var source = unit.DotSource;
                ApplyDamage(source, unit, unit.DotPerSecond * dt, source != null && source.Role == CombatRole.Tank ? CombatData.GuardThreatMultiplier : 1f);
                if (!unit.Active) return;
            }

            if (unit.IsCasting)
            {
                unit.CastTimer -= dt;
                if (unit.CastTimer <= 0f) ResolveCast(unit);
            }
        }

        void ActHero(Combatant hero)
        {
            if (hero.IsCasting) return;
            if (hero.Role != CombatRole.Tank && hero.Role != CombatRole.Healer && Time < CombatData.DamageDealerDelay &&
                FindRole(CombatRole.Tank) != null)
                return;
            if (hero.Gcd <= 0f && UseAbility(hero)) hero.Gcd = CombatData.GlobalCooldown;
            if (hero.IsCasting || hero.SwingTimer > 0f) return;

            var target = Focus();
            if (target == null) return;
            Hit(hero, target, 1f, hero.MagicAttack, 1f);
            hero.SwingTimer = hero.AttackInterval;
        }

        bool UseAbility(Combatant hero)
        {
            var caster = CastingEnemy();
            if (caster != null)
            {
                for (var slot = 0; slot < hero.Abilities.Count; slot++)
                {
                    var def = Classes.Get(hero.Abilities[slot]);
                    if (!def.Interrupts || !Ready(hero, slot, def)) continue;
                    Execute(hero, slot, def, caster);
                    return true;
                }
            }

            for (var slot = 0; slot < hero.Abilities.Count; slot++)
            {
                var def = Classes.Get(hero.Abilities[slot]);
                if (!Ready(hero, slot, def)) continue;
                var target = FindTarget(hero, def);
                if (target == null) continue;
                Execute(hero, slot, def, target);
                return true;
            }
            return false;
        }

        static bool Ready(Combatant hero, int slot, AbilityDef def) =>
            slot < Combatant.CooldownSlots && hero.Cooldowns[slot] <= 0f && hero.Mana >= def.Mana;

        Combatant FindTarget(Combatant hero, AbilityDef def)
        {
            switch (def.Trigger)
            {
                case AbilityTrigger.EnemyCasting:
                    return CastingEnemy();
                case AbilityTrigger.EnemyOnAlly:
                    foreach (var enemy in enemies)
                    {
                        if (!enemy.Active) continue;
                        var target = TopThreat(enemy);
                        if (target != null && target != hero) return enemy;
                    }
                    return null;
                case AbilityTrigger.SelfHurt:
                    return hero.HpRatio < 0.5f ? hero : null;
                case AbilityTrigger.AllyHurt:
                    return Wounded(0.7f, false);
                case AbilityTrigger.AllyCritical:
                    return Wounded(0.35f, false);
                case AbilityTrigger.AllyNeedsHot:
                    return Wounded(0.85f, true);
                case AbilityTrigger.AllyDowned:
                    foreach (var ally in heroes)
                        if (ally.Downed)
                            return ally;
                    return null;
                case AbilityTrigger.GroupHurt:
                    var hurt = 0;
                    foreach (var ally in heroes)
                        if (ally.Active && ally.HpRatio < 0.75f)
                            hurt++;
                    return hurt >= 2 ? hero : null;
                case AbilityTrigger.TankUnshielded:
                    var tank = FindRole(CombatRole.Tank);
                    return tank != null && tank.Absorb <= 0f ? tank : null;
                case AbilityTrigger.TwoEnemies:
                    return CountActive(enemies) >= 2 ? Focus() : null;
                case AbilityTrigger.ThreeEnemies:
                    return CountActive(enemies) >= 3 ? Focus() : null;
                case AbilityTrigger.TargetUnmarked:
                    var focus = Focus();
                    return focus != null && focus.MarkTimer <= 0f ? focus : null;
                case AbilityTrigger.TargetClean:
                    var clean = Focus();
                    if (clean != null && clean.DotTimer <= 0f) return clean;
                    foreach (var enemy in enemies)
                        if (enemy.Active && enemy.DotTimer <= 0f)
                            return enemy;
                    return null;
                case AbilityTrigger.SelfTargeted:
                    foreach (var enemy in enemies)
                        if (enemy.Active && TopThreat(enemy) == hero)
                            return hero;
                    return null;
                case AbilityTrigger.NotTargeted:
                    var mark = Focus();
                    return mark != null && TopThreat(mark) != hero ? mark : null;
                default:
                    return IsSelfEffect(def.Effect) ? hero : Focus();
            }
        }

        static bool IsSelfEffect(AbilityEffect effect) =>
            effect == AbilityEffect.SelfGuard || effect == AbilityEffect.SelfRage || effect == AbilityEffect.SelfEvade ||
            effect == AbilityEffect.SelfEmpower || effect == AbilityEffect.PartyGuard || effect == AbilityEffect.PartyEmpower;

        void Execute(Combatant hero, int slot, AbilityDef def, Combatant target)
        {
            hero.Mana -= def.Mana;
            hero.Cooldowns[slot] = def.Cooldown;
            if (def.Cast > 0f)
            {
                hero.Cast = CastKind.Ability;
                hero.CastAbility = def.Id;
                hero.CastTimer = def.Cast;
                hero.CastTarget = target;
                Emit(BattleEventKind.CastStart, hero, target, def.Cast, false, def.Id);
                return;
            }
            Apply(hero, def, target);
        }

        void Apply(Combatant hero, AbilityDef def, Combatant target)
        {
            var threat = def.Threat * (hero.Role == CombatRole.Tank ? CombatData.GuardThreatMultiplier : 1f);
            Emit(BattleEventKind.Ability, hero, target, 0f, false, def.Id);

            switch (def.Effect)
            {
                case AbilityEffect.Strike:
                    if (!target.Active) return;
                    if (def.Interrupts && Interruptible(target)) Interrupt(hero, target);
                    Hit(hero, target, def.Power, def.Magic, threat);
                    if (def.Stun > 0f && target.Active && !IsBoss(target))
                    {
                        target.StunTimer = def.Stun;
                        target.Cast = CastKind.None;
                    }
                    break;
                case AbilityEffect.MultiStrike:
                    for (var i = 0; i < def.Hits && target.Active; i++) Hit(hero, target, def.Power, def.Magic, threat);
                    break;
                case AbilityEffect.AreaStrike:
                    foreach (var enemy in enemies.ToArray())
                        if (enemy.Active)
                            Hit(hero, enemy, def.Power, def.Magic, threat);
                    break;
                case AbilityEffect.ChainStrike:
                    var struck = 0;
                    if (target.Active)
                    {
                        Hit(hero, target, def.Power, def.Magic, threat);
                        struck++;
                    }
                    foreach (var enemy in enemies.ToArray())
                    {
                        if (struck >= def.Hits) break;
                        if (enemy == target || !enemy.Active) continue;
                        Hit(hero, enemy, def.Power, def.Magic, threat);
                        struck++;
                    }
                    break;
                case AbilityEffect.RandomStrike:
                    if (target.Active) Hit(hero, target, def.Power + def.Amount * rng.Value, def.Magic, threat);
                    break;
                case AbilityEffect.WeakenStrike:
                    if (!target.Active) return;
                    Hit(hero, target, def.Power, def.Magic, threat);
                    target.WeakenTimer = def.Duration;
                    break;
                case AbilityEffect.Drain:
                    var drained = Focus();
                    if (drained == null) return;
                    var dealt = Hit(hero, drained, def.Power, def.Magic, threat);
                    if (target.IsHero && target.Active) HealUnit(hero, target, dealt * def.Amount);
                    break;
                case AbilityEffect.Interrupt:
                    if (target.Active && Interruptible(target)) Interrupt(hero, target);
                    break;
                case AbilityEffect.Taunt:
                    if (!target.Active) return;
                    var max = 0f;
                    foreach (var value in target.Threat.Values) max = Math.Max(max, value);
                    target.Threat[hero] = max * 1.1f + 1f;
                    Emit(BattleEventKind.Taunt, hero, target);
                    Write($"{hero.Name} провоцирует {target.Name}");
                    break;
                case AbilityEffect.SelfGuard:
                    hero.GuardTimer = def.Duration;
                    hero.GuardAmount = def.Amount;
                    Write($"{hero.Name}: {def.Name}");
                    break;
                case AbilityEffect.SelfRage:
                    hero.EmpowerTimer = def.Duration;
                    hero.EmpowerAmount = def.Amount;
                    hero.GuardTimer = def.Duration;
                    hero.GuardAmount = 0.2f;
                    Write($"{hero.Name} впадает в ярость");
                    break;
                case AbilityEffect.SelfEvade:
                    hero.EvadeTimer = def.Duration;
                    break;
                case AbilityEffect.SelfEmpower:
                    hero.Hp = Math.Max(1f, hero.Hp - hero.MaxHp * 0.1f);
                    hero.EmpowerTimer = def.Duration;
                    hero.EmpowerAmount = def.Amount;
                    break;
                case AbilityEffect.Heal:
                    if (target.Active) HealUnit(hero, target, rng.Spread(def.Power + hero.HealPower * def.Scaling, CombatData.DamageSpread));
                    break;
                case AbilityEffect.HealOverTime:
                    if (!target.Active) return;
                    target.HotTimer = def.Duration;
                    target.HotPerSecond = (def.Power + hero.HealPower * def.Scaling) / def.Duration;
                    break;
                case AbilityEffect.GroupHeal:
                    foreach (var ally in heroes)
                        if (ally.Active)
                            HealUnit(hero, ally, (def.Power + hero.HealPower * def.Scaling) * GroupScale);
                    break;
                case AbilityEffect.Shield:
                    if (target.Active) target.Absorb = def.Power + hero.HealPower * def.Scaling;
                    break;
                case AbilityEffect.Revive:
                    if (!target.Downed) return;
                    target.Downed = false;
                    target.Hp = target.MaxHp * 0.3f;
                    target.Injured = true;
                    Emit(BattleEventKind.Revived, hero, target);
                    Write($"{hero.Name} поднимает {target.Name}");
                    break;
                case AbilityEffect.PartyGuard:
                    foreach (var ally in heroes)
                    {
                        if (!ally.Active || ally.GuardTimer > 0f && ally.GuardAmount > def.Amount * GroupScale) continue;
                        ally.GuardTimer = def.Duration;
                        ally.GuardAmount = def.Amount * GroupScale;
                    }
                    break;
                case AbilityEffect.PartyEmpower:
                    foreach (var ally in heroes)
                    {
                        if (!ally.Active || ally.EmpowerTimer > 0f && ally.EmpowerAmount > def.Amount * GroupScale) continue;
                        ally.EmpowerTimer = def.Duration;
                        ally.EmpowerAmount = def.Amount * GroupScale;
                    }
                    break;
                case AbilityEffect.Mark:
                    if (target.Active) target.MarkTimer = def.Duration;
                    break;
                case AbilityEffect.DamageOverTime:
                    if (!target.Active) return;
                    target.DotTimer = def.Duration;
                    target.DotPerSecond = (hero.Weapon + hero.Power) * def.Power * (1f + hero.DamageBonus);
                    target.DotSource = hero;
                    break;
                case AbilityEffect.AreaStun:
                    foreach (var enemy in enemies)
                    {
                        if (!enemy.Active || IsBoss(enemy)) continue;
                        enemy.StunTimer = def.Duration;
                        enemy.Cast = CastKind.None;
                    }
                    Write($"{hero.Name}: {def.Name}");
                    break;
            }
        }

        void Interrupt(Combatant source, Combatant target)
        {
            target.Cast = CastKind.None;
            Emit(BattleEventKind.Interrupt, source, target);
            Write($"{source.Name} прерывает {target.Name}");
        }

        void HealUnit(Combatant healer, Combatant target, float amount)
        {
            var healed = Math.Min(amount, target.MaxHp - target.Hp);
            if (healed <= 0f) return;
            target.Hp += healed;
            AddHealThreat(healer, healed);
            Emit(BattleEventKind.Heal, healer, target, healed);
        }

        Combatant Wounded(float below, bool withoutHot)
        {
            Combatant wounded = null;
            foreach (var ally in heroes)
            {
                if (!ally.Active || ally.HpRatio >= below || withoutHot && ally.HotTimer > 0f) continue;
                if (wounded == null || ally.HpRatio < wounded.HpRatio) wounded = ally;
            }
            return wounded;
        }

        Combatant CastingEnemy()
        {
            foreach (var enemy in enemies)
                if (enemy.Active && Interruptible(enemy))
                    return enemy;
            return null;
        }

        static bool IsBoss(Combatant unit) => !unit.IsHero && CombatData.Get(unit.Enemy).Boss;

        static bool Interruptible(Combatant unit) => unit.IsCasting && !IsBoss(unit);

        void ActEnemy(Combatant enemy)
        {
            if (enemy.IsCasting) return;
            var cd = enemy.Cooldowns;

            var def = CombatData.Get(enemy.Enemy);

            if (def.HealAmount > 0f && cd[EnemyHealSlot] <= 0f)
            {
                Combatant wounded = null;
                foreach (var ally in enemies)
                    if (ally.Active && ally.HpRatio < 1f && (wounded == null || ally.HpRatio < wounded.HpRatio))
                        wounded = ally;
                if (wounded != null)
                {
                    cd[EnemyHealSlot] = def.HealInterval;
                    StartCast(enemy, CastKind.EnemyHeal, def.HealCast, wounded);
                    return;
                }
            }

            if (def.SummonCount > 0 && !enemy.SummonUsed && enemy.HpRatio <= def.SummonAtHpRatio)
            {
                enemy.SummonUsed = true;
                for (var i = 0; i < def.SummonCount; i++)
                    reinforcements.Add(Combatant.FromEnemy(def.SummonKind, enemy.Scale, enemy.HpScale));
                Emit(BattleEventKind.Summon, enemy, null);
                Write($"{enemy.Name} зовёт подмогу");
            }

            if (def.AoeDamage > 0f && cd[EnemyAoeSlot] <= 0f)
            {
                cd[EnemyAoeSlot] = def.AoeInterval;
                StartCast(enemy, CastKind.EnemyAoe, def.AoeCast, null);
                Write($"{enemy.Name} готовит «{def.AoeName}»");
                return;
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
            var ability = caster.CastAbility;
            caster.Cast = CastKind.None;
            caster.CastTarget = null;
            caster.CastAbility = Ability.None;

            switch (kind)
            {
                case CastKind.Ability:
                    if (target != null) Apply(caster, Classes.Get(ability), target);
                    break;
                case CastKind.EnemyHeal:
                    if (target == null || !target.Active) return;
                    var restored = Math.Min(CombatData.Get(caster.Enemy).HealAmount * caster.Scale, target.MaxHp - target.Hp);
                    target.Hp += restored;
                    Emit(BattleEventKind.Heal, caster, target, restored);
                    break;
                case CastKind.EnemyAoe:
                    Emit(BattleEventKind.EnemyAoe, caster, null);
                    Write($"«{CombatData.Get(caster.Enemy).AoeName}» бьёт по отряду");
                    foreach (var hero in heroes)
                        if (hero.Active)
                            ApplyDamage(caster, hero, CombatData.Get(caster.Enemy).AoeDamage * caster.Scale, 1f);
                    break;
            }
        }

        float Hit(Combatant attacker, Combatant defender, float multiplier, bool magic, float threatMultiplier)
        {
            if (attacker.IsHero && attacker.Role == CombatRole.Tank && threatMultiplier == 1f) threatMultiplier = CombatData.GuardThreatMultiplier;
            if (!magic && rng.Chance(defender.EffectiveDodge))
            {
                Emit(BattleEventKind.Dodge, attacker, defender);
                return 0f;
            }

            var damage = (attacker.Weapon + attacker.Power) * multiplier;
            if (!magic)
                damage *= 1f - defender.Armor / (defender.Armor + 50f + 10f * attacker.Level);
            damage = rng.Spread(damage, CombatData.DamageSpread);
            var crit = rng.Chance(attacker.CritChance);
            if (crit) damage *= CombatData.CritMultiplier;
            damage *= 1f + attacker.DamageBonus + (attacker.EmpowerTimer > 0f ? attacker.EmpowerAmount : 0f);
            if (attacker.WeakenTimer > 0f) damage *= 1f - Classes.WeakenPenalty;
            return ApplyDamage(attacker, defender, damage, threatMultiplier, crit);
        }

        public float EnrageMultiplier => Time < CombatData.EnrageStart
            ? 1f
            : 1f + CombatData.EnrageStep * (1 + (int)((Time - CombatData.EnrageStart) / CombatData.EnrageInterval));

        float ApplyDamage(Combatant attacker, Combatant defender, float damage, float threatMultiplier, bool crit = false)
        {
            if (attacker != null && !attacker.IsHero) damage *= EnrageMultiplier;
            if (defender.IsHero && defender.Role == CombatRole.Tank) damage *= 1f - CombatData.TankMitigation;
            if (defender.MarkTimer > 0f) damage *= 1f + Classes.MarkBonus;
            if (defender.GuardTimer > 0f) damage *= 1f - defender.GuardAmount;
            if (defender.IsHero)
            {
                var absorbed = Math.Min(defender.Absorb, damage);
                defender.Absorb -= absorbed;
                damage -= absorbed;
            }

            defender.Hp -= damage;
            if (defender.IsHero) defender.DamageTaken += damage;
            Emit(BattleEventKind.Hit, attacker, defender, damage, crit);
            if (attacker != null && attacker.IsHero && !defender.IsHero)
                defender.Threat[attacker] = (defender.Threat.TryGetValue(attacker, out var t) ? t : 0f) + damage * threatMultiplier;

            if (defender.Hp > 0f) return damage;
            defender.Hp = 0f;
            defender.Cast = CastKind.None;
            if (defender.IsHero)
            {
                defender.Downed = true;
                defender.DownedTimer = CombatData.DownedSeconds;
                defender.HotTimer = 0f;
                defender.DotTimer = 0f;
                Emit(BattleEventKind.Downed, attacker, defender);
                Write($"{defender.Name} падает");
            }
            else
            {
                defender.Dead = true;
                defender.DotTimer = 0f;
                XpPool += CombatData.XpPerEnemyLevel * defender.Level;
                Emit(BattleEventKind.Killed, attacker, defender);
                Write($"{defender.Name} повержен");
            }
            return damage;
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

            best = FindRole(CombatRole.Tank) ?? FindRole(CombatRole.Healer) ?? FirstActive(heroes);
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
                if (CombatData.Get(enemy.Enemy).HealAmount > 0f && (shaman == null || enemy.Hp < shaman.Hp)) shaman = enemy;
                if (weakest == null || enemy.Hp < weakest.Hp) weakest = enemy;
            }
            return shaman ?? weakest;
        }

        Combatant FindRole(CombatRole role)
        {
            foreach (var hero in heroes)
                if (hero.Role == role && hero.Active)
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
            var retreatAt = heroes.Count <= 3 ? heroes.Count - 1 : heroes.Count / 2 + 1;
            if ((heroes.Count > 1 && fallen >= retreatAt) || Time >= CombatData.MaxBattleSeconds)
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

        void Emit(BattleEventKind kind, Combatant source, Combatant target, float amount = 0f, bool crit = false, Ability ability = Ability.None) =>
            Events.Add(new BattleEvent(kind, source, target, amount, crit, ability));

        void Write(string text) => log?.Add($"[{Time:0.0} с] {text}");
    }
}
