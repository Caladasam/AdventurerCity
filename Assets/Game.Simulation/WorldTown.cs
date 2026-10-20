using System;
using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        public IReadOnlyList<Visitor> Visitors => State.Visitors;
        public CaravanRecord Caravan => State.Caravan;
        public int Fame => State.Fame;

        void EnsureTown()
        {
            if (State.Visitors == null) State.Visitors = new List<Visitor>();
            if (State.Caravan == null) State.Caravan = new CaravanRecord();
            var c = State.Caravan;
            c.Buy = Resize(c.Buy);
            c.Sell = Resize(c.Sell);
            c.Supply = Resize(c.Supply);
            c.Demand = Resize(c.Demand);
        }

        static int[] Resize(int[] array)
        {
            if (array != null && array.Length >= ResourceInfo.Count) return array;
            var result = new int[ResourceInfo.Count];
            if (array != null) Array.Copy(array, result, array.Length);
            return result;
        }

        public Building FindWorking(BuildingKind kind)
        {
            foreach (var building in State.Buildings)
                if (building.Kind == kind && building.IsComplete && building.IsConnected && building.Workers > 0)
                    return building;
            return null;
        }

        void DailyTown()
        {
            DailyTavern();
            DailyCaravan();
            DailyTrainingYard();
            CheckVictory();
        }

        void DailyTavern()
        {
            var visitors = State.Visitors;
            for (var i = visitors.Count - 1; i >= 0; i--)
            {
                if (--visitors[i].DaysLeft > 0) continue;
                Report($"{CitizenInfo.FullName(visitors[i].Hero)} покинул(а) таверну и отправился(лась) дальше");
                visitors.RemoveAt(i);
            }

            if (FindWorking(BuildingKind.Tavern) == null || visitors.Count >= Town.MaxVisitors) return;
            var chance = Math.Min(Town.VisitorChanceMax, Town.VisitorChance + Town.VisitorChancePerFame * State.Fame);
            if (Random(1000) >= chance * 1000f) return;

            var visitor = MakeVisitor();
            visitors.Add(visitor);
            Report($"В таверне новый авантюрист — {CitizenInfo.FullName(visitor.Hero)}: {CombatData.Get(visitor.Hero.Class).Name} {visitor.Hero.HeroLevel} ур., просит {visitor.Price} золота");
        }

        Visitor MakeVisitor()
        {
            var female = Random(2) == 0;
            var classes = (HeroClass[])Enum.GetValues(typeof(HeroClass));
            var cap = Math.Min(CurrentStage.VisitorMaxLevel, 1 + State.Fame / Town.FamePerVisitorLevel);
            var hero = new Citizen
            {
                Id = State.NextCitizenId++,
                Female = female,
                FirstName = Random(CitizenInfo.FirstNameCount(female)),
                LastName = Random(CitizenInfo.LastNameCount),
                BornDay = Day - (20 + Random(20)) * SimConstants.DaysPerYear,
                IsHero = true,
                Class = classes[Random(classes.Length)],
                HeroLevel = 1 + Random(cap)
            };
            AssignTraits(ref hero);
            var visitor = new Visitor
            {
                Hero = hero,
                DaysLeft = Town.VisitorStayDays,
                Price = Town.VisitorBasePrice + Town.VisitorPricePerLevel * (hero.HeroLevel - 1)
            };
            if (Random(1000) < Town.VisitorGearChance * 1000f)
            {
                var rng = new Rng(Random(int.MaxValue));
                Item gear = null;
                for (var guard = 0; guard < 50; guard++)
                {
                    gear = Items.Roll(rng, hero.HeroLevel + 1, false, false, EnemyKind.Ratman);
                    if (gear.Rarity != ItemRarity.Normal && RequiredLevel(gear) <= hero.HeroLevel) break;
                }
                visitor.HasGear = true;
                visitor.Gear = gear;
                visitor.Price += Town.VisitorGearPrice;
            }
            return visitor;
        }

        public bool CanHireVisitor(int heroId, out string reason)
        {
            var visitor = FindVisitor(heroId);
            if (visitor == null)
            {
                reason = "Авантюрист уже ушёл";
                return false;
            }
            if (FindWorking(BuildingKind.Tavern) == null)
            {
                reason = "Нужна работающая таверна";
                return false;
            }
            if (!Stock.Has(ResourceType.Gold, visitor.Price))
            {
                reason = $"Нужно {visitor.Price} золота";
                return false;
            }
            if (FindHomeFor(0) == 0)
            {
                reason = "Нет свободного места в домах";
                return false;
            }
            reason = null;
            return true;
        }

        Visitor FindVisitor(int heroId)
        {
            foreach (var visitor in State.Visitors)
                if (visitor.Hero.Id == heroId)
                    return visitor;
            return null;
        }

        internal bool HireVisitor(int heroId, out string error)
        {
            if (!CanHireVisitor(heroId, out error)) return false;
            var visitor = FindVisitor(heroId);
            State.Visitors.Remove(visitor);
            Stock.Take(ResourceType.Gold, visitor.Price);
            var hero = visitor.Hero;
            hero.HomeId = FindHomeFor(0);
            GetBuilding(hero.HomeId).Residents++;
            State.Citizens.Add(hero);
            if (visitor.HasGear) AddItem(visitor.Gear, false).EquippedBy = hero.Id;
            workforceDirty = true;
            Report($"{CitizenInfo.FullName(hero)} вступил(а) в гильдию: {CombatData.Get(hero.Class).Name} {hero.HeroLevel} ур.");
            AddChronicle(hero.Id, $"пришёл(шла) из странствий и вступил(а) в гильдию: {CombatData.Get(hero.Class).Name}");
            return true;
        }

        void DailyCaravan()
        {
            var caravan = State.Caravan;
            if (caravan.Present)
            {
                if (--caravan.DaysLeft > 0) return;
                caravan.Present = false;
                caravan.NextDay = Day + Town.CaravanIntervalMin + Random(Town.CaravanIntervalSpread);
                Report("Караван ушёл с рынка");
                return;
            }
            if (FindWorking(BuildingKind.Market) == null) return;
            if (caravan.NextDay == 0) caravan.NextDay = Day + 2;
            if (Day < caravan.NextDay) return;

            caravan.Present = true;
            caravan.DaysLeft = Town.CaravanStayDays;
            var scale = CurrentStage.CaravanScale;
            foreach (var type in Town.Tradable)
            {
                var i = (int)type;
                var swing = 0.8f + Random(41) / 100f;
                caravan.Sell[i] = Math.Max(1, (int)Math.Round(Town.Value(type) * Town.CaravanBuyMarkdown * swing));
                caravan.Buy[i] = Math.Max(caravan.Sell[i] + 1, (int)Math.Round(Town.Value(type) * Town.CaravanSellMarkup * swing));
                caravan.Supply[i] = (int)Math.Round(Town.Supply(type) * scale);
                caravan.Demand[i] = (int)Math.Round(Town.Demand(type) * scale);
            }
            Report($"На рынок пришёл караван: торгует {Town.CaravanStayDays} дня");
        }

        public bool CanTrade(ResourceType type, int amount, bool buy, out string reason)
        {
            var caravan = State.Caravan;
            var i = (int)type;
            if (!caravan.Present || FindWorking(BuildingKind.Market) == null)
            {
                reason = "Каравана нет";
                return false;
            }
            if (Array.IndexOf(Town.Tradable, type) < 0 || amount <= 0)
            {
                reason = "Этим не торгуют";
                return false;
            }
            if (buy)
            {
                if (caravan.Supply[i] < amount)
                {
                    reason = "У каравана столько нет";
                    return false;
                }
                if (!Stock.Has(ResourceType.Gold, caravan.Buy[i] * amount))
                {
                    reason = $"Нужно {caravan.Buy[i] * amount} золота";
                    return false;
                }
            }
            else
            {
                if (caravan.Demand[i] < amount)
                {
                    reason = "Караван столько не возьмёт";
                    return false;
                }
                if (!Stock.Has(type, amount))
                {
                    reason = "Нет на складе";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        internal bool Trade(ResourceType type, int amount, bool buy, out string error)
        {
            if (!CanTrade(type, amount, buy, out error)) return false;
            var caravan = State.Caravan;
            var i = (int)type;
            if (buy)
            {
                Stock.Take(ResourceType.Gold, caravan.Buy[i] * amount);
                Stock.Add(type, amount);
                caravan.Supply[i] -= amount;
            }
            else
            {
                Stock.Take(type, amount);
                Stock.Add(ResourceType.Gold, caravan.Sell[i] * amount);
                caravan.Demand[i] -= amount;
            }
            return true;
        }

        internal bool SellItem(int itemId, out string error)
        {
            var item = FindItem(itemId);
            error = null;
            if (item == null) return false;
            if (!State.Caravan.Present || FindWorking(BuildingKind.Market) == null)
            {
                error = "Каравана нет";
                return false;
            }
            if (item.EquippedBy != 0)
            {
                error = "Сначала снимите предмет с героя";
                return false;
            }
            State.Items.Remove(item);
            Stock.Add(ResourceType.Gold, Town.ItemPrice(item));
            Report($"Продано каравану: {item.Name} за {Town.ItemPrice(item)} золота");
            return true;
        }
    }
}
