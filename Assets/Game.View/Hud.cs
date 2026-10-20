using System.Collections.Generic;
using AdventurerCity.Simulation;
using AdventurerCity.Simulation.Combat;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed class Hud : MonoBehaviour
    {
        const float TopBarHeight = 30f;
        const float ToolbarHeight = 56f;
        const int LogLines = 8;

        static readonly Color ActiveTool = new Color(0.55f, 0.85f, 1f);

        readonly List<Rect> uiRects = new List<Rect>();
        GameRunner runner;
        BuildTool tool;
        GUIStyle richLabel;
        GUIStyle smallLabel;
        GUIStyle panelStyle;
        GUIStyle toolStyle;
        readonly HashSet<int> partySelection = new HashSet<int>();
        readonly List<int> partyIds = new List<int>();
        ResourceFlow[] flows;
        int flowsFrame = -1;
        bool economyOpen;
        bool mapOpen;
        int selectedDungeon = Dungeons.MineId;
        bool selectedHeroic;

        public void Init(GameRunner owner, BuildTool buildTool)
        {
            runner = owner;
            tool = buildTool;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) economyOpen = !economyOpen;
            if (Input.GetKeyDown(KeyCode.M)) mapOpen = !mapOpen;
        }

        ResourceFlow[] Flows(World world)
        {
            if (flowsFrame != Time.frameCount || flows == null)
            {
                flows = Economy.Compute(world);
                flowsFrame = Time.frameCount;
            }
            return flows;
        }

        static string Short(int value) => value >= 10000 ? $"{value / 1000}k" : value.ToString();

        static string Signed(float value, bool colored)
        {
            var rounded = Mathf.RoundToInt(value);
            var text = rounded > 0 ? "+" + rounded : rounded.ToString();
            if (!colored || rounded == 0) return text;
            return rounded > 0 ? $"<color=#8BE28B>{text}</color>" : $"<color=#FF8A80>{text}</color>";
        }

        public bool IsPointerOverUi(Vector3 mousePosition)
        {
            var point = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            foreach (var rect in uiRects)
                if (rect.Contains(point))
                    return true;
            return false;
        }

        void OnGUI()
        {
            uiRects.Clear();
            if (richLabel == null)
            {
                richLabel = new GUIStyle(GUI.skin.label) { richText = true };
                smallLabel = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11, wordWrap = true };
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0.11f, 0.12f, 0.14f, 0.94f));
                background.Apply();
                panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = background } };
                toolStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, wordWrap = true };
            }

            var world = runner.Sim;
            DrawTopBar(world);
            if (runner.IsWatchingDungeon)
            {
                DrawLog();
                return;
            }
            DrawToolbar(world);
            DrawLog();
            DrawSelection(world);
            DrawExpeditionWidget(world);
            DrawRaidWarning(world);
            if (economyOpen) DrawEconomy(world);
            if (mapOpen) DrawMap(world);
            DrawHover();
        }

        Rect Panel(Rect rect)
        {
            uiRects.Add(rect);
            GUI.Box(rect, GUIContent.none, panelStyle);
            return rect;
        }

        void DrawTopBar(World world)
        {
            var rect = Panel(new Rect(0f, 0f, Screen.width, TopBarHeight));
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, rect.height - 6f));
            GUILayout.BeginHorizontal();
            var resourceFlows = Flows(world);
            for (var i = 0; i < ResourceInfo.Count; i++)
            {
                var type = (ResourceType)i;
                GUILayout.Label($"{ResourceInfo.Name(type)}: {Short(world.Stock[type])} {Signed(resourceFlows[i].Net, true)}",
                    richLabel, GUILayout.Width(104f));
            }
            var minutes = Mathf.FloorToInt(world.DayProgress * 24f * 60f);
            GUILayout.Label($"День {world.Day}, {minutes / 60:00}:{minutes % 60:00}", GUILayout.Width(105f));
            GUILayout.Label($"Жители: {world.Population} (раб. {world.EmployedWorkers}/{world.Adults})", GUILayout.Width(170f));
            GUILayout.FlexibleSpace();
            for (var i = 0; i < runner.Speeds.Count; i++)
            {
                GUI.backgroundColor = i == runner.SpeedIndex ? ActiveTool : Color.white;
                if (GUILayout.Button($"x{runner.Speeds[i]}", GUILayout.Width(34f))) runner.SetSpeed(i);
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Сохр.", GUILayout.Width(56f))) runner.Save();
            if (GUILayout.Button("Загр.", GUILayout.Width(56f))) runner.Load();
            if (GUILayout.Button("Новая", GUILayout.Width(60f))) runner.NewGame();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawToolbar(World world)
        {
            var rect = Panel(new Rect(0f, Screen.height - ToolbarHeight, Screen.width, ToolbarHeight));
            GUILayout.BeginArea(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f));
            GUILayout.BeginHorizontal();

            if (ToolButton($"Дорога\n{SimConstants.RoadGoldCost} {ResourceInfo.Name(ResourceType.Gold)}/кл.", tool.Mode == ToolMode.Road))
                tool.SelectRoad();
            foreach (var def in world.Content.Buildings)
            {
                var active = tool.Mode == ToolMode.Build && tool.Kind == def.Kind;
                if (ToolButton($"{def.Name}\n{ResourceInfo.Format(def.Cost)}", active))
                    tool.SelectBuilding(def.Kind);
                DrawSwatch(GUILayoutUtility.GetLastRect(), Palette.Building(def.Kind));
            }
            if (ToolButton("Снос", tool.Mode == ToolMode.Demolish))
                tool.SelectDemolish();
            if (ToolButton("Экономика\n(Tab)", economyOpen))
                economyOpen = !economyOpen;
            if (ToolButton("Карта\n(M)", mapOpen))
                mapOpen = !mapOpen;

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            var help = "ЛКМ — строить/выбрать · ПКМ/Esc — отмена · R — поворот · WASD, Q/E, колесо, СКМ — камера · Tab — экономика";
            GUI.Label(new Rect(8f, Screen.height - ToolbarHeight - 22f, Screen.width, 20f), help);
        }

        bool ToolButton(string text, bool active)
        {
            GUI.backgroundColor = active ? ActiveTool : Color.white;
            return GUILayout.Button(text, toolStyle, GUILayout.Height(ToolbarHeight - 10f), GUILayout.MinWidth(80f));
        }

        static void DrawSwatch(Rect button, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(button.x + 4f, button.y + 3f, button.width - 8f, 5f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void DrawLog()
        {
            var log = runner.Log;
            if (log.Count == 0) return;
            const float width = 340f;
            var count = Mathf.Min(LogLines, log.Count);
            var texts = new string[count];
            var heights = new float[count];
            var total = 12f;
            for (var i = 0; i < count; i++)
            {
                var entry = log[log.Count - 1 - i];
                texts[i] = $"День {entry.Day}: {entry.Text}";
                heights[i] = smallLabel.CalcHeight(new GUIContent(texts[i]), width - 16f);
                total += heights[i] + 2f;
            }

            var rect = Panel(new Rect(Screen.width - width - 10f, TopBarHeight + 8f, width, total));
            var y = rect.y + 6f;
            for (var i = 0; i < count; i++)
            {
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, heights[i]), texts[i], smallLabel);
                y += heights[i] + 2f;
            }
        }

        void DrawSelection(World world)
        {
            var building = world.GetBuilding(tool.SelectedId);
            if (building == null) return;
            if (building.Kind == BuildingKind.Guild && building.IsComplete)
            {
                DrawGuild(world);
                return;
            }
            if (building.Kind == BuildingKind.Forge && building.IsComplete)
            {
                DrawForge(world, building);
                return;
            }
            if (building.Kind == BuildingKind.Watchtower && building.IsComplete)
            {
                DrawWatchtower(world, building);
                return;
            }

            var def = world.Def(building);
            var lines = new List<string> { def.Name };
            if (!building.IsComplete)
            {
                var built = 1f - building.ConstructionLeft / (float)def.BuildTicks;
                lines.Add($"Строится: {Mathf.RoundToInt(built * 100f)}%");
            }
            else if (building.Kind == BuildingKind.House)
            {
                var level = world.HouseLevel(building);
                lines.Add($"{level.Name}, жильцов {building.Residents}/{level.Capacity}");
                lines.Add("Еда: " + (building.FoodMet ? "есть" : "не хватает") + (level.NeedsBread ? " (нужен хлеб)" : ""));
                if (level.NeedsFirewood) lines.Add("Дрова: " + (building.FirewoodMet ? "есть" : "не хватает"));
                lines.Add($"Налог: {level.TaxPerResident} {ResourceInfo.Name(ResourceType.Gold)} с жильца в день");
                if (building.Level < world.Content.HouseLevels.Count)
                    lines.Add($"Улучшение: полный дом 3 сытых дня, {level.UpgradePlanks} досок" +
                              (level.UpgradeOre > 0 ? $", {level.UpgradeOre} руды" : ""));
                var food = level.NeedsBread ? "хлеба" : "зерна (хлеба, если зерна нет)";
                lines.Add($"Потребляет: {building.Residents * SimConstants.FoodPerResident} {food} в день" +
                          (level.NeedsFirewood ? $", {SimConstants.FirewoodPerHouse} {ResourceInfo.Name(ResourceType.Logs).ToLower()} на дрова" : ""));
                AddPeople(world, lines, building.Id, true);
            }
            else if (def.Recipe != null)
            {
                lines.Add($"Рабочие: {building.Workers}/{def.Workers}");
                lines.Add($"Логистика: {Mathf.RoundToInt(building.Efficiency * 100f)}%");
                var input = def.Recipe.Inputs.Length > 0 ? ResourceInfo.Format(def.Recipe.Inputs) + " → " : "";
                lines.Add($"Цикл: {input}{ResourceInfo.Format(def.Recipe.Outputs)} за {def.Recipe.CycleTicks} с");
                var cycles = Economy.CyclesPerDay(world, building);
                var perDay = "Сейчас в день: " + PerDay(def.Recipe.Outputs, cycles, "+");
                if (def.Recipe.Inputs.Length > 0) perDay += ", " + PerDay(def.Recipe.Inputs, cycles, "−");
                lines.Add(perDay);
                if (building.Paused) lines.Add("На паузе");
                else if (building.LimitReached) lines.Add("Стоит: на складе достигнут лимит запаса");
                else if (building.Stalled) lines.Add("Ждёт сырьё");
                AddPeople(world, lines, building.Id, false);
            }
            else if (building.Kind == BuildingKind.Storehouse)
            {
                lines.Add($"Радиус обслуживания: {def.StorageRadius} клеток дороги");
            }

            if (building.IsComplete && !building.IsConnected) lines.Add("Нет дороги к складу");

            var canPause = building.IsComplete && def.Recipe != null;
            var rect = Panel(new Rect(10f, TopBarHeight + 8f, 380f, 12f + lines.Count * 20f + (canPause ? 30f : 0f)));
            for (var i = 0; i < lines.Count; i++)
                GUI.Label(new Rect(rect.x + 8f, rect.y + 6f + i * 20f, rect.width - 16f, 20f), lines[i]);
            if (canPause &&
                GUI.Button(new Rect(rect.x + 8f, rect.y + 8f + lines.Count * 20f, 120f, 24f), building.Paused ? "Продолжить" : "Пауза"))
                world.Enqueue(new TogglePauseCommand(building.Id));
            if (canPause &&
                GUI.Button(new Rect(rect.x + 136f, rect.y + 8f + lines.Count * 20f, 220f, 24f),
                    building.Priority ? "Приоритет найма: вкл" : "Приоритет найма: выкл"))
                world.Enqueue(new TogglePriorityCommand(building.Id));
        }

        static string PerDay(ResourceAmount[] amounts, float cycles, string sign)
        {
            var parts = new List<string>();
            foreach (var amount in amounts)
                parts.Add($"{sign}{cycles * amount.Amount:0.#} {ResourceInfo.Name(amount.Type).ToLower()}");
            return string.Join(", ", parts);
        }

        void DrawEconomy(World world)
        {
            const float width = 660f;
            const float rowHeight = 48f;
            var resourceFlows = Flows(world);
            var rect = Panel(new Rect((Screen.width - width) * 0.5f, TopBarHeight + 8f, width, 44f + ResourceInfo.Count * rowHeight));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Ресурс</b>", richLabel, GUILayout.Width(80f));
            GUILayout.Label("<b>Запас</b>", richLabel, GUILayout.Width(55f));
            GUILayout.Label("<b>Приход</b>", richLabel, GUILayout.Width(60f));
            GUILayout.Label("<b>Расход</b>", richLabel, GUILayout.Width(60f));
            GUILayout.Label("<b>Итог/день</b>", richLabel, GUILayout.Width(75f));
            GUILayout.Label("<b>Хватит на</b>", richLabel, GUILayout.Width(95f));
            GUILayout.Label("<b>Лимит запаса</b>", richLabel);
            GUILayout.EndHorizontal();

            for (var i = 0; i < ResourceInfo.Count; i++)
            {
                var type = (ResourceType)i;
                var flow = resourceFlows[i];
                var stock = world.Stock[type];

                GUILayout.BeginHorizontal();
                GUILayout.Label(ResourceInfo.Name(type), GUILayout.Width(80f));
                GUILayout.Label(stock.ToString(), GUILayout.Width(55f));
                GUILayout.Label($"+{flow.Produced:0}", GUILayout.Width(60f));
                GUILayout.Label($"−{flow.Consumed:0}", GUILayout.Width(60f));
                GUILayout.Label(Signed(flow.Net, true), richLabel, GUILayout.Width(75f));
                var days = Economy.DaysLeft(stock, flow);
                var daysText = float.IsPositiveInfinity(days) ? "—" : days > 99f ? "> 99 дн." : days < SimConstants.ShortageWarningDays
                    ? $"<color=#FF8A80>{days:0.0} дн.</color>"
                    : $"{days:0.0} дн.";
                GUILayout.Label(daysText, richLabel, GUILayout.Width(95f));

                if (type != ResourceType.Gold)
                {
                    var limit = world.StockLimit(type);
                    if (GUILayout.Button("−", GUILayout.Width(24f)) && limit > 0)
                        world.Enqueue(new SetStockLimitCommand(type, limit - SimConstants.StockLimitStep));
                    GUILayout.Label(limit == 0 ? "без лимита" : limit.ToString(), GUILayout.Width(75f));
                    if (GUILayout.Button("+", GUILayout.Width(24f)))
                        world.Enqueue(new SetStockLimitCommand(type, limit + SimConstants.StockLimitStep));
                }
                GUILayout.EndHorizontal();

                var stockLimit = type == ResourceType.Gold ? 0 : world.StockLimit(type);
                if (stockLimit > 0 && stockLimit < flow.Consumed)
                    GUILayout.Label($"<color=#FF8A80>Лимит {stockLimit} ниже суточного расхода ({flow.Consumed:0}) — в полночь ресурса не хватит</color>", smallLabel);
                GUILayout.Label(Breakdown(flow), smallLabel);
            }

            GUILayout.EndArea();
        }

        static string Breakdown(ResourceFlow flow)
        {
            var parts = new List<string>();
            foreach (var entry in flow.Sources)
                parts.Add($"<color=#8BE28B>{Entry(entry)} +{entry.PerDay:0.#}</color>");
            foreach (var entry in flow.Sinks)
                parts.Add($"<color=#FF8A80>{Entry(entry)} −{entry.PerDay:0.#}</color>");
            return parts.Count == 0 ? "<color=#AAAAAA>нет ни производства, ни расхода</color>" : string.Join("  ·  ", parts);
        }

        static string Entry(FlowEntry entry) => entry.Count > 1 ? $"{entry.Label} ×{entry.Count}" : entry.Label;

        void DrawGuild(World world)
        {
            const float width = 480f;
            var heroes = new List<Citizen>();
            foreach (var citizen in world.Citizens)
                if (citizen.IsHero)
                    heroes.Add(citizen);
            var expeditions = world.Expeditions;
            var compact = expeditions.Count > 1;

            partySelection.RemoveWhere(id =>
            {
                var index = world.CitizenIndex(id);
                return index < 0 || world.Citizens[index].OnExpedition;
            });

            var openDungeons = 0;
            foreach (var dungeon in Dungeons.All)
                if (world.IsDungeonOpen(dungeon.Id))
                    openDungeons++;
            var height = 206f + heroes.Count * 24f + (openDungeons - 1) / 3 * 24f;
            foreach (var active in expeditions) height += 30f + ExpeditionDetailsHeight(active, compact);
            var rect = Panel(new Rect(10f, TopBarHeight + 8f, width, height));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));

            GUILayout.Label("<b>Гильдия</b>", richLabel);
            GUILayout.Label($"Героев: {heroes.Count}. Каждый в день: {SimConstants.HeroWageGold} зол. жалованья и " +
                            $"{SimConstants.HeroBreadPerDay} хлеб пайка. Провизия на поход: {SimConstants.ProvisionsPerHero} хлеба на героя.", smallLabel);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Нанять ({SimConstants.HeroHireGold} зол., {SimConstants.HeroHirePlanks} досок):", GUILayout.Width(170f));
            foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
                if (GUILayout.Button(CombatData.Get(heroClass).Name, GUILayout.Width(85f)))
                    world.Enqueue(new HireHeroCommand(heroClass));
            GUILayout.EndHorizontal();

            if (heroes.Count == 0)
                GUILayout.Label("Героев пока нет. Нанятый житель уходит с работы и тренируется в гильдии.", smallLabel);

            foreach (var hero in heroes)
            {
                GUILayout.BeginHorizontal();
                if (!hero.OnExpedition)
                {
                    var selected = partySelection.Contains(hero.Id);
                    if (GUILayout.Toggle(selected, GUIContent.none, GUILayout.Width(18f)) != selected)
                    {
                        if (selected) partySelection.Remove(hero.Id);
                        else if (partySelection.Count < SimConstants.MaxPartySize) partySelection.Add(hero.Id);
                    }
                }
                else
                {
                    GUILayout.Space(22f);
                }

                var next = World.XpToNextLevel(hero.HeroLevel);
                var xp = hero.HeroLevel >= CombatData.MaxLevel ? "макс." : $"{hero.HeroXp}/{next} оп.";
                GUILayout.Label($"{CitizenInfo.FullName(hero)} — {CombatData.Get(hero.Class).Name}, {hero.HeroLevel} ур. ({xp})", GUILayout.Width(270f));
                GUILayout.Label($"<color=#B0BEC5>{GearShort(hero.WeaponTier)}/{GearShort(hero.ArmorTier)}</color>", richLabel, GUILayout.Width(40f));
                var status = hero.OnExpedition ? "в походе"
                    : hero.Hungry ? "<color=#FF8A80>голодает</color>"
                    : hero.InjuryDays > 0 ? $"<color=#FFB74D>ранен(а) {hero.InjuryDays} дн., слабее</color>"
                    : "<color=#8BE28B>готов(а)</color>";
                GUILayout.Label(status, richLabel);
                GUILayout.EndHorizontal();
            }

            partyIds.Clear();
            partyIds.AddRange(partySelection);
            GUILayout.BeginHorizontal();
            var inRow = 0;
            foreach (var dungeon in Dungeons.All)
            {
                if (!world.IsDungeonOpen(dungeon.Id)) continue;
                if (inRow == 3)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    inRow = 0;
                }
                GUI.backgroundColor = selectedDungeon == dungeon.Id ? ActiveTool : Color.white;
                if (GUILayout.Button(dungeon.Name, GUILayout.Width(146f))) selectedDungeon = dungeon.Id;
                inRow++;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var heroicUnlocked = world.IsHeroicUnlocked(selectedDungeon);
            if (!heroicUnlocked) selectedHeroic = false;
            GUI.enabled = heroicUnlocked;
            selectedHeroic = GUILayout.Toggle(selectedHeroic, "героический");
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            var target = Dungeons.Get(selectedDungeon);
            var canSend = world.CanSendExpedition(partyIds, selectedDungeon, selectedHeroic, out var reason);
            GUI.enabled = canSend;
            if (GUILayout.Button($"Отправить: «{target.Name}»{(selectedHeroic ? " (героич.)" : "")} — {partyIds.Count} гер., " +
                                 $"{World.Provisions(partyIds.Count, selectedDungeon)} хлеба"))
            {
                world.Enqueue(new SendExpeditionCommand(partyIds, selectedDungeon, selectedHeroic));
                partySelection.Clear();
            }
            GUI.enabled = true;
            if (!canSend && reason != null)
                GUILayout.Label($"<color=#AAAAAA>{(partyIds.Count == 0 ? "Отметьте до 3 героев галочками" : reason)}</color>", smallLabel);

            for (var i = 0; i < expeditions.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Отряд {i + 1}: {expeditions[i].Run.Dungeon.Name}</b>", richLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Смотреть бой", GUILayout.Width(110f))) runner.WatchExpedition(expeditions[i].Record.Id);
                GUILayout.EndHorizontal();
                DrawExpeditionDetails(expeditions[i], compact);
            }

            GUILayout.EndArea();
        }

        void DrawMap(World world)
        {
            const float width = 700f, height = 470f;
            var rect = Panel(new Rect((Screen.width - width) * 0.5f, TopBarHeight + 30f, width, height));
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, 400f, 22f), "<b>Карта региона</b>", richLabel);
            if (GUI.Button(new Rect(rect.xMax - 110f, rect.y + 8f, 100f, 22f), "Закрыть (M)")) mapOpen = false;

            var center = new Vector2(rect.x + width * 0.5f, rect.y + height * 0.5f + 16f);
            DrawBar(new Rect(center.x - 50f, center.y - 18f, 100f, 36f), 1f, new Color(0.22f, 0.42f, 0.86f));
            GUI.Label(new Rect(center.x - 50f, center.y - 18f, 100f, 36f), "<b>Город</b>", new GUIStyle(richLabel) { alignment = TextAnchor.MiddleCenter });

            foreach (var def in Region.Sites)
            {
                var site = world.FindSite(def.Id);
                if (site == null) continue;
                var pos = new Vector2(center.x + def.MapX * 250f, center.y - def.MapY * 165f);
                var box = new Rect(pos.x - 95f, pos.y - 50f, 190f, 100f);
                DrawBar(box, 1f, site.Discovered
                    ? def.Kind == SiteKind.Dungeon ? new Color(0.25f, 0.22f, 0.3f) : new Color(0.35f, 0.18f, 0.16f)
                    : new Color(0.18f, 0.18f, 0.2f));
                GUILayout.BeginArea(new Rect(box.x + 6f, box.y + 4f, box.width - 12f, box.height - 8f));
                if (!site.Discovered)
                {
                    GUILayout.Label($"<b>Неизведано</b> · {def.Distance} дн. пути", richLabel);
                    var scout = world.ScoutFor(def.Id);
                    if (scout != null)
                    {
                        GUILayout.Label($"<color=#BBBBBB>Разведчик в пути: {scout.TicksLeft} с</color>", smallLabel);
                    }
                    else
                    {
                        var canScout = world.CanScout(def.Id, out var reason);
                        GUI.enabled = canScout;
                        if (GUILayout.Button("Разведать (1 герой)")) world.Enqueue(new ScoutCommand(def.Id));
                        GUI.enabled = true;
                        if (!canScout) GUILayout.Label($"<color=#AAAAAA>{reason}</color>", smallLabel);
                    }
                }
                else
                {
                    var dungeon = Dungeons.Get(def.DungeonId);
                    GUILayout.Label($"<b>{def.Name}</b>", richLabel);
                    GUILayout.Label($"Добыча: {ResourceInfo.Name(dungeon.LootType).ToLower()} · {def.Distance} дн. пути · зачищен {site.TimesCleared} раз", smallLabel);
                    var tensionRect = GUILayoutUtility.GetRect(170f, 10f);
                    DrawBar(new Rect(tensionRect.x, tensionRect.y + 2f, tensionRect.width, 8f), site.Tension / Region.TensionMax, new Color(0.9f, 0.45f, 0.2f));
                    var raid = world.RaidFrom(def.Id);
                    var state = raid != null ? $"<color=#FF8A80>{(def.Kind == SiteKind.Dungeon ? "Вспышка" : "Набег")}: в городе через {raid.TicksLeft} с</color>"
                        : $"<color=#BBBBBB>{(def.Kind == SiteKind.Dungeon ? "Напряжение" : "Угроза набега")} {site.Tension:0}%{(world.IsHeroicUnlocked(def.DungeonId) ? " · есть героич." : "")}</color>";
                    GUILayout.Label(state, smallLabel);
                }
                GUILayout.EndArea();
            }
        }

        void DrawRaidWarning(World world)
        {
            if (world.Raids.Count == 0) return;
            const float width = 460f;
            world.CountDefenders(out var heroes, out var guards);
            var height = 30f + world.Raids.Count * 20f;
            var rect = Panel(new Rect((Screen.width - width) * 0.5f, Screen.height - ToolbarHeight - 28f - height, width, height));
            var y = rect.y + 5f;
            foreach (var raid in world.Raids)
            {
                var def = Region.Get(raid.SiteId);
                var site = world.FindSite(raid.SiteId);
                var from = site != null && site.Discovered ? $"«{def.Name}»" : "неизведанные земли";
                var kind = def.Kind == SiteKind.Dungeon ? "Вспышка" : "Набег";
                GUI.Label(new Rect(rect.x + 10f, y, width - 20f, 20f), $"<color=#FF8A80><b>{kind}</b></color>: {from} — враги в городе через {raid.TicksLeft} с", richLabel);
                y += 20f;
            }
            GUI.Label(new Rect(rect.x + 10f, y, width - 20f, 20f),
                $"<color=#BBBBBB>Защитники в городе: героев {heroes}, стражников {guards}. Если не выстоят — разграбят 20% запасов.</color>", smallLabel);
        }

        void DrawWatchtower(World world, Building tower)
        {
            var lines = new List<string>();
            AddPeople(world, lines, tower.Id, false);
            world.CountDefenders(out var heroes, out var guards);
            var rect = Panel(new Rect(10f, TopBarHeight + 8f, 380f, 104f + lines.Count * 20f));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));
            GUILayout.Label("<b>Сторожевая башня</b>", richLabel);
            GUILayout.Label($"Стражники: {tower.Workers}/{world.Def(tower).Workers}. Каждый стражник выходит на защиту города при набеге. " +
                            $"Сейчас в городе героев {heroes}, стражников {guards}.", smallLabel);
            foreach (var line in lines) GUILayout.Label(line);
            if (tower.IsComplete && !tower.IsConnected) GUILayout.Label("Нет дороги к складу");
            if (GUILayout.Button(tower.Priority ? "Приоритет найма: вкл" : "Приоритет найма: выкл", GUILayout.Width(220f)))
                world.Enqueue(new TogglePriorityCommand(tower.Id));
            GUILayout.EndArea();
        }

        static string GearShort(int tier) => tier == 0 ? "—" : tier == 1 ? "Ж" : "С";

        void DrawForge(World world, Building forge)
        {
            const float width = 700f;
            var heroes = new List<Citizen>();
            foreach (var citizen in world.Citizens)
                if (citizen.IsHero)
                    heroes.Add(citizen);
            var orders = world.ForgeOrders;

            var height = 130f + heroes.Count * 38f + orders.Count * 20f;
            var rect = Panel(new Rect(10f, TopBarHeight + 8f, width, height));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));

            GUILayout.Label("<b>Кузница</b>", richLabel);
            GUILayout.Label($"Рабочие: {forge.Workers}/{world.Def(forge).Workers}. Заказы куются по очереди; каждая работающая кузница ведёт свой заказ. " +
                            "Железо: +1 к оружию, +3 к лечению жреца, +3 брони; сталь — вдвое больше.", smallLabel);
            if (orders.Count > 0 && forge.Workers == 0)
                GUILayout.Label("<color=#FF8A80>Нет рабочих: в городе не осталось взрослых, которых можно перевести в кузницу</color>", smallLabel);
            else if (orders.Count > 0)
                GUILayout.Label("<color=#AAAAAA>Пока есть заказы, кузница забирает рабочих у обычных зданий первой</color>", smallLabel);
            if (heroes.Count == 0) GUILayout.Label("Героев нет — нанять их можно в гильдии.", smallLabel);

            foreach (var hero in heroes)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{CitizenInfo.FullName(hero)} — {CombatData.Get(hero.Class).Name}, {hero.HeroLevel} ур.", GUILayout.Width(230f));
                GearButton(world, hero, GearSlot.Weapon);
                GearButton(world, hero, GearSlot.Armor);
                GUILayout.EndHorizontal();
            }

            if (orders.Count > 0) GUILayout.Label("<b>Очередь</b>", richLabel);
            foreach (var order in orders)
            {
                var index = world.CitizenIndex(order.HeroId);
                var owner = index >= 0 ? CitizenInfo.FullName(world.Citizens[index]) : "?";
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{GearData.Name(order.Slot, order.Tier)} — {owner}", smallLabel, GUILayout.Width(300f));
                var bar = GUILayoutUtility.GetRect(160f, 12f, GUILayout.Width(160f));
                DrawBar(new Rect(bar.x, bar.y + 4f, bar.width, 10f), order.Progress / World.ForgeTicks(order), new Color(0.9f, 0.6f, 0.25f));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        void GearButton(World world, in Citizen hero, GearSlot slot)
        {
            var tier = slot == GearSlot.Weapon ? hero.WeaponTier : hero.ArmorTier;
            var current = GearData.Name(slot, tier);
            var pending = world.PendingOrder(hero.Id, slot);
            string label;
            var canOrder = false;
            string reason = null;
            if (pending != null)
            {
                label = $"{current}: куётся {Mathf.RoundToInt(100f * pending.Progress / World.ForgeTicks(pending))}%";
            }
            else if (tier >= GearData.MaxTier)
            {
                label = current;
            }
            else
            {
                canOrder = world.CanOrderGear(hero.Id, slot, out var next, out reason);
                var cost = GearData.Cost(slot, next);
                label = $"{current} → {GearData.Tier(next).WeaponName}: {ResourceInfo.Format(cost)}";
                if (slot == GearSlot.Armor) label = $"{current} → {GearData.Tier(next).ArmorName}: {ResourceInfo.Format(cost)}";
            }

            GUI.enabled = canOrder;
            if (GUILayout.Button(new GUIContent(label, reason), toolStyle, GUILayout.Width(220f), GUILayout.Height(34f)))
                world.Enqueue(new OrderGearCommand(hero.Id, slot));
            GUI.enabled = true;
        }

        void DrawExpeditionWidget(World world)
        {
            var expeditions = world.Expeditions;
            if (expeditions.Count == 0) return;
            var selected = world.GetBuilding(tool.SelectedId);
            if (selected != null && selected.Kind == BuildingKind.Guild) return;

            const float width = 380f;
            var height = 40f;
            foreach (var active in expeditions) height += 28f + ExpeditionDetailsHeight(active, true);
            var rect = Panel(new Rect(10f, Screen.height - ToolbarHeight - 30f - height, width, height));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Отрядов в походе: {expeditions.Count}</b>", richLabel);
            GUILayout.FlexibleSpace();
            var guild = world.FindGuild();
            if (guild != null && GUILayout.Button("Гильдия", GUILayout.Width(80f))) tool.SelectedId = guild.Id;
            GUILayout.EndHorizontal();
            for (var i = 0; i < expeditions.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(expeditions.Count > 1 ? $"<b>Отряд {i + 1}</b>" : "", richLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Смотреть бой", GUILayout.Width(110f))) runner.WatchExpedition(expeditions[i].Record.Id);
                GUILayout.EndHorizontal();
                DrawExpeditionDetails(expeditions[i], true);
            }
            GUILayout.EndArea();
        }

        static float ExpeditionDetailsHeight(ActiveExpedition active, bool compact)
        {
            var run = active.Run;
            var enemies = run.Battle != null && !compact ? run.Battle.Enemies.Count : 0;
            return 26f + (run.Party.Count + enemies) * 20f + Mathf.Min(compact ? 3 : 6, active.Log.Count) * 17f + 10f;
        }

        void DrawExpeditionDetails(ActiveExpedition active, bool compact)
        {
            var run = active.Run;
            GUILayout.Label($"{PhaseText(run)} · {run.LootSummary}", richLabel);

            foreach (var hero in run.Party)
                DrawUnit(hero, hero.Dead ? "погиб(ла)" : hero.Downed ? "при смерти" : null);
            if (!compact && run.Battle != null)
                foreach (var enemy in run.Battle.Enemies)
                    DrawUnit(enemy, enemy.Dead ? "повержен" : null);

            var lines = compact ? 3 : 6;
            for (var i = Mathf.Max(0, active.Log.Count - lines); i < active.Log.Count; i++)
                GUILayout.Label($"<color=#BBBBBB>{active.Log[i]}</color>", smallLabel);
        }

        void DrawUnit(Combatant unit, string state)
        {
            GUILayout.BeginHorizontal();
            var label = unit.IsHero ? $"{unit.Name} ({CombatData.Get(unit.Class).Name})" : unit.Name;
            GUILayout.Label(label, smallLabel, GUILayout.Width(200f));
            var bar = GUILayoutUtility.GetRect(140f, 12f, GUILayout.Width(140f));
            var color = !unit.IsHero ? new Color(0.75f, 0.3f, 0.3f)
                : unit.HpRatio > 0.5f ? new Color(0.35f, 0.75f, 0.35f)
                : unit.HpRatio > 0.25f ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.9f, 0.3f, 0.25f);
            DrawBar(new Rect(bar.x, bar.y + 4f, bar.width, 10f), unit.Dead ? 0f : unit.HpRatio, color);
            GUILayout.Label(state ?? $"{Mathf.CeilToInt(unit.Hp)}/{Mathf.CeilToInt(unit.MaxHp)}", smallLabel);
            GUILayout.EndHorizontal();
        }

        static void DrawBar(Rect rect, float ratio, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static string PhaseText(Expedition run)
        {
            switch (run.Phase)
            {
                case ExpeditionPhase.TravelOut: return "В пути к данжу";
                case ExpeditionPhase.Room:
                    var room = $"Комната {run.RoomIndex + 1}/{run.Rooms.Count}: {run.Rooms[run.RoomIndex].Name}";
                    return run.Battle != null ? room + " — <color=#FF8A80>бой</color>" : room;
                case ExpeditionPhase.Rest: return "Привал между комнатами";
                case ExpeditionPhase.TravelBack: return run.Cleared ? "Возвращается с победой" : "Возвращается в город";
                default: return "Вернулся";
            }
        }

        static void AddPeople(World world, List<string> lines, int buildingId, bool residents)
        {
            const int maxShown = 8;
            var shown = 0;
            var total = 0;
            foreach (var citizen in world.Citizens)
            {
                if ((residents ? citizen.HomeId : citizen.WorkId) != buildingId) continue;
                total++;
                if (shown == maxShown) continue;
                shown++;

                var role = residents ? JobOf(world, citizen) : "";
                lines.Add($"· {CitizenInfo.FullName(citizen)}, {world.AgeOf(citizen)} — {role}{CitizenInfo.ActivityName(world.ActivityOf(citizen)).ToLower()}");
            }
            if (total > shown) lines.Add($"· и ещё {total - shown}");
        }

        static string JobOf(World world, in Citizen citizen)
        {
            if (!world.IsAdult(citizen)) return "ребёнок, ";
            var work = world.GetBuilding(citizen.WorkId);
            return work == null ? "без работы, " : world.Def(work).Name.ToLower() + ", ";
        }

        void DrawHover()
        {
            if (string.IsNullOrEmpty(tool.HoverMessage)) return;
            var mouse = Event.current.mousePosition;
            var size = GUI.skin.box.CalcSize(new GUIContent(tool.HoverMessage));
            GUI.Box(new Rect(mouse.x + 18f, mouse.y + 18f, size.x + 12f, size.y + 4f), tool.HoverMessage);
        }
    }
}
