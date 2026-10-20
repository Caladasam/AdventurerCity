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

        public bool CanSendExpedition(IReadOnlyList<int> heroIds, out string reason)
        {
            if (FindGuild() == null)
            {
                reason = "Нужна достроенная гильдия с дорогой к складу";
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
                if (index < 0 || !State.Citizens[index].IsHero || State.Citizens[index].OnExpedition)
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
            var bread = heroIds.Count * SimConstants.ProvisionsPerHero;
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
            hero.WorkId = 0;
            citizens[candidate] = hero;
            Stock.Take(ResourceType.Gold, SimConstants.HeroHireGold);
            Stock.Take(ResourceType.Planks, SimConstants.HeroHirePlanks);
            workforceDirty = true;
            Report($"{CitizenInfo.FullName(hero)} — новый герой гильдии: {CombatData.Get(heroClass).Name}");
            return true;
        }

        internal bool SendExpedition(IReadOnlyList<int> heroIds, out string error)
        {
            if (!CanSendExpedition(heroIds, out error)) return false;

            Stock.Take(ResourceType.Bread, heroIds.Count * SimConstants.ProvisionsPerHero);
            var record = new ExpeditionRecord
            {
                Id = State.NextExpeditionId++,
                Seed = Random(1000000000),
                HeroIds = new List<int>(heroIds)
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
            Report("Отряд выступил в «Заброшенную шахту»");
            return true;
        }

        ActiveExpedition Launch(ExpeditionRecord record)
        {
            var party = new List<Combatant>();
            foreach (var id in record.HeroIds)
            {
                var index = CitizenIndex(id);
                if (index < 0) continue;
                var hero = State.Citizens[index];
                party.Add(Combatant.FromHero(CitizenInfo.FullName(hero), hero.Class, hero.HeroLevel, hero.InjuryDays > 0,
                    hero.WeaponTier, hero.ArmorTier));
            }

            var active = new ActiveExpedition { Record = record };
            active.Run = new Expedition(party, record.Seed, active.Log);
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

        void TickExpeditions()
        {
            FeedHungryHeroes();
            for (var e = expeditions.Count - 1; e >= 0; e--)
            {
                var active = expeditions[e];
                for (var i = 0; i < SimConstants.CombatStepsPerTick && !active.Run.IsDone; i++) active.Run.Step();
                active.Record.Elapsed = active.Run.Elapsed;
                if (!active.Run.IsDone) continue;

                CompleteExpedition(active);
                expeditions.RemoveAt(e);
                State.Expeditions.Remove(active.Record);
            }
        }

        void CompleteExpedition(ActiveExpedition active)
        {
            var run = active.Run;
            Stock.Add(ResourceType.Ore, run.Ore);
            Stock.Add(ResourceType.Gold, run.Gold);

            if (run.Cleared) Report($"Шахта зачищена! Добыча: {run.Ore} руды, {run.Gold} золота");
            else if (run.Wiped) Report("Отряд погиб в шахте, добыча потеряна");
            else Report($"Отряд отступил из шахты. Добыча: {run.Ore} руды, {run.Gold} золота");

            var citizens = State.Citizens;
            for (var i = 0; i < active.Record.HeroIds.Count && i < run.Party.Count; i++)
            {
                var index = CitizenIndex(active.Record.HeroIds[i]);
                if (index < 0) continue;
                var hero = citizens[index];
                var fighter = run.Party[i];

                if (fighter.Dead)
                {
                    Report($"{CitizenInfo.FullName(hero)} погиб(ла) в шахте");
                    citizens.RemoveAt(index);
                    workforceDirty = true;
                    continue;
                }

                hero.OnExpedition = false;
                if (fighter.Injured && hero.InjuryDays == 0)
                {
                    hero.InjuryDays = SimConstants.InjuryDays;
                    Report($"{CitizenInfo.FullName(hero)} ранен(а) и {SimConstants.InjuryDays} дня восстанавливается");
                }

                hero.HeroXp += fighter.XpGained;
                while (hero.HeroLevel < CombatData.MaxLevel && hero.HeroXp >= XpToNextLevel(hero.HeroLevel))
                {
                    hero.HeroXp -= XpToNextLevel(hero.HeroLevel);
                    hero.HeroLevel++;
                    Report($"{CitizenInfo.FullName(hero)} достигает {hero.HeroLevel}-го уровня");
                }
                citizens[index] = hero;
            }
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
                if (Stock.Take(ResourceType.Gold, SimConstants.HeroWageGold) < SimConstants.HeroWageGold) unpaid = true;
                if (!hero.OnExpedition)
                {
                    hero.Hungry = Stock.Take(ResourceType.Bread, SimConstants.HeroBreadPerDay) < SimConstants.HeroBreadPerDay;
                    if (hero.Hungry) hungry++;
                    if (hero.InjuryDays > 0) hero.InjuryDays--;
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
