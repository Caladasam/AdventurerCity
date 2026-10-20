using System;

namespace AdventurerCity.Simulation
{
    static class Households
    {
        public static void DailyUpdate(World world)
        {
            var arrived = 0;
            var born = 0;
            var left = 0;
            foreach (var house in world.State.Buildings)
            {
                if (house.Kind != BuildingKind.House || !house.IsComplete) continue;

                var level = world.HouseLevel(house);
                var connected = house.IsConnected;
                var needsMet = true;
                var hungry = false;
                var survives = true;

                if (house.Residents > 0)
                {
                    var meal = connected ? Feed(world.Stock, house.Residents * SimConstants.FoodPerResident, level.NeedsBread) : Meal.None;
                    house.FoodMet = meal == Meal.Full;
                    hungry = meal == Meal.None;
                    house.FirewoodMet = !level.NeedsFirewood ||
                                        connected && world.Stock.Take(ResourceType.Logs, SimConstants.FirewoodPerHouse) ==
                                        SimConstants.FirewoodPerHouse;
                    needsMet = house.FoodMet && house.FirewoodMet;
                    survives = !hungry && house.FirewoodMet;
                    if (connected)
                        world.Stock.Add(ResourceType.Gold, (int)Math.Round(house.Residents * level.TaxPerResident * world.TaxMultiplier));
                }
                else
                {
                    house.FoodMet = true;
                    house.FirewoodMet = true;
                }

                if (needsMet)
                {
                    house.UnhappyDays = 0;
                    house.SatisfiedDays++;
                    TryUpgrade(world, house);
                }
                else
                {
                    house.SatisfiedDays = 0;
                    if (survives) house.UnhappyDays = 0;
                    else house.UnhappyDays++;
                    if (house.UnhappyDays >= SimConstants.DaysToDowngrade && house.Level > 1)
                    {
                        house.Level--;
                        house.UnhappyDays = 0;
                        var lowered = world.HouseLevel(house);
                        left += world.EvictNewest(house.Id, lowered.Capacity);
                        world.Report("Дом пришёл в упадок: " + lowered.Name);
                    }
                }

                var capacity = world.HouseLevel(house).Capacity;
                if (!connected || !needsMet || house.Residents >= capacity) continue;

                if (house.Residents >= 2 && CountAdults(world, house.Id) >= 2 &&
                    world.Random(100) < SimConstants.BirthChancePercent)
                {
                    world.AddCitizen(house.Id, 0);
                    born++;
                }
                else
                {
                    var age = SimConstants.ImmigrantMinAge +
                              world.Random(SimConstants.ImmigrantMaxAge - SimConstants.ImmigrantMinAge + 1);
                    world.AddCitizen(house.Id, age);
                    arrived++;
                }
            }

            if (arrived > 0) world.Report($"Новые жители: +{arrived}");
            if (born > 0) world.Report($"Родились дети: +{born}");
            if (left > 0) world.Report($"Покинули город: {left}");
        }

        static int CountAdults(World world, int houseId)
        {
            var adults = 0;
            foreach (var citizen in world.Citizens)
                if (citizen.HomeId == houseId && world.IsAdult(citizen))
                    adults++;
            return adults;
        }

        enum Meal
        {
            None,
            Substitute,
            Full
        }

        static Meal Feed(Stockpile stock, int need, bool breadOnly)
        {
            if (breadOnly)
            {
                var bread = stock.Take(ResourceType.Bread, need);
                if (bread >= need) return Meal.Full;
                return bread + stock.Take(ResourceType.Grain, need - bread) >= need ? Meal.Substitute : Meal.None;
            }
            var eaten = stock.Take(ResourceType.Grain, need);
            if (eaten < need) eaten += stock.Take(ResourceType.Bread, need - eaten);
            return eaten >= need ? Meal.Full : Meal.None;
        }

        static void TryUpgrade(World world, Building house)
        {
            var levels = world.Content.HouseLevels;
            if (house.Level >= world.MaxHouseLevel() || house.SatisfiedDays < SimConstants.DaysToUpgrade) return;

            var current = levels[house.Level - 1];
            var next = levels[house.Level];
            var stock = world.Stock;
            if (house.Residents < current.Capacity) return;
            if (!stock.Has(ResourceType.Planks, current.UpgradePlanks)) return;
            if (!stock.Has(ResourceType.Ore, current.UpgradeOre)) return;
            if (!stock.Has(ResourceType.Gems, current.UpgradeGems)) return;
            if (next.NeedsFirewood && !stock.Has(ResourceType.Logs, SimConstants.FirewoodPerHouse)) return;
            if (next.NeedsBread && !stock.Has(ResourceType.Bread, house.Residents * SimConstants.FoodPerResident)) return;

            stock.Take(ResourceType.Planks, current.UpgradePlanks);
            stock.Take(ResourceType.Ore, current.UpgradeOre);
            stock.Take(ResourceType.Gems, current.UpgradeGems);
            house.Level++;
            house.SatisfiedDays = 0;
            world.Report("Дом улучшен: " + next.Name);
        }
    }
}
