using System.Collections.Generic;
using AdventurerCity.Simulation;
using AdventurerCity.Simulation.Combat;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed partial class GameUi
    {
        static readonly Color TensionColor = new Color(0.9f, 0.45f, 0.2f);

        void CloseRow(Ui ui, string title, System.Action close, string hotkey = null)
        {
            ui.Row(() =>
            {
                ui.Title(title);
                ui.Flex();
                ui.Button(hotkey != null ? $"Закрыть ({hotkey})" : "Закрыть", close, true, null, 0f);
            });
        }

        void BuildEconomy(Ui ui)
        {
            var world = runner.Sim;
            ui.Width(700f);
            var resourceFlows = Flows(world);
            CloseRow(ui, "Экономика", () => economyOpen = false, "Tab");
            ui.Row(() =>
            {
                ui.Small("<b>Ресурс</b>", 90f);
                ui.Small("<b>Запас</b>", 60f);
                ui.Small("<b>Приход</b>", 60f);
                ui.Small("<b>Расход</b>", 60f);
                ui.Small("<b>Итог/день</b>", 76f);
                ui.Small("<b>Хватит на</b>", 90f);
                ui.Small("<b>Лимит запаса</b>");
            });
            ui.Scroll("economy", 620f, () =>
            {
                for (var i = 0; i < ResourceInfo.Count; i++)
                {
                    var type = (ResourceType)i;
                    var flow = resourceFlows[i];
                    var stock = world.Stock[type];
                    ui.Row(() =>
                    {
                        ui.Text($"<b>{ResourceInfo.Name(type)}</b>", 90f);
                        ui.Text(stock.ToString(), 60f);
                        ui.Text($"+{flow.Produced:0}", 60f);
                        ui.Text($"−{flow.Consumed:0}", 60f);
                        ui.Text(UiText.Signed(flow.Net, true), 76f);
                        var days = Economy.DaysLeft(stock, flow);
                        ui.Text(float.IsPositiveInfinity(days) ? "—" : days > 99f ? "> 99 дн." : days < SimConstants.ShortageWarningDays ? UiText.Red($"{days:0.0} дн.") : $"{days:0.0} дн.", 90f);
                        if (type == ResourceType.Gold) return;
                        var limit = world.StockLimit(type);
                        ui.Button("−", () => { if (limit > 0) world.Enqueue(new SetStockLimitCommand(type, limit - SimConstants.StockLimitStep)); }, limit > 0, null, 24f);
                        ui.Text(limit == 0 ? "без лимита" : limit.ToString(), 80f).style.unityTextAlign = TextAnchor.MiddleCenter;
                        ui.Button("+", () => world.Enqueue(new SetStockLimitCommand(type, limit + SimConstants.StockLimitStep)), true, null, 24f);
                    });
                    var stockLimit = type == ResourceType.Gold ? 0 : world.StockLimit(type);
                    if (stockLimit > 0 && stockLimit < flow.Consumed)
                        ui.Small(UiText.Red($"Лимит {stockLimit} ниже суточного расхода ({flow.Consumed:0}) — в полночь ресурса не хватит"));
                    ui.Small(UiText.Breakdown(flow));
                    ui.Space(3f);
                }
            });
        }

        void BuildMap(Ui ui)
        {
            var world = runner.Sim;
            const float width = 720f, height = 500f;
            ui.Width(width + 24f);
            CloseRow(ui, "Карта региона", () => mapOpen = false, "M");
            ui.Canvas(width, height, () =>
            {
                var cx = width * 0.5f;
                var cy = height * 0.5f;
                ui.Absolute(cx - 55f, cy - 20f, 110f, 40f, new Color(0.22f, 0.42f, 0.86f), () => ui.Text("<b>Город</b>").style.unityTextAlign = TextAnchor.MiddleCenter);
                foreach (var def in Region.Sites)
                {
                    var site = world.FindSite(def.Id);
                    if (site == null) continue;
                    var x = cx + def.MapX * 255f - 95f;
                    var y = cy - def.MapY * 200f - 48f;
                    var color = site.Discovered
                        ? def.Kind == SiteKind.Dungeon ? new Color(0.25f, 0.22f, 0.3f) : new Color(0.35f, 0.18f, 0.16f)
                        : new Color(0.18f, 0.18f, 0.2f);
                    ui.Absolute(x, y, 190f, 96f, color, () => BuildSite(ui, world, def, site));
                }
            });
        }

        void BuildSite(Ui ui, World world, SiteDef def, SiteState site)
        {
            if (!site.Discovered)
            {
                ui.Text($"<b>Неизведано</b> · {def.Distance} дн. пути");
                var scout = world.ScoutFor(def.Id);
                if (scout != null)
                {
                    ui.Small($"<color=#BBBBBB>Разведчик в пути: {scout.TicksLeft} с</color>");
                    return;
                }
                var canScout = world.CanScout(def.Id, out var reason);
                ui.Button("Разведать (1 герой)", () => world.Enqueue(new ScoutCommand(def.Id)), canScout, reason);
                if (!canScout) ui.Small(UiText.Gray(reason));
                return;
            }
            var dungeon = Dungeons.Get(def.DungeonId);
            ui.Text($"<b>{def.Name}</b>");
            ui.Small($"Добыча: {ResourceInfo.Name(dungeon.LootType).ToLower()} · {def.Distance} дн. · зачищен {site.TimesCleared} раз");
            ui.Bar(site.Tension / Region.TensionMax, TensionColor, 170f, 8f);
            var raid = world.RaidFrom(def.Id);
            var state = raid != null
                ? UiText.Red($"{(def.Kind == SiteKind.Dungeon ? "Вспышка" : "Набег")}: {(raid.Fighting ? "бой у ворот" : $"в городе через {raid.TicksLeft} с")}")
                : $"<color=#BBBBBB>{(def.Kind == SiteKind.Dungeon ? "Напряжение" : "Угроза набега")} {site.Tension:0}%{(world.IsHeroicUnlocked(def.DungeonId) ? " · есть героич." : "")}</color>";
            ui.Small(state);
        }

        void BuildStage(Ui ui)
        {
            var world = runner.Sim;
            ui.Width(580f);
            var current = world.CurrentStage;
            var next = world.Stage < Stages.Max ? Stages.Get(world.Stage + 1) : null;
            CloseRow(ui, $"Этап {current.Number}/{Stages.Max}: {current.Name}", () => stageOpen = false);
            ui.Small($"Слава гильдии: <b>{world.Fame}</b> (зачистка +1, героическая +2, отбитый набег +1). Чем выше слава, тем чаще и опытнее авантюристы в таверне.");
            if (next == null)
            {
                ui.Small("Поселение достигло высшего этапа.");
                BuildGoal(ui, world);
                return;
            }
            ui.Space(6f);
            ui.Text($"<b>Следующий этап: {next.Name}</b>");
            foreach (var (text, met) in world.StageRequirements(next)) ui.Text(UiText.Check(text, met));
            ui.Small($"<color=#BBBBBB>Откроется: {next.Unlocks}</color>");
            ui.Button($"Объявить поселение: {next.Name}", () => world.Enqueue(new AdvanceStageCommand()), world.CanAdvanceStage(out _), null, 0f, 30f);
            BuildGoal(ui, world);
        }

        void BuildGoal(Ui ui, World world)
        {
            ui.Space(6f);
            ui.Text(world.Victory ? $"<b>Цель игры достигнута на {world.State.VictoryDay}-й день</b>" : "<b>Цель игры</b>");
            foreach (var (text, met) in world.VictoryRequirements()) ui.Text(UiText.Check(text, met));
        }

        void BuildVictory(Ui ui)
        {
            var world = runner.Sim;
            ui.Width(540f);
            ui.Text("<size=24><color=#FFD54F><b>ПОБЕДА!</b></color></size>");
            ui.Small($"На {world.State.VictoryDay}-й день город авантюристов стал легендой: ратуша стоит, культ Мор'Гата повержен. Гильдия получила +10 славы. Игру можно продолжать.");
            ui.Button("Продолжить", () => victoryClosed = true, true, null, 0f, 30f);
        }

        void BuildExpeditionsWidget(Ui ui)
        {
            var world = runner.Sim;
            var expeditions = world.Expeditions;
            ui.Width(400f);
            ui.Row(() =>
            {
                ui.Text($"<b>Отрядов в походе: {expeditions.Count}</b>");
                ui.Flex();
                var guild = world.FindGuild();
                if (guild != null) ui.Button("Гильдия", () => tool.SelectedId = guild.Id, true, null, 80f);
            });
            for (var i = 0; i < expeditions.Count; i++)
            {
                var active = expeditions[i];
                ui.Row(() =>
                {
                    ui.Text($"<b>{active.Run.Dungeon.Name}</b>");
                    ui.Flex();
                    ui.Button("Смотреть бой", () => runner.WatchExpedition(active.Record.Id), true, null, 110f);
                });
                BuildExpeditionDetails(ui, active, true);
            }
        }

        void BuildExpeditionDetails(Ui ui, ActiveExpedition active, bool compact)
        {
            var run = active.Run;
            ui.Small($"{UiText.PhaseText(run)} · {run.LootSummary}{(run.Potions > 0 ? $" · зелий {run.Potions}" : "")}");
            foreach (var hero in run.Party) BuildUnit(ui, hero, hero.Dead ? "погиб(ла)" : hero.Downed ? "при смерти" : null);
            if (!compact && run.Battle != null)
                foreach (var enemy in run.Battle.Enemies)
                    BuildUnit(ui, enemy, enemy.Dead ? "повержен" : null);
            var lines = compact ? 3 : 6;
            for (var i = Mathf.Max(0, active.Log.Count - lines); i < active.Log.Count; i++)
                ui.Small($"<color=#BBBBBB>{active.Log[i]}</color>");
        }

        static void BuildUnit(Ui ui, Combatant unit, string state)
        {
            ui.Row(() =>
            {
                ui.Small(unit.IsHero ? $"{unit.Name} ({CombatData.Get(unit.Class).Name})" : unit.Name, 210f);
                ui.Bar(unit.Dead ? 0f : unit.HpRatio, UiText.HpColor(unit), 120f);
                ui.Small(state ?? $"{Mathf.CeilToInt(unit.Hp)}/{Mathf.CeilToInt(unit.MaxHp)}");
            });
        }

        void BuildRaidWarning(Ui ui)
        {
            var world = runner.Sim;
            ui.Width(500f);
            world.CountDefenders(out var heroes, out var guards);
            foreach (var raid in world.Raids)
            {
                var def = Region.Get(raid.SiteId);
                var site = world.FindSite(raid.SiteId);
                var from = site != null && site.Discovered ? $"«{def.Name}»" : "неизведанные земли";
                var kind = def.Kind == SiteKind.Dungeon ? "Вспышка" : "Набег";
                var siteId = raid.SiteId;
                ui.Row(() =>
                {
                    if (raid.Fighting)
                    {
                        ui.Text($"{UiText.Red($"<b>{kind}</b>")}: {from} — бой у ворот!");
                        ui.Flex();
                        ui.Button("Смотреть бой", () => runner.WatchRaid(siteId), true, null, 110f);
                    }
                    else
                    {
                        var when = raid.TicksLeft > 0 ? $"враги в городе через {raid.TicksLeft} с" : "враги у ворот, ждут конца боя";
                        ui.Text($"{UiText.Red($"<b>{kind}</b>")}: {from} — {when}");
                    }
                });
            }
            ui.Small($"<color=#BBBBBB>Защитники в городе: героев {heroes}, стражников и солдат {guards}. Если не выстоят — разграбят часть запасов.</color>");
        }
    }
}
