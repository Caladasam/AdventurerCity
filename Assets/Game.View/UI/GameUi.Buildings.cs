using System.Collections.Generic;
using AdventurerCity.Simulation;
using AdventurerCity.Simulation.Combat;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed partial class GameUi
    {
        static readonly Color ForgeBar = new Color(0.9f, 0.6f, 0.25f);

        void BuildSelection(Ui ui)
        {
            var world = runner.Sim;
            var building = world.GetBuilding(tool.SelectedId);
            if (building == null) return;
            ui.Key(building.Id);
            if (building.IsComplete)
            {
                switch (building.Kind)
                {
                    case BuildingKind.Guild: BuildGuild(ui, world); return;
                    case BuildingKind.Forge: BuildForge(ui, world, building); return;
                    case BuildingKind.Watchtower: BuildWatchtower(ui, world, building); return;
                    case BuildingKind.Temple: BuildTemple(ui, world, building); return;
                    case BuildingKind.Tavern: BuildTavern(ui, world); return;
                    case BuildingKind.Market: BuildMarket(ui, world); return;
                    case BuildingKind.TownHall:
                    case BuildingKind.Barracks:
                    case BuildingKind.TrainingYard: BuildProject(ui, world, building); return;
                }
            }
            BuildGeneric(ui, world, building);
        }

        void Header(Ui ui, string title)
        {
            ui.Row(() =>
            {
                ui.Title(title);
                ui.Flex();
                ui.Button("×", () => tool.SelectedId = 0, true, "Закрыть (Esc)", 24f);
            });
        }

        void BuildGeneric(Ui ui, World world, Building building)
        {
            ui.Width(400f);
            var def = world.Def(building);
            var lines = new List<string>();
            if (!building.IsComplete)
            {
                var built = 1f - building.ConstructionLeft / (float)def.BuildTicks;
                lines.Add($"Строится: {Mathf.RoundToInt(built * 100f)}%");
            }
            else if (building.Kind == BuildingKind.House)
            {
                var level = world.HouseLevel(building);
                lines.Add($"{level.Name}, жильцов {building.Residents}/{level.Capacity}");
                lines.Add("Еда: " + (building.FoodMet ? "есть" : UiText.Red("не хватает")) + (level.NeedsBread ? " (нужен хлеб)" : ""));
                if (level.NeedsFirewood) lines.Add("Дрова: " + (building.FirewoodMet ? "есть" : UiText.Red("не хватает")));
                lines.Add($"Налог: {level.TaxPerResident} зол. с жильца в день");
                if (building.Level < world.MaxHouseLevel())
                    lines.Add($"Улучшение: полный дом 3 сытых дня, {level.UpgradePlanks} досок" +
                              (level.UpgradeOre > 0 ? $", {level.UpgradeOre} руды" : "") + (level.UpgradeGems > 0 ? $", {level.UpgradeGems} самоцв." : ""));
                else if (building.Level < world.Content.HouseLevels.Count)
                    lines.Add($"Следующий уровень откроется на этапе «{Stages.Get(world.Content.HouseLevels[building.Level].Stage).Name}»");
                var food = level.NeedsBread ? "хлеба" : "зерна (хлеба, если зерна нет)";
                lines.Add($"Потребляет: {building.Residents * SimConstants.FoodPerResident} {food} в день" +
                          (level.NeedsFirewood ? $", {SimConstants.FirewoodPerHouse} бревно на дрова" : ""));
                UiText.AddPeople(world, lines, building.Id, true);
            }
            else if (def.Recipe != null)
            {
                lines.Add($"Рабочие: {building.Workers}/{def.Workers} · логистика {Mathf.RoundToInt(building.Efficiency * 100f)}%");
                var input = def.Recipe.Inputs.Length > 0 ? ResourceInfo.Format(def.Recipe.Inputs) + " → " : "";
                lines.Add($"Цикл: {input}{ResourceInfo.Format(def.Recipe.Outputs)} за {def.Recipe.CycleTicks} с");
                var cycles = Economy.CyclesPerDay(world, building);
                var perDay = "Сейчас в день: " + UiText.PerDay(def.Recipe.Outputs, cycles, "+");
                if (def.Recipe.Inputs.Length > 0) perDay += ", " + UiText.PerDay(def.Recipe.Inputs, cycles, "−");
                lines.Add(perDay);
                if (building.Paused) lines.Add(UiText.Red("На паузе"));
                else if (building.LimitReached) lines.Add(UiText.Red("Стоит: на складе достигнут лимит запаса"));
                else if (building.Stalled) lines.Add(UiText.Red("Ждёт сырьё"));
                UiText.AddPeople(world, lines, building.Id, false);
            }
            else if (building.Kind == BuildingKind.Storehouse)
            {
                lines.Add($"Радиус обслуживания: {def.StorageRadius} клеток дороги");
            }
            if (building.IsComplete && !building.IsConnected) lines.Add(UiText.Red("Нет дороги к складу"));

            Header(ui, def.Name);
            foreach (var line in lines) ui.Small(line);
            if (building.IsComplete && def.Recipe != null)
                ui.Row(() =>
                {
                    ui.Button(building.Paused ? "Продолжить" : "Пауза", () => world.Enqueue(new TogglePauseCommand(building.Id)), true, null, 120f);
                    ui.Button(building.Priority ? "Приоритет найма: вкл" : "Приоритет найма: выкл", () => world.Enqueue(new TogglePriorityCommand(building.Id)),
                        true, "Рабочие сначала идут в здания с приоритетом", 200f, 0f, building.Priority);
                });
        }

        void BuildWatchtower(Ui ui, World world, Building tower)
        {
            ui.Width(400f);
            var lines = new List<string>();
            UiText.AddPeople(world, lines, tower.Id, false);
            world.CountDefenders(out var heroes, out var guards);
            Header(ui, "Сторожевая башня");
            ui.Small($"Стражники: {tower.Workers}/{world.Def(tower).Workers}. Каждый стражник выходит на защиту города при набеге. " +
                     $"Сейчас в городе героев {heroes}, стражников и солдат {guards}.");
            foreach (var line in lines) ui.Small(line);
            if (!tower.IsConnected) ui.Small(UiText.Red("Нет дороги к складу"));
            ui.Button(tower.Priority ? "Приоритет найма: вкл" : "Приоритет найма: выкл", () => world.Enqueue(new TogglePriorityCommand(tower.Id)),
                true, null, 200f, 0f, tower.Priority);
        }

        void BuildTemple(Ui ui, World world, Building temple)
        {
            ui.Width(520f);
            var fallen = world.Fallen;
            Header(ui, "Храм и мемориал павших");
            ui.Small($"Пока храм работает, ранения героев заживают на день быстрее. Павшего героя можно вернуть: " +
                     $"{Traits.ResurrectGoldPerLevel} золота и {Traits.ResurrectOrePerLevel} руды за уровень. Воскрешённый теряет уровень, " +
                     $"{Traits.ResurrectInjuryDays} дн. восстанавливается и получает черту «Видевший смерть».");
            if (!temple.IsConnected) ui.Small(UiText.Red("Нет дороги к складу — храм не работает"));
            var brew = world.Def(temple).Recipe;
            ui.Small($"Жрец варит зелья: {ResourceInfo.Format(brew.Inputs)} → {ResourceInfo.Format(brew.Outputs)} за {brew.CycleTicks} с " +
                     $"(жрецов {temple.Workers}/{world.Def(temple).Workers}). Запас зелий: <b>{world.Stock[ResourceType.Potions]}</b>. " +
                     "Отряд берёт по зелью на героя; в бою герой пьёт его сам при 20% здоровья, а при просмотре боя — по вашему приказу.");
            ui.Space(4f);
            ui.Text("<b>Мемориал</b>");
            if (fallen.Count == 0) ui.Small(UiText.Gray("Мемориал пуст. Пусть так и остаётся."));
            ui.Scroll("fallen", 320f, () =>
            {
                for (var i = fallen.Count - 1; i >= 0; i--)
                {
                    var record = fallen[i];
                    var hero = record.Hero;
                    var can = world.CanResurrect(hero.Id, out var reason);
                    ui.Row(() =>
                    {
                        ui.Small($"<b>{CitizenInfo.FullName(hero)}</b> — {UiText.ClassLine(hero)}, {hero.HeroLevel} ур.\n" +
                                 $"<color=#BBBBBB>День {record.Day}: {record.Cause}</color>", 300f);
                        ui.Button($"Воскресить\n{World.ResurrectGold(hero)} зол., {World.ResurrectOre(hero)} руды", () => world.Enqueue(new ResurrectCommand(hero.Id)),
                            can, reason, 170f, 38f);
                    });
                }
            });
        }

        void BuildTavern(Ui ui, World world)
        {
            ui.Width(560f);
            var visitors = world.Visitors;
            Header(ui, "Таверна");
            ui.Small($"Сюда заходят странствующие авантюристы: у них свой класс, уровень (до {world.CurrentStage.VisitorMaxLevel}-го на этом этапе) и иногда своё снаряжение. " +
                     $"Каждый ждёт {Town.VisitorStayDays} дн. Слава гильдии {world.Fame}: чем выше, тем чаще и опытнее гости. Нужен трактирщик и свободное место в доме.");
            if (world.FindWorking(BuildingKind.Tavern) == null)
                ui.Small(UiText.Red("Таверна не работает: нужна дорога к складу и трактирщик"));
            if (visitors.Count == 0) ui.Small(UiText.Gray("Пока никого. Загляните через пару дней."));
            for (var i = visitors.Count - 1; i >= 0; i--)
            {
                var visitor = visitors[i];
                var hero = visitor.Hero;
                var traits = new List<string>();
                foreach (var trait in Traits.Of(hero)) traits.Add(Traits.Get(trait).Name);
                var gear = visitor.HasGear ? "\nСнаряжение: " + UiText.ItemTitle(visitor.Gear) : "";
                var can = world.CanHireVisitor(hero.Id, out var reason);
                ui.Row(() =>
                {
                    ui.Small($"<b>{CitizenInfo.FullName(hero)}</b> — {CombatData.Get(hero.Class).Name}, {hero.HeroLevel} ур. <color=#999999>(ещё {visitor.DaysLeft} дн.)</color>\n" +
                             $"<color=#BBBBBB>{string.Join(", ", traits)}</color>{gear}", 390f);
                    ui.Button($"Нанять\n{visitor.Price} зол.", () => world.Enqueue(new HireVisitorCommand(hero.Id)), can, reason, 120f, 40f);
                });
            }
        }

        void BuildMarket(Ui ui, World world)
        {
            ui.Width(640f);
            var caravan = world.Caravan;
            Header(ui, "Рынок");
            ui.Small($"Раз в {Town.CaravanIntervalMin}–{Town.CaravanIntervalMin + Town.CaravanIntervalSpread - 1} дн. приходит караван на {Town.CaravanStayDays} дн.: " +
                     "покупает излишки, продаёт недостающее и берёт добычу со склада гильдии. Цены и объёмы у каждого каравана свои.");
            if (world.FindWorking(BuildingKind.Market) == null)
                ui.Small(UiText.Red("Рынок не работает: нужна дорога к складу и торговец"));
            if (!caravan.Present)
            {
                ui.Small(caravan.NextDay > 0 ? $"Каравана нет. Ждём примерно к {caravan.NextDay}-му дню." : "Каравана нет.");
                return;
            }

            ui.Text(UiText.Green($"Караван на рынке ещё {caravan.DaysLeft} дн."));
            ui.Row(() =>
            {
                ui.Small("<b>Товар</b>", 90f);
                ui.Small("<b>Склад</b>", 50f);
                ui.Small("<b>Продать каравану</b>", 214f);
                ui.Small("<b>Купить у каравана</b>");
            });
            foreach (var type in Town.Tradable)
            {
                var i = (int)type;
                var lot = Town.Lot(type);
                ui.Row(() =>
                {
                    ui.Small(ResourceInfo.Name(type), 90f);
                    ui.Small(world.Stock[type].ToString(), 50f);
                    ui.Button($"{lot} за {caravan.Sell[i] * lot} зол. (берёт ещё {caravan.Demand[i]})", () => world.Enqueue(new TradeCommand(type, lot, false)),
                        world.CanTrade(type, lot, false, out _), null, 210f);
                    ui.Button($"{lot} за {caravan.Buy[i] * lot} зол. (есть {caravan.Supply[i]})", () => world.Enqueue(new TradeCommand(type, lot, true)),
                        world.CanTrade(type, lot, true, out _), null, 210f);
                });
            }

            var stash = world.Stash();
            ui.Space(4f);
            ui.Text($"<b>Добыча на складе гильдии</b> ({stash.Count})");
            if (stash.Count == 0) ui.Small(UiText.Gray("Склад гильдии пуст"));
            ui.Scroll("market", 220f, () =>
            {
                foreach (var item in stash)
                    ui.Row(() =>
                    {
                        ui.Small(UiText.ItemTitle(item), 470f);
                        ui.Button($"Продать: {Town.ItemPrice(item)}", () => world.Enqueue(new SellItemCommand(item.Id)), true, Items.Describe(item), 120f);
                    });
            });
        }

        void BuildProject(Ui ui, World world, Building building)
        {
            ui.Width(460f);
            var def = world.Def(building);
            string text;
            switch (building.Kind)
            {
                case BuildingKind.TownHall:
                    text = $"Сердце города: налоги +{City.TownHallTaxBonus * 100f:0}%. Нужна для победы.";
                    break;
                case BuildingKind.Barracks:
                    text = $"Солдаты казарм ({building.Workers}/{def.Workers}) защищают город как воины {City.SoldierLevel}-го уровня. Пока казармы работают, грабители уносят вдвое меньше.";
                    break;
                default:
                    text = $"Наставник ({building.Workers}/{def.Workers}) каждый день даёт героям в городе по {City.TrainingXp} опыта за {City.TrainingGold} золота с героя.";
                    break;
            }
            Header(ui, def.Name);
            ui.Small(text);
            if (def.Upkeep > 0) ui.Small(UiText.Gray($"Содержание: {def.Upkeep} золота в день"));
            if (!building.IsConnected) ui.Small(UiText.Red("Нет дороги к складу — не работает"));
            if (building.Kind == BuildingKind.TownHall) BuildGoal(ui, world);
        }

        void BuildForge(Ui ui, World world, Building forge)
        {
            ui.Width(720f);
            var orders = world.ForgeOrders;
            Header(ui, "Кузница");
            ui.Small($"Рабочие: {forge.Workers}/{world.Def(forge).Workers}. Куёт обычные предметы выбранной базы на склад гильдии; " +
                     "магические, редкие и уникальные — только из данжей. Ярусы: простой → железный → стальной → мифриловый. Наведите на ярус, чтобы увидеть цену.");
            if (orders.Count > 0 && forge.Workers == 0)
                ui.Small(UiText.Red("Нет рабочих: в городе не осталось взрослых, которых можно перевести в кузницу"));

            var tierNames = new[] { "Простой", "Железный", "Стальной", "Мифрил" };
            foreach (var b in Items.Bases)
            {
                ui.Row(() =>
                {
                    ui.Small($"<b>{char.ToUpper(b.Noun[0]) + b.Noun.Substring(1)}</b> <color=#999999>{Items.SlotName(b.Slot).ToLower()}: {Items.StatText(b.Implicit, b.ImplicitValues[1])}</color>", 300f);
                    for (var tier = 0; tier <= Items.MaxBaseTier; tier++)
                    {
                        var level = tier;
                        var can = world.CanCraft(b.Id, level, out var reason);
                        var cost = ResourceInfo.Format(World.CraftCost(b.Slot, level));
                        ui.Button(tierNames[level], () => world.Enqueue(new CraftItemCommand(b.Id, level)), can, cost + (reason != null ? "\n" + reason : ""), 90f);
                    }
                });
            }

            ui.Space(4f);
            ui.Text($"<b>Заказ уникальной вещи</b> <color=#AAAAAA>({City.UniqueGold} золота и {City.UniqueGems} самоцветов, {City.UniqueForgeTicks / SimConstants.TicksPerDay} дн.; только в «{Stages.Get(Stages.Max).Name}»)</color>");
            ui.Row(() =>
            {
                foreach (var unique in Items.Uniques)
                {
                    var id = unique.Id;
                    var can = world.CanOrderUnique(id, out var why);
                    ui.Button(unique.Name, () => world.Enqueue(new OrderUniqueCommand(id)), can, $"<i>{unique.Lore}</i>" + (why != null ? "\n" + why : ""), 134f, 32f);
                }
            });

            if (orders.Count > 0) ui.Text("<b>Очередь</b>");
            foreach (var order in orders)
                ui.Row(() =>
                {
                    ui.Small(order.UniqueId != 0 ? $"<color=#FF9A3C>{Items.Unique(order.UniqueId).Name}</color>" : Items.BaseName(Items.Base(order.BaseId), order.Tier), 300f);
                    ui.Bar(order.Progress / World.ForgeTicks(order), ForgeBar, 180f);
                });
        }
    }
}
