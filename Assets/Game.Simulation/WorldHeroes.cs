using System;
using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed class ActiveExpedition
    {
        public ExpeditionRecord Record;
        public Expedition Run;
        public readonly List<string> Log = new List<string>();
    }

    public sealed partial class World
    {
        readonly List<ActiveExpedition> expeditions = new List<ActiveExpedition>();
        bool wageWarningShown;
        bool rationWarningShown;
        int pendingCombatSteps;

        public IReadOnlyList<ActiveExpedition> Expeditions => expeditions;

        public static int XpToNextLevel(int level) => CombatData.XpPerLevel * level;

        public Building FindGuild()
        {
            foreach (var building in State.Buildings)
                if (building.Kind == BuildingKind.Guild && building.IsComplete && building.IsConnected)
                    return building;
            return null;
        }

        public int CitizenIndex(int citizenId)
        {
            var citizens = State.Citizens;
            for (var i = 0; i < citizens.Count; i++)
                if (citizens[i].Id == citizenId)
                    return i;
            return -1;
        }

        public static int Provisions(int heroes, int dungeonId) =>
            heroes * SimConstants.ProvisionsPerHero * Dungeons.Get(dungeonId).Distance;

        public bool CanSendExpedition(IReadOnlyList<int> heroIds, int dungeonId, bool heroic, out string reason)
        {
            if (FindGuild() == null)
            {
                reason = "Нужна достроенная гильдия с дорогой к складу";
                return false;
            }
            if (!IsDungeonOpen(dungeonId))
            {
                reason = "Данж ещё не разведан";
                return false;
            }
            if (heroic && !IsHeroicUnlocked(dungeonId))
            {
                reason = "Героический уровень открывается после первой зачистки";
                return false;
            }
            if (heroIds.Count == 0 || heroIds.Count > SimConstants.MaxPartySize)
            {
                reason = $"В отряде должно быть от 1 до {SimConstants.MaxPartySize} героев";
                return false;
            }
            foreach (var id in heroIds)
            {
                var index = CitizenIndex(id);
                if (index < 0 || !IsAvailable(State.Citizens[index]))
                {
                    reason = "Кто-то из отряда недоступен";
                    return false;
                }
                if (State.Citizens[index].Hungry)
                {
                    reason = $"{CitizenInfo.FullName(State.Citizens[index])} голодает: героям нужен паёк хлеба";
                    return false;
                }
            }
            var bread = Provisions(heroIds.Count, dungeonId);
            if (!Stock.Has(ResourceType.Bread, bread))
            {
                reason = $"Нужно {bread} хлеба на провизию";
                return false;
            }
            reason = null;
            return true;
        }

        internal bool HireHero(HeroClass heroClass, out string error)
        {
            error = null;
            if (FindGuild() == null)
            {
                error = "Нужна достроенная гильдия с дорогой к складу";
                return false;
            }
            if (!Stock.Has(ResourceType.Gold, SimConstants.HeroHireGold) || !Stock.Has(ResourceType.Planks, SimConstants.HeroHirePlanks))
            {
                error = $"Найм стоит {SimConstants.HeroHireGold} золота и {SimConstants.HeroHirePlanks} досок на снаряжение";
                return false;
            }

            var citizens = State.Citizens;
            var candidate = -1;
            for (var i = 0; i < citizens.Count; i++)
            {
                var citizen = citizens[i];
                if (citizen.IsHero || !IsAdult(citizen)) continue;
                if (candidate < 0 || citizen.WorkId == 0 && citizens[candidate].WorkId != 0) candidate = i;
            }
            if (candidate < 0)
            {
                error = "Нет свободных взрослых жителей для найма";
                return false;
            }

            var hero = citizens[candidate];
            hero.IsHero = true;
            hero.Class = heroClass;
            hero.HeroLevel = 1;
            hero.HeroXp = 0;
            hero.ClassMask = 0;
            hero.SlotA = Ability.None;
            hero.SlotB = Ability.None;
            hero.TrainingClass = 0;
            hero.WorkId = 0;
            hero.Trait3 = HeroTrait.None;
            AssignTraits(ref hero);
            citizens[candidate] = hero;
            Stock.Take(ResourceType.Gold, SimConstants.HeroHireGold);
            Stock.Take(ResourceType.Planks, SimConstants.HeroHirePlanks);
            workforceDirty = true;
            Report($"{CitizenInfo.FullName(hero)} — новый герой гильдии: {CombatData.Get(heroClass).Name} ({Traits.Get(hero.Trait1).Name}, {Traits.Get(hero.Trait2).Name})");
            AddChronicle(hero.Id, $"принят(а) в гильдию: {CombatData.Get(heroClass).Name}");
            return true;
        }

        internal bool SendExpedition(IReadOnlyList<int> heroIds, int dungeonId, bool heroic, out string error)
        {
            if (!CanSendExpedition(heroIds, dungeonId, heroic, out error)) return false;

            Stock.Take(ResourceType.Bread, Provisions(heroIds.Count, dungeonId));
            var record = new ExpeditionRecord
            {
                Id = State.NextExpeditionId++,
                Seed = Random(1000000000),
                HeroIds = new List<int>(heroIds),
                DungeonId = dungeonId,
                Heroic = heroic
            };
            foreach (var id in heroIds)
            {
                var index = CitizenIndex(id);
                var hero = State.Citizens[index];
                hero.OnExpedition = true;
                State.Citizens[index] = hero;
            }
            State.Expeditions.Add(record);
            expeditions.Add(Launch(record));
            Report($"Отряд выступил: «{Dungeons.Get(dungeonId).Name}»{(heroic ? ", героический" : "")}");
            return true;
        }

        ActiveExpedition Launch(ExpeditionRecord record)
        {
            var party = new List<Combatant>();
            foreach (var id in record.HeroIds)
            {
                var index = CitizenIndex(id);
                if (index < 0) continue;
                party.Add(HeroCombatant(State.Citizens[index]));
            }
            ApplyBonds(party, record.HeroIds);

            var active = new ActiveExpedition { Record = record };
            active.Run = new Expedition(party, record.Seed, Dungeons.Get(record.DungeonId), record.Heroic, active.Log);
            return active;
        }

        void RestoreExpeditions()
        {
            foreach (var record in State.Expeditions)
            {
                var active = Launch(record);
                var steps = (int)Math.Round(record.Elapsed / CombatData.Tick);
                for (var i = 0; i < steps && !active.Run.IsDone; i++) active.Run.Step();
                expeditions.Add(active);
            }
        }

        void FeedHungryHeroes()
        {
            var citizens = State.Citizens;
            for (var i = 0; i < citizens.Count; i++)
            {
                var hero = citizens[i];
                if (!hero.Hungry || hero.OnExpedition) continue;
                if (!Stock.Has(ResourceType.Bread, SimConstants.HeroBreadPerDay)) return;
                Stock.Take(ResourceType.Bread, SimConstants.HeroBreadPerDay);
                hero.Hungry = false;
                citizens[i] = hero;
            }
        }

        public bool RealtimeCombat { get; set; }

        public void AdvanceCombat(int steps)
        {
            steps = Math.Min(steps, pendingCombatSteps);
            if (steps <= 0) return;
            pendingCombatSteps -= steps;
            RunCombatSteps(steps);
        }

        void TickExpeditions()
        {
            FeedHungryHeroes();
            if (!RealtimeCombat)
            {
                RunCombatSteps(SimConstants.CombatStepsPerTick);
                return;
            }

            pendingCombatSteps += SimConstants.CombatStepsPerTick;
            var backlog = pendingCombatSteps - SimConstants.CombatStepsPerTick;
            if (backlog > 0) AdvanceCombat(backlog);
        }

        void RunCombatSteps(int steps)
        {
            StepRaids(steps);
            for (var e = expeditions.Count - 1; e >= 0; e--)
            {
                var active = expeditions[e];
                for (var i = 0; i < steps && !active.Run.IsDone; i++) active.Run.Step();
                active.Record.Elapsed = active.Run.Elapsed;
                if (!active.Run.IsDone) continue;

                CompleteExpedition(active);
                expeditions.RemoveAt(e);
                State.Expeditions.Remove(active.Record);
            }
        }

        internal void GrantHeroXp(ref Citizen hero, int xp)
        {
            hero.HeroXp += xp;
            while (hero.HeroLevel < CombatData.MaxLevel && hero.HeroXp >= XpToNextLevel(hero.HeroLevel))
            {
                hero.HeroXp -= XpToNextLevel(hero.HeroLevel);
                hero.HeroLevel++;
                Report($"{CitizenInfo.FullName(hero)} достигает {hero.HeroLevel}-го уровня");
                AddChronicle(hero.Id, $"достиг(ла) {hero.HeroLevel}-го уровня");
            }
        }

        void CompleteExpedition(ActiveExpedition active)
        {
            var run = active.Run;
            var greed = LootBonus(active.Record.HeroIds);
            var extraLoot = (int)Math.Round(run.Loot * greed);
            var extraGold = (int)Math.Round(run.Gold * greed);
            Stock.Add(run.LootType, run.Loot + extraLoot);
            Stock.Add(ResourceType.Gold, run.Gold + extraGold);

            var loot = run.LootSummary + (extraLoot + extraGold > 0 ? $" (+{greed * 100f:0}% от жадных)" : "");
            if (run.Cleared) Report($"«{run.Dungeon.Name}» зачищено! Добыча: {loot}");
            else if (run.Wiped) Report($"Отряд погиб: «{run.Dungeon.Name}», добыча потеряна");
            else Report($"Отряд отступил: «{run.Dungeon.Name}». Добыча: {loot}");
            ApplyExpeditionToSite(run);

            var citizens = State.Citizens;
            var survivors = new List<int>();
            for (var i = 0; i < active.Record.HeroIds.Count && i < run.Party.Count; i++)
            {
                var index = CitizenIndex(active.Record.HeroIds[i]);
                if (index < 0) continue;
                var hero = citizens[index];
                var fighter = run.Party[i];

                if (fighter.Dead)
                {
                    Report($"{CitizenInfo.FullName(hero)} погиб(ла): «{run.Dungeon.Name}»");
                    Bury(hero, $"пал(а) в «{run.Dungeon.Name}»");
                    citizens.RemoveAt(index);
                    workforceDirty = true;
                    continue;
                }

                survivors.Add(hero.Id);
                hero.OnExpedition = false;
                if (fighter.Injured && hero.InjuryDays == 0)
                {
                    hero.InjuryDays = SimConstants.InjuryDays;
                    Report($"{CitizenInfo.FullName(hero)} ранен(а) и {SimConstants.InjuryDays} дня восстанавливается");
                    AddChronicle(hero.Id, $"ранен(а) в «{run.Dungeon.Name}»");
                }
                if (run.Cleared) AddChronicle(hero.Id, $"зачистил(а) «{run.Dungeon.Name}»{(run.Heroic ? " (героич.)" : "")}");
                else if (run.BossReached) AddChronicle(hero.Id, $"дошёл(ла) до босса в «{run.Dungeon.Name}» и отступил(а)");

                GrantHeroXp(ref hero, (int)(fighter.XpGained * TraitXp(hero)));
                citizens[index] = hero;
            }
            ShareExperience(survivors);
        }

        void DailyHeroes()
        {
            var citizens = State.Citizens;
            var unpaid = false;
            var hungry = 0;
            for (var i = 0; i < citizens.Count; i++)
            {
                var hero = citizens[i];
                if (!hero.IsHero) continue;
                var wage = SimConstants.HeroWageGold + TraitWage(hero);
                if (Stock.Take(ResourceType.Gold, wage) < wage) unpaid = true;
                if (!hero.OnExpedition)
                {
                    hero.Hungry = Stock.Take(ResourceType.Bread, SimConstants.HeroBreadPerDay) < SimConstants.HeroBreadPerDay;
                    if (hero.Hungry) hungry++;
                    if (hero.InjuryDays > 0) hero.InjuryDays = Math.Max(0, hero.InjuryDays - DailyRecovery(hero));
                }
                citizens[i] = hero;
            }

            if (unpaid && !wageWarningShown) Report("Не хватает золота на жалованье героям");
            wageWarningShown = unpaid;
            if (hungry > 0 && !rationWarningShown) Report($"Героям не хватило пайка: голодают {hungry}, в поход они не пойдут");
            rationWarningShown = hungry > 0;
        }
    }
}
