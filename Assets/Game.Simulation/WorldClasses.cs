using System;
using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        public static bool IsTraining(in Citizen hero) => hero.TrainingClass != 0;

        public static bool IsAvailable(in Citizen hero) => hero.IsHero && !hero.OnExpedition && !IsTraining(hero);

        public bool IsTitleDiscovered(int titleId) => State.Titles.Contains(titleId);

        public static List<Ability> SlotOptions(in Citizen hero)
        {
            var options = new List<Ability>();
            foreach (var def in Classes.All)
            {
                if (def.Class == hero.Class || !Classes.HasClass(hero.ClassMask, def.Class)) continue;
                options.AddRange(def.Abilities);
            }
            return options;
        }

        public static List<Ability> HeroAbilities(in Citizen hero)
        {
            var options = SlotOptions(hero);
            var slotA = options.Contains(hero.SlotA) ? hero.SlotA : Ability.None;
            var slotB = options.Contains(hero.SlotB) ? hero.SlotB : Ability.None;
            return Classes.Loadout(hero.Class, slotA, slotB);
        }

        public static TitleDef HeroTitle(in Citizen hero) => Classes.TitleFor(hero.Class, hero.ClassMask);

        public static Combatant HeroCombatant(in Citizen hero)
        {
            var fighter = Combatant.FromHero(CitizenInfo.FullName(hero), hero.Class, hero.HeroLevel, hero.InjuryDays > 0, hero.WeaponTier,
                hero.ArmorTier, HeroAbilities(hero), HeroTitle(hero));
            ApplyTraits(fighter, hero);
            return fighter;
        }

        public bool CanLearnClass(int heroId, HeroClass heroClass, out string reason)
        {
            var index = CitizenIndex(heroId);
            if (index < 0 || !State.Citizens[index].IsHero)
            {
                reason = "Герой не найден";
                return false;
            }
            var hero = State.Citizens[index];
            if (FindGuild() == null)
            {
                reason = "Нужна гильдия";
                return false;
            }
            if (hero.Class == heroClass || Classes.HasClass(hero.ClassMask, heroClass))
            {
                reason = "Класс уже изучен";
                return false;
            }
            if (IsTraining(hero))
            {
                reason = "Герой уже учится";
                return false;
            }
            if (hero.OnExpedition)
            {
                reason = "Герой в походе";
                return false;
            }
            var limit = Classes.ExtraClassLimit(hero.HeroLevel);
            if (Classes.CountClasses(hero.ClassMask) >= limit)
            {
                reason = limit == 0
                    ? $"Второй класс — с {Classes.SecondClassLevel}-го уровня"
                    : limit == 1 ? $"Третий класс — с {Classes.ThirdClassLevel}-го уровня" : "Изучено максимум классов";
                return false;
            }
            if (!Stock.Has(ResourceType.Gold, Classes.TrainingGold))
            {
                reason = $"Обучение стоит {Classes.TrainingGold} золота";
                return false;
            }
            reason = null;
            return true;
        }

        internal bool LearnClass(int heroId, HeroClass heroClass, out string error)
        {
            if (!CanLearnClass(heroId, heroClass, out error)) return false;
            var index = CitizenIndex(heroId);
            var hero = State.Citizens[index];
            hero.TrainingClass = (int)heroClass + 1;
            hero.TrainingDays = Math.Max(1, Classes.TrainingDays + TraitTrainingDays(hero));
            State.Citizens[index] = hero;
            Stock.Take(ResourceType.Gold, Classes.TrainingGold);
            Report($"{CitizenInfo.FullName(hero)} начинает учиться: {Classes.Get(heroClass).Name}, {Classes.TrainingDays} дн.");
            return true;
        }

        internal bool SetAbilitySlot(int heroId, int slot, Ability ability, out string error)
        {
            error = null;
            var index = CitizenIndex(heroId);
            if (index < 0) return false;
            var hero = State.Citizens[index];
            if (ability != Ability.None && !SlotOptions(hero).Contains(ability))
            {
                error = "Навык не изучен";
                return false;
            }
            if (hero.OnExpedition)
            {
                error = "Навыки меняются только в городе";
                return false;
            }
            if (slot == 0)
            {
                if (ability != Ability.None && hero.SlotB == ability) hero.SlotB = hero.SlotA;
                hero.SlotA = ability;
            }
            else
            {
                if (ability != Ability.None && hero.SlotA == ability) hero.SlotA = hero.SlotB;
                hero.SlotB = ability;
            }
            State.Citizens[index] = hero;
            return true;
        }

        void DailyTraining()
        {
            var citizens = State.Citizens;
            for (var i = 0; i < citizens.Count; i++)
            {
                var hero = citizens[i];
                if (!IsTraining(hero)) continue;
                hero.TrainingDays--;
                if (hero.TrainingDays <= 0)
                {
                    var learned = (HeroClass)(hero.TrainingClass - 1);
                    hero.TrainingClass = 0;
                    hero.ClassMask |= 1 << (int)learned;
                    var first = Classes.Get(learned).Abilities[0];
                    if (hero.SlotA == Ability.None) hero.SlotA = first;
                    else if (hero.SlotB == Ability.None) hero.SlotB = first;
                    Report($"{CitizenInfo.FullName(hero)} освоил(а) второй путь: {Classes.Get(learned).Name}");
                    AddChronicle(hero.Id, $"освоил(а) путь: {Classes.Get(learned).Name}");

                    var title = HeroTitle(hero);
                    if (title != null && !State.Titles.Contains(title.Id))
                    {
                        State.Titles.Add(title.Id);
                        Report($"Открыт титул «{title.Name}» ({Classes.Get(title.A).Name} + {Classes.Get(title.B).Name}): {title.BonusText}");
                    }
                }
                citizens[i] = hero;
            }
        }
    }
}
