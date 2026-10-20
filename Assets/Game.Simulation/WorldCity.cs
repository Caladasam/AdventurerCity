using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public static class City
    {
        public const float TownHallTaxBonus = 0.25f;
        public const int SoldierLevel = 4;
        public const float BarracksPillageScale = 0.5f;
        public const int TrainingGold = 5;
        public const int TrainingXp = 40;
        public const int UniqueGold = 1500;
        public const int UniqueGems = 10;
        public const int UniqueForgeTicks = 150;
        public const int UniqueItemLevel = 10;
    }

    public sealed partial class World
    {
        public float TaxMultiplier => HasComplete(BuildingKind.TownHall) ? 1f + City.TownHallTaxBonus : 1f;

        public bool Victory => State.VictoryDay > 0;

        void DailyTrainingYard()
        {
            if (FindWorking(BuildingKind.TrainingYard) == null) return;
            var citizens = State.Citizens;
            for (var i = 0; i < citizens.Count; i++)
            {
                var hero = citizens[i];
                if (!hero.IsHero || hero.OnExpedition || hero.HeroLevel >= CombatData.MaxLevel) continue;
                if (Stock.Take(ResourceType.Gold, City.TrainingGold) < City.TrainingGold) return;
                GrantHeroXp(ref hero, City.TrainingXp);
                citizens[i] = hero;
            }
        }

        public List<(string Text, bool Met)> VictoryRequirements()
        {
            var fortress = FindSite(Region.ForDungeon(Dungeons.FortressId).Id);
            return new List<(string, bool)>
            {
                ($"Поселение — «{Stages.Get(Stages.Max).Name}»", State.Stage >= Stages.Max),
                ("Построена ратуша", HasComplete(BuildingKind.TownHall)),
                ($"Героическая зачистка «{Dungeons.Get(Dungeons.FortressId).Name}»", fortress != null && fortress.HeroicClears > 0)
            };
        }

        void CheckVictory()
        {
            if (State.VictoryDay > 0) return;
            foreach (var (_, met) in VictoryRequirements())
                if (!met)
                    return;
            State.VictoryDay = Day;
            State.Fame += 10;
            Report("ПОБЕДА! Город авантюристов стал легендой: ратуша стоит, культ Мор'Гата повержен. Игру можно продолжать.");
        }

        public bool CanOrderUnique(int uniqueId, out string reason)
        {
            if (State.Stage < Stages.Max)
            {
                reason = $"Мастер берётся за такое только в «{Stages.Get(Stages.Max).Name}»";
                return false;
            }
            if (!HasForge())
            {
                reason = "Нужна достроенная кузница с дорогой к складу";
                return false;
            }
            foreach (var order in State.ForgeOrders)
                if (order.UniqueId != 0)
                {
                    reason = "Мастер уже работает над уникальной вещью";
                    return false;
                }
            if (!Stock.Has(ResourceType.Gold, City.UniqueGold) || !Stock.Has(ResourceType.Gems, City.UniqueGems))
            {
                reason = $"Нужно {City.UniqueGold} золота и {City.UniqueGems} самоцветов";
                return false;
            }
            reason = null;
            return true;
        }

        internal bool OrderUnique(int uniqueId, out string error)
        {
            if (!CanOrderUnique(uniqueId, out error)) return false;
            var def = Items.Unique(uniqueId);
            Stock.Take(ResourceType.Gold, City.UniqueGold);
            Stock.Take(ResourceType.Gems, City.UniqueGems);
            State.ForgeOrders.Add(new ForgeOrder { BaseId = def.BaseId, Tier = def.BaseTier, UniqueId = uniqueId });
            workforceDirty = true;
            Report($"Мастер взялся выковать «{def.Name}»: {City.UniqueForgeTicks / SimConstants.TicksPerDay} дней работы");
            return true;
        }
    }
}
