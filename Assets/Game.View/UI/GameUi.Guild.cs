using System.Collections.Generic;
using AdventurerCity.Simulation;
using AdventurerCity.Simulation.Combat;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed partial class GameUi
    {
        readonly HashSet<int> partySelection = new HashSet<int>();
        readonly List<int> partyIds = new List<int>();
        int selectedDungeon = Dungeons.MineId;
        int selectedHeroId;
        int heroTab;
        ItemSlot stashSlot;
        bool selectedHeroic;

        int SelectedHero(World world) => selectedHeroId != 0 ? world.CitizenIndex(selectedHeroId) : -1;

        void BuildGuild(Ui ui, World world)
        {
            ui.Width(540f);
            var heroes = new List<Citizen>();
            foreach (var citizen in world.Citizens)
                if (citizen.IsHero)
                    heroes.Add(citizen);
            partySelection.RemoveWhere(id =>
            {
                var index = world.CitizenIndex(id);
                return index < 0 || !World.IsAvailable(world.Citizens[index]);
            });

            Header(ui, "Гильдия");
            ui.Small($"Героев: {heroes.Count}. Каждый в день: {SimConstants.HeroWageGold} + уровень зол. жалованья и {SimConstants.HeroBreadPerDay} хлеб пайка. " +
                     $"Провизия на поход: {SimConstants.ProvisionsPerHero} хлеба на героя за день пути.");
            ui.Small($"<b>Нанять</b> ({SimConstants.HeroHireGold} зол., {SimConstants.HeroHirePlanks} досок). Класс задаёт роль и 3 базовых навыка:");
            for (var start = 0; start < Classes.All.Count; start += 4)
            {
                var first = start;
                ui.Row(() =>
                {
                    for (var i = first; i < first + 4 && i < Classes.All.Count; i++)
                    {
                        var def = Classes.All[i];
                        ui.Button($"{def.Name} · {UiText.RoleShort(def.Role)}", () => world.Enqueue(new HireHeroCommand(def.Class)), true, def.Description, 126f);
                    }
                });
            }

            ui.Space(4f);
            if (heroes.Count == 0) ui.Small("Героев пока нет. Нанятый житель уходит с работы и тренируется в гильдии.");
            ui.Scroll("heroes", 300f, () =>
            {
                foreach (var hero in heroes)
                {
                    var id = hero.Id;
                    ui.Row(() =>
                    {
                        if (World.IsAvailable(hero))
                        {
                            var selected = partySelection.Contains(id);
                            ui.Check(selected, () =>
                            {
                                if (partySelection.Contains(id)) partySelection.Remove(id);
                                else if (partySelection.Count < SimConstants.MaxPartySize) partySelection.Add(id);
                            });
                        }
                        else
                        {
                            ui.Space(22f);
                        }
                        var xp = hero.HeroLevel >= CombatData.MaxLevel ? "макс." : $"{hero.HeroXp}/{World.XpToNextLevel(hero.HeroLevel)} оп.";
                        ui.Button($"{CitizenInfo.FullName(hero)} — {UiText.ClassLine(hero)}, {hero.HeroLevel} ур. <color=#AAAAAA>({xp})</color>",
                            () => selectedHeroId = selectedHeroId == id ? 0 : id, true, "Открыть карточку героя", 310f, 0f, selectedHeroId == id, "left");
                        ui.Small(UiText.GearMarks(world, id), 40f);
                        var status = hero.OnExpedition ? "в походе"
                            : World.IsTraining(hero) ? $"<color=#90CAF9>учится {hero.TrainingDays} дн.</color>"
                            : hero.Hungry ? UiText.Red("голодает")
                            : hero.InjuryDays > 0 ? $"<color=#FFB74D>ранен(а) {hero.InjuryDays} дн.</color>"
                            : UiText.Green("готов(а)");
                        ui.Small(status);
                    });
                }
            });

            partyIds.Clear();
            partyIds.AddRange(partySelection);
            ui.Space(4f);
            ui.Text("<b>Поход</b>");
            var open = new List<DungeonDef>();
            foreach (var dungeon in Dungeons.All)
                if (world.IsDungeonOpen(dungeon.Id))
                    open.Add(dungeon);
            if (!world.IsDungeonOpen(selectedDungeon) && open.Count > 0) selectedDungeon = open[0].Id;
            for (var start = 0; start < open.Count; start += 3)
            {
                var first = start;
                ui.Row(() =>
                {
                    for (var i = first; i < first + 3 && i < open.Count; i++)
                    {
                        var dungeon = open[i];
                        ui.Button($"{dungeon.Name}\n<size=10>{dungeon.Distance} дн. · добыча: {ResourceInfo.Name(dungeon.LootType).ToLower()}</size>",
                            () => selectedDungeon = dungeon.Id, true, null, 168f, 36f, selectedDungeon == dungeon.Id);
                    }
                });
            }
            var heroicUnlocked = world.IsHeroicUnlocked(selectedDungeon);
            if (!heroicUnlocked) selectedHeroic = false;
            ui.Row(() =>
            {
                ui.Check(selectedHeroic, () => selectedHeroic = !selectedHeroic, heroicUnlocked);
                ui.Small(heroicUnlocked ? "героический: враги ×1,5, добыча ×2, лучше предметы" : UiText.Gray("героический — после первой зачистки"));
            });

            var target = Dungeons.Get(selectedDungeon);
            var canSend = world.CanSendExpedition(partyIds, selectedDungeon, selectedHeroic, out var reason);
            var potions = world.Stock[ResourceType.Potions] > 0 ? $", {Mathf.Min(partyIds.Count, world.Stock[ResourceType.Potions])} зел." : "";
            ui.Button($"Отправить: «{target.Name}»{(selectedHeroic ? " (героич.)" : "")} — {partyIds.Count} гер., {World.Provisions(partyIds.Count, selectedDungeon)} хлеба{potions}",
                () =>
                {
                    world.Enqueue(new SendExpeditionCommand(new List<int>(partyIds), selectedDungeon, selectedHeroic));
                    partySelection.Clear();
                }, canSend, null, 0f, 30f);
            if (!canSend && reason != null)
                ui.Small(UiText.Gray(partyIds.Count == 0 ? $"Отметьте до {SimConstants.MaxPartySize} героев галочками" : reason));

            var expeditions = world.Expeditions;
            for (var i = 0; i < expeditions.Count; i++)
            {
                var active = expeditions[i];
                ui.Row(() =>
                {
                    ui.Text($"<b>Отряд: {active.Run.Dungeon.Name}</b>");
                    ui.Flex();
                    ui.Button("Смотреть бой", () => runner.WatchExpedition(active.Record.Id), true, null, 110f);
                });
                BuildExpeditionDetails(ui, active, expeditions.Count > 1);
            }
        }

        void BuildHeroPanel(Ui ui)
        {
            var world = runner.Sim;
            var index = SelectedHero(world);
            if (index < 0) return;
            ui.Width(460f);
            var hero = world.Citizens[index];
            ui.Key(hero.Id);
            ui.Row(() =>
            {
                ui.Title($"{CitizenInfo.FullName(hero)}");
                ui.Flex();
                ui.Button("×", () => selectedHeroId = 0, true, null, 24f);
            });
            ui.Small($"{UiText.ClassLine(hero)}, {hero.HeroLevel} ур." + (hero.HeroLevel >= CombatData.LegendLevel ? " · <color=#FFD54F>Легенда</color>" : hero.HeroLevel >= CombatData.VeteranLevel ? " · <color=#90CAF9>Ветеран</color>" : ""));
            ui.Row(() =>
            {
                var tabs = new[] { "Классы и навыки", "Снаряжение", "Личность" };
                for (var t = 0; t < tabs.Length; t++)
                {
                    var tab = t;
                    ui.Button(tabs[t], () => heroTab = tab, true, null, 140f, 0f, heroTab == t, "tab");
                }
            });
            if (heroTab == 2) BuildPersonality(ui, world, hero);
            else if (heroTab == 1) BuildGear(ui, world, hero);
            else BuildClasses(ui, world, hero);
        }

        void BuildClasses(Ui ui, World world, Citizen hero)
        {
            var primary = Classes.Get(hero.Class);
            var abilities = World.HeroAbilities(hero);
            var options = World.SlotOptions(hero);
            var title = World.HeroTitle(hero);
            ui.Small($"Роль в бою: {Classes.RoleName(primary.Role)}. {primary.Description}");
            ui.Small(title != null
                ? $"Титул: <b>«{title.Name}»</b> — {title.BonusText}"
                : UiText.Gray("Титула нет: некоторые сочетания классов дают скрытый титул с бонусом"));
            if (hero.HeroLevel < CombatData.VeteranLevel)
                ui.Small(UiText.Gray($"На {CombatData.VeteranLevel}-м уровне — «Ветеран» (бонус по роли), на {CombatData.LegendLevel}-м — «Легенда» (+10% ко всему)."));

            ui.Text("<b>Навыки</b> (герой применяет их сам, по порядку приоритета):");
            foreach (var ability in abilities)
            {
                var def = Classes.Get(ability);
                var from = def.Class == hero.Class ? "" : $" <color=#90CAF9>[{Classes.Get(def.Class).Name}]</color>";
                var cost = (def.Mana > 0f ? $"{def.Mana:0} маны · " : "") + (def.Cooldown > 0f ? $"раз в {def.Cooldown:0} с" : "без перезарядки");
                ui.Small($"<b>{def.Name}</b>{from} — {def.Text}. {UiText.Gray(cost)}");
            }

            if (options.Count > 0)
            {
                ui.Text($"<b>Свободные слоты</b> ({Classes.FreeSlots}): клик — следующий навык из изученных классов");
                ui.Row(() =>
                {
                    SlotButton(ui, world, hero, 0, hero.SlotA, options);
                    SlotButton(ui, world, hero, 1, hero.SlotB, options);
                });
            }

            ui.Small($"<b>Учить класс</b> — {Classes.TrainingGold} зол., {Classes.TrainingDays} дн. без походов. Второй класс с {Classes.SecondClassLevel}-го уровня, третий с {Classes.ThirdClassLevel}-го.");
            if (World.IsTraining(hero))
            {
                ui.Text($"<color=#90CAF9>Учится: {Classes.Get((HeroClass)(hero.TrainingClass - 1)).Name}, осталось {hero.TrainingDays} дн.</color>");
            }
            else
            {
                string reason = null;
                for (var start = 0; start < Classes.All.Count; start += 4)
                {
                    var first = start;
                    ui.Row(() =>
                    {
                        for (var i = first; i < first + 4 && i < Classes.All.Count; i++)
                        {
                            var def = Classes.All[i];
                            var can = world.CanLearnClass(hero.Id, def.Class, out var why);
                            if (!can && reason == null && why != "Класс уже изучен") reason = why;
                            ui.Button(def.Name, () => world.Enqueue(new LearnClassCommand(hero.Id, def.Class)), can, def.Description, 106f);
                        }
                    });
                }
                if (reason != null) ui.Small(UiText.Gray(reason));
            }

            var known = 0;
            var names = new List<string>();
            foreach (var t in Classes.Titles)
            {
                if (!world.IsTitleDiscovered(t.Id)) continue;
                known++;
                names.Add($"{t.Name} ({Classes.Get(t.A).Name} + {Classes.Get(t.B).Name})");
            }
            ui.Small(UiText.Gray($"Открыто титулов: {known}/{Classes.Titles.Count}{(names.Count > 0 ? ": " + string.Join(", ", names) : "")}"));
        }

        static void SlotButton(Ui ui, World world, Citizen hero, int slot, Ability current, List<Ability> options)
        {
            var text = current == Ability.None || !options.Contains(current) ? "— пусто —" : Classes.Get(current).Name;
            ui.Button(text, () =>
            {
                var position = options.IndexOf(current);
                var next = position + 1 >= options.Count ? Ability.None : options[position + 1];
                world.Enqueue(new SetAbilitySlotCommand(hero.Id, slot, next));
            }, true, current != Ability.None && options.Contains(current) ? Classes.Get(current).Text : null, 200f);
        }

        void BuildPersonality(Ui ui, World world, Citizen hero)
        {
            ui.Text("<b>Черты</b>");
            foreach (var trait in Traits.Of(hero))
            {
                var def = Traits.Get(trait);
                var color = def.Negative ? "#FF8A80" : trait == HeroTrait.Deathtouched ? "#CE93D8" : "#8BE28B";
                ui.Small($"<color={color}><b>{def.Name}</b></color> — {def.Description}");
            }

            ui.Text("<b>Отношения</b> <size=11>(друзья в одном отряде бьют на 5% сильнее, соперники — слабее)</size>");
            var relations = world.RelationsOf(hero.Id);
            if (relations.Count == 0) ui.Small(UiText.Gray("Пока ни с кем не сблизился — сходите вместе в поход"));
            var shown = 0;
            foreach (var (otherId, value) in relations)
            {
                if (shown++ == 6) break;
                var other = world.CitizenIndex(otherId);
                var name = other >= 0 ? CitizenInfo.FullName(world.Citizens[other]) : "павший герой";
                var color = value >= Traits.FriendThreshold ? "#8BE28B" : value <= Traits.RivalThreshold ? "#FF8A80" : "#BBBBBB";
                ui.Small($"<color={color}>{name}: {Traits.BondName(value)} ({value:+0;-0})</color>");
            }

            ui.Text("<b>Хроника</b>");
            var chronicle = world.ChronicleOf(hero.Id);
            ui.Scroll("chronicle", 260f, () =>
            {
                for (var i = chronicle.Count - 1; i >= 0; i--)
                    ui.Small($"<color=#BBBBBB>День {chronicle[i].Day}:</color> {chronicle[i].Text}");
            });
        }

        void BuildGear(Ui ui, World world, Citizen hero)
        {
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                var item = world.Equipped(hero.Id, slot);
                var itemSlot = slot;
                ui.Row(() =>
                {
                    ui.Small($"<b>{Items.SlotName(slot)}:</b> " + (item != null ? UiText.ItemTitle(item) : "<color=#777777>пусто</color>"), 350f);
                    if (item != null) ui.Button("Снять", () => world.Enqueue(new UnequipItemCommand(hero.Id, itemSlot)), true, null, 70f);
                });
                if (item == null) continue;
                if (item.UniqueId != 0) ui.Small($"<i><color=#C8A070>{Items.Unique(item.UniqueId).Lore}</color></i>");
                ui.Small(Items.Describe(item));
            }

            ui.Space(4f);
            ui.Row(() =>
            {
                ui.Text("<b>Склад гильдии</b>", 120f);
                foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
                {
                    var tab = slot;
                    ui.Button($"{Items.SlotName(slot)} ({world.Stash(slot).Count})", () => stashSlot = tab, true, null, 98f, 0f, stashSlot == slot);
                }
            });
            var stash = world.Stash(stashSlot);
            if (stash.Count == 0) ui.Small(UiText.Gray("Пусто. Предметы падают в данжах и куются в кузнице."));
            ui.Scroll("stash", 300f, () =>
            {
                foreach (var item in stash)
                {
                    var can = world.CanEquip(hero.Id, item.Id, out var reason);
                    ui.Row(() =>
                    {
                        ui.Small(UiText.ItemTitle(item) + "\n" + Items.Describe(item), 290f);
                        ui.Button("Надеть", () => world.Enqueue(new EquipItemCommand(hero.Id, item.Id)), can, reason, 66f);
                        ui.Button("Разобрать", () => world.Enqueue(new SalvageItemCommand(item.Id)), true, "Даёт: " + ResourceInfo.Format(World.SalvageYield(item)), 76f);
                    }, "row-top");
                }
            });
        }
    }
}
