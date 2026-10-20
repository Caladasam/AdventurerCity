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
        int selectedHeroId;
        int heroTab;
        ItemSlot stashSlot;
        Vector2 stashScroll;
        GUIStyle leftButton;
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
                leftButton = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = true, fontSize = 12 };
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
                    richLabel, GUILayout.Width(98f));
            }
            var minutes = Mathf.FloorToInt(world.DayProgress * 24f * 60f);
            GUILayout.Label($"День {world.Day}, {minutes / 60:00}:{minutes % 60:00}", GUILayout.Width(105f));
            GUILayout.Label($"Жители: {world.Population} (раб. {world.EmployedWorkers}/{world.Adults})", GUILayout.Width(150f));
            GUILayout.FlexibleSpace();
            for (var i = 0; i < runner.Speeds.Count; i++)
            {
                GUI.backgroundColor = i == runner.SpeedIndex ? ActiveTool : Color.white;
                if (GUILayout.Button($"x{runner.Speeds[i]}", GUILayout.Width(34f))) runner.SetSpeed(i);
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button(GameAudio.Muted ? "Тихо" : "Звук", GUILayout.Width(50f))) GameAudio.Muted = !GameAudio.Muted;
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

            var help = "ЛКМ — строить/выбрать · ПКМ/Esc — отмена · R — поворот · WASD, колесо, СКМ — камера · Tab — экономика · M — карта";
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
                DrawHeroPanel(world);
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
            if (building.Kind == BuildingKind.Temple && building.IsComplete)
            {
                DrawTemple(world, building);
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
            const float width = 520f;
            var heroes = new List<Citizen>();
            foreach (var citizen in world.Citizens)
                if (citizen.IsHero)
                    heroes.Add(citizen);
            var expeditions = world.Expeditions;
            var compact = expeditions.Count > 1;

            partySelection.RemoveWhere(id =>
            {
                var index = world.CitizenIndex(id);
                return index < 0 || !World.IsAvailable(world.Citizens[index]);
            });

            var openDungeons = 0;
            foreach (var dungeon in Dungeons.All)
                if (world.IsDungeonOpen(dungeon.Id))
                    openDungeons++;
            var height = 290f + heroes.Count * 24f + (openDungeons - 1) / 3 * 24f;
            foreach (var active in expeditions) height += 30f + ExpeditionDetailsHeight(active, compact);
            var rect = Panel(new Rect(10f, TopBarHeight + 8f, width, height));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));

            GUILayout.Label("<b>Гильдия</b>", richLabel);
            GUILayout.Label($"Героев: {heroes.Count}. Каждый в день: {SimConstants.HeroWageGold} зол. жалованья и " +
                            $"{SimConstants.HeroBreadPerDay} хлеб пайка. Провизия на поход: {SimConstants.ProvisionsPerHero} хлеба на героя.", smallLabel);

            GUILayout.Label($"Нанять ({SimConstants.HeroHireGold} зол., {SimConstants.HeroHirePlanks} досок). Класс задаёт роль и 3 базовых навыка:", smallLabel);
            for (var i = 0; i < Classes.All.Count; i++)
            {
                if (i % 4 == 0) GUILayout.BeginHorizontal();
                var def = Classes.All[i];
                if (GUILayout.Button(new GUIContent($"{def.Name} · {RoleShort(def.Role)}", def.Description), toolStyle, GUILayout.Width(121f), GUILayout.Height(22f)))
                    world.Enqueue(new HireHeroCommand(def.Class));
                if (i % 4 == 3 || i == Classes.All.Count - 1) GUILayout.EndHorizontal();
            }

            if (heroes.Count == 0)
                GUILayout.Label("Героев пока нет. Нанятый житель уходит с работы и тренируется в гильдии.", smallLabel);

            foreach (var hero in heroes)
            {
                GUILayout.BeginHorizontal();
                if (World.IsAvailable(hero))
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
                GUI.backgroundColor = selectedHeroId == hero.Id ? ActiveTool : Color.white;
                if (GUILayout.Button($"{CitizenInfo.FullName(hero)} — {ClassLine(hero)}, {hero.HeroLevel} ур. ({xp})", leftButton, GUILayout.Width(300f)))
                    selectedHeroId = selectedHeroId == hero.Id ? 0 : hero.Id;
                GUI.backgroundColor = Color.white;
                GUILayout.Label(GearMarks(world, hero.Id), richLabel, GUILayout.Width(40f));
                var status = hero.OnExpedition ? "в походе"
                    : World.IsTraining(hero) ? $"<color=#90CAF9>учится {hero.TrainingDays} дн.</color>"
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
                if (GUILayout.Button(dungeon.Name, toolStyle, GUILayout.Width(146f), GUILayout.Height(24f))) selectedDungeon = dungeon.Id;
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
                GUILayout.Label($"<color=#AAAAAA>{(partyIds.Count == 0 ? $"Отметьте до {SimConstants.MaxPartySize} героев галочками" : reason)}</color>", smallLabel);

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
                    var state = raid != null ? $"<color=#FF8A80>{(def.Kind == SiteKind.Dungeon ? "Вспышка" : "Набег")}: {(raid.Fighting ? "бой у ворот" : $"в городе через {raid.TicksLeft} с")}</color>"
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
                if (raid.Fighting)
                {
                    GUI.Label(new Rect(rect.x + 10f, y, width - 140f, 20f), $"<color=#FF8A80><b>{kind}</b></color>: {from} — бой у ворот!", richLabel);
                    if (GUI.Button(new Rect(rect.xMax - 124f, y, 114f, 19f), "Смотреть бой")) runner.WatchRaid(raid.SiteId);
                }
                else
                {
                    var when = raid.TicksLeft > 0 ? $"враги в городе через {raid.TicksLeft} с" : "враги у ворот, ждут конца боя";
                    GUI.Label(new Rect(rect.x + 10f, y, width - 20f, 20f), $"<color=#FF8A80><b>{kind}</b></color>: {from} — {when}", richLabel);
                }
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

        static string RoleShort(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Tank: return "танк";
                case CombatRole.Healer: return "лекарь";
                case CombatRole.Support: return "поддерж.";
                case CombatRole.Melee: return "ближн.";
                default: return "дальн.";
            }
        }

        static string ClassLine(in Citizen hero)
        {
            var line = Classes.Get(hero.Class).Name;
            foreach (var def in Classes.All)
                if (def.Class != hero.Class && Classes.HasClass(hero.ClassMask, def.Class))
                    line += "/" + def.Name;
            return line;
        }

        void DrawHeroPanel(World world)
        {
            var index = selectedHeroId != 0 ? world.CitizenIndex(selectedHeroId) : -1;
            if (index < 0) return;
            var hero = world.Citizens[index];
            var primary = Classes.Get(hero.Class);
            var abilities = World.HeroAbilities(hero);
            var options = World.SlotOptions(hero);
            var title = World.HeroTitle(hero);

            const float width = 440f;
            var height = heroTab == 2 ? 470f : heroTab == 1 ? 560f : 214f + abilities.Count * 34f + (options.Count > 0 ? 60f : 0f) + 4 * 24f + 50f;
            var rect = Panel(new Rect(540f, TopBarHeight + 8f, width, height));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{CitizenInfo.FullName(hero)}</b> — {ClassLine(hero)}, {hero.HeroLevel} ур.", richLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", GUILayout.Width(24f))) selectedHeroId = 0;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var tabs = new[] { "Классы и навыки", "Снаряжение", "Личность" };
            for (var t = 0; t < tabs.Length; t++)
            {
                GUI.backgroundColor = heroTab == t ? ActiveTool : Color.white;
                if (GUILayout.Button(tabs[t], GUILayout.Width(132f))) heroTab = t;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            if (heroTab == 2)
            {
                DrawPersonality(world, hero);
                GUILayout.EndArea();
                return;
            }
            if (heroTab == 1)
            {
                DrawGear(world, hero);
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label($"Роль в бою: {Classes.RoleName(primary.Role)}. {primary.Description}", smallLabel);
            GUILayout.Label(title != null
                ? $"Титул: <b>«{title.Name}»</b> — {title.BonusText}"
                : "<color=#AAAAAA>Титула нет: некоторые сочетания классов дают скрытый титул с бонусом</color>", smallLabel);

            GUILayout.Label("<b>Навыки</b> (герой применяет их сам, по порядку приоритета):", richLabel);
            foreach (var ability in abilities)
            {
                var def = Classes.Get(ability);
                var from = def.Class == hero.Class ? "" : $" <color=#90CAF9>[{Classes.Get(def.Class).Name}]</color>";
                var cost = (def.Mana > 0f ? $"{def.Mana:0} маны · " : "") + (def.Cooldown > 0f ? $"раз в {def.Cooldown:0} с" : "без перезарядки");
                GUILayout.Label($"<b>{def.Name}</b>{from} — {def.Text}. <color=#AAAAAA>{cost}</color>", smallLabel);
            }

            if (options.Count > 0)
            {
                GUILayout.Label($"<b>Свободные слоты</b> ({Classes.FreeSlots}): клик — следующий навык из изученных классов", richLabel);
                GUILayout.BeginHorizontal();
                SlotButton(world, hero, 0, hero.SlotA, options);
                SlotButton(world, hero, 1, hero.SlotB, options);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label($"<b>Учить класс</b> — {Classes.TrainingGold} зол., {Classes.TrainingDays} дн. без походов. Второй класс с {Classes.SecondClassLevel}-го уровня, третий с {Classes.ThirdClassLevel}-го.", smallLabel);
            if (World.IsTraining(hero))
            {
                GUILayout.Label($"<color=#90CAF9>Учится: {Classes.Get((HeroClass)(hero.TrainingClass - 1)).Name}, осталось {hero.TrainingDays} дн.</color>", richLabel);
            }
            else
            {
                string reason = null;
                for (var i = 0; i < Classes.All.Count; i++)
                {
                    if (i % 4 == 0) GUILayout.BeginHorizontal();
                    var def = Classes.All[i];
                    var can = world.CanLearnClass(hero.Id, def.Class, out var why);
                    if (!can && reason == null && why != "Класс уже изучен") reason = why;
                    GUI.enabled = can;
                    if (GUILayout.Button(new GUIContent(def.Name, def.Description), toolStyle, GUILayout.Width(100f), GUILayout.Height(22f)))
                        world.Enqueue(new LearnClassCommand(hero.Id, def.Class));
                    GUI.enabled = true;
                    if (i % 4 == 3 || i == Classes.All.Count - 1) GUILayout.EndHorizontal();
                }
                if (reason != null) GUILayout.Label($"<color=#AAAAAA>{reason}</color>", smallLabel);
            }

            var known = 0;
            var names = new List<string>();
            foreach (var t in Classes.Titles)
            {
                if (!world.IsTitleDiscovered(t.Id)) continue;
                known++;
                names.Add($"{t.Name} ({Classes.Get(t.A).Name} + {Classes.Get(t.B).Name})");
            }
            GUILayout.Label($"<color=#AAAAAA>Открыто титулов: {known}/{Classes.Titles.Count}{(names.Count > 0 ? ": " + string.Join(", ", names) : "")}</color>", smallLabel);
            GUILayout.EndArea();
        }

        void DrawPersonality(World world, in Citizen hero)
        {
            GUILayout.Label("<b>Черты</b>", richLabel);
            foreach (var trait in Traits.Of(hero))
            {
                var def = Traits.Get(trait);
                var color = def.Negative ? "#FF8A80" : trait == HeroTrait.Deathtouched ? "#CE93D8" : "#8BE28B";
                GUILayout.Label($"<color={color}><b>{def.Name}</b></color> — {def.Description}", smallLabel);
            }

            GUILayout.Label("<b>Отношения</b> (друзья в одном отряде бьют на 5% сильнее, соперники — слабее)", richLabel);
            var relations = world.RelationsOf(hero.Id);
            if (relations.Count == 0) GUILayout.Label("<color=#AAAAAA>Пока ни с кем не сблизился — сходите вместе в поход</color>", smallLabel);
            var shown = 0;
            foreach (var (otherId, value) in relations)
            {
                if (shown++ == 5) break;
                var other = world.CitizenIndex(otherId);
                var name = other >= 0 ? CitizenInfo.FullName(world.Citizens[other]) : "павший герой";
                var color = value >= Traits.FriendThreshold ? "#8BE28B" : value <= Traits.RivalThreshold ? "#FF8A80" : "#BBBBBB";
                GUILayout.Label($"<color={color}>{name}: {Traits.BondName(value)} ({value:+0;-0})</color>", smallLabel);
            }

            GUILayout.Label("<b>Хроника</b>", richLabel);
            var chronicle = world.ChronicleOf(hero.Id);
            for (var i = chronicle.Count - 1; i >= 0 && i >= chronicle.Count - 10; i--)
                GUILayout.Label($"<color=#BBBBBB>День {chronicle[i].Day}:</color> {chronicle[i].Text}", smallLabel);
        }

        void DrawTemple(World world, Building temple)
        {
            const float width = 520f;
            var fallen = world.Fallen;
            var rect = Panel(new Rect(10f, TopBarHeight + 8f, width, 120f + Mathf.Max(1, fallen.Count) * 46f));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));
            GUILayout.Label("<b>Храм и мемориал павших</b>", richLabel);
            GUILayout.Label($"Пока храм работает, ранения героев заживают на день быстрее. Павшего героя можно вернуть: " +
                            $"{Traits.ResurrectGoldPerLevel} золота и {Traits.ResurrectOrePerLevel} руды за уровень. Воскрешённый теряет уровень, " +
                            $"{Traits.ResurrectInjuryDays} дн. восстанавливается и получает черту «Видевший смерть».", smallLabel);
            if (!temple.IsConnected) GUILayout.Label("<color=#FF8A80>Нет дороги к складу — храм не работает</color>", smallLabel);
            if (fallen.Count == 0) GUILayout.Label("<color=#AAAAAA>Мемориал пуст. Пусть так и остаётся.</color>", smallLabel);
            for (var i = fallen.Count - 1; i >= 0; i--)
            {
                var hero = fallen[i].Hero;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{CitizenInfo.FullName(hero)}</b> — {ClassLine(hero)}, {hero.HeroLevel} ур.\n" +
                                $"<color=#BBBBBB>День {fallen[i].Day}: {fallen[i].Cause}</color>", smallLabel, GUILayout.Width(300f));
                var can = world.CanResurrect(hero.Id, out var reason);
                GUI.enabled = can;
                if (GUILayout.Button($"Воскресить\n{World.ResurrectGold(hero)} зол., {World.ResurrectOre(hero)} руды", toolStyle, GUILayout.Width(170f), GUILayout.Height(36f)))
                    world.Enqueue(new ResurrectCommand(hero.Id));
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (!can && reason != null && !reason.StartsWith("Нужен")) GUILayout.Label($"<color=#AAAAAA>{reason}</color>", smallLabel);
            }
            GUILayout.EndArea();
        }

        void SlotButton(World world, in Citizen hero, int slot, Ability current, List<Ability> options)
        {
            var text = current == Ability.None || !options.Contains(current) ? "— пусто —" : Classes.Get(current).Name;
            if (!GUILayout.Button(text, GUILayout.Width(200f))) return;
            var position = options.IndexOf(current);
            var next = position + 1 >= options.Count ? Ability.None : options[position + 1];
            world.Enqueue(new SetAbilitySlotCommand(hero.Id, slot, next));
        }

        static string RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return "#8FA8FF";
                case ItemRarity.Rare: return "#FFE36E";
                case ItemRarity.Unique: return "#FF9A3C";
                default: return "#DDDDDD";
            }
        }

        static string GearMarks(World world, int heroId)
        {
            var marks = "";
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                var item = world.Equipped(heroId, slot);
                marks += item == null ? "<color=#555555>■</color>" : $"<color={RarityColor(item.Rarity)}>■</color>";
            }
            return marks;
        }

        static string ItemTitle(Item item) =>
            $"<color={RarityColor(item.Rarity)}><b>{item.Name}</b></color> <color=#999999>({Items.RarityName(item.Rarity)}, ур. предмета {item.ItemLevel}" +
            (item.Rarity == ItemRarity.Normal ? "" : $", {Items.BaseName(Items.Base(item.BaseId), item.BaseTier).ToLower()}") + ")</color>";

        void DrawGear(World world, in Citizen hero)
        {
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                var item = world.Equipped(hero.Id, slot);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{Items.SlotName(slot)}:</b> " + (item != null ? ItemTitle(item) : "<color=#777777>пусто</color>"), smallLabel, GUILayout.Width(330f));
                if (item != null && GUILayout.Button("Снять", GUILayout.Width(70f))) world.Enqueue(new UnequipItemCommand(hero.Id, slot));
                GUILayout.EndHorizontal();
                if (item != null)
                {
                    if (item.UniqueId != 0) GUILayout.Label($"<i><color=#C8A070>{Items.Unique(item.UniqueId).Lore}</color></i>", smallLabel);
                    GUILayout.Label(Items.Describe(item), smallLabel);
                }
            }

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Склад гильдии</b>", richLabel, GUILayout.Width(120f));
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                GUI.backgroundColor = stashSlot == slot ? ActiveTool : Color.white;
                if (GUILayout.Button($"{Items.SlotName(slot)} ({world.Stash(slot).Count})", GUILayout.Width(92f))) stashSlot = slot;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            var stash = world.Stash(stashSlot);
            if (stash.Count == 0) GUILayout.Label("<color=#AAAAAA>Пусто. Предметы падают в данжах и куются в кузнице.</color>", smallLabel);
            stashScroll = GUILayout.BeginScrollView(stashScroll, GUILayout.Height(230f));
            foreach (var item in stash)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(ItemTitle(item) + "\n" + Items.Describe(item), smallLabel, GUILayout.Width(280f));
                var can = world.CanEquip(hero.Id, item.Id, out var reason);
                GUI.enabled = can;
                if (GUILayout.Button(new GUIContent("Надеть", reason), GUILayout.Width(62f))) world.Enqueue(new EquipItemCommand(hero.Id, item.Id));
                GUI.enabled = true;
                if (GUILayout.Button(new GUIContent("Разобрать", "Даёт: " + ResourceInfo.Format(World.SalvageYield(item))), GUILayout.Width(72f)))
                    world.Enqueue(new SalvageItemCommand(item.Id));
                GUILayout.EndHorizontal();
                if (!can && reason != null) GUILayout.Label($"<color=#AAAAAA>{reason}</color>", smallLabel);
            }
            GUILayout.EndScrollView();
        }

        void DrawForge(World world, Building forge)
        {
            const float width = 700f;
            var orders = world.ForgeOrders;
            var height = 140f + Items.Bases.Count * 26f + orders.Count * 20f;
            var rect = Panel(new Rect(10f, TopBarHeight + 8f, width, height));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 10f));

            GUILayout.Label("<b>Кузница</b>", richLabel);
            GUILayout.Label($"Рабочие: {forge.Workers}/{world.Def(forge).Workers}. Куёт обычные предметы выбранной базы на склад гильдии; " +
                            "магические, редкие и уникальные — только из данжей. Ярусы: простой → железный → стальной → мифриловый.", smallLabel);
            if (orders.Count > 0 && forge.Workers == 0)
                GUILayout.Label("<color=#FF8A80>Нет рабочих: в городе не осталось взрослых, которых можно перевести в кузницу</color>", smallLabel);

            foreach (var b in Items.Bases)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{char.ToUpper(b.Noun[0]) + b.Noun.Substring(1)}</b> <color=#999999>{Items.SlotName(b.Slot).ToLower()}: {Items.StatText(b.Implicit, b.ImplicitValues[1])}</color>",
                    smallLabel, GUILayout.Width(300f));
                for (var tier = 0; tier <= Items.MaxBaseTier; tier++)
                {
                    var can = world.CanCraft(b.Id, tier, out var reason);
                    GUI.enabled = can;
                    var cost = World.CraftCost(b.Slot, tier);
                    if (GUILayout.Button(new GUIContent(new[] { "Прост.", "Жел.", "Стал.", "Мифр." }[tier], ResourceInfo.Format(cost) + (reason != null ? " — " + reason : "")),
                            GUILayout.Width(80f)))
                        world.Enqueue(new CraftItemCommand(b.Id, tier));
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"<color=#AAAAAA>Цена: простое — {ResourceInfo.Format(World.CraftCost(ItemSlot.Weapon, 0))}; железное — {ResourceInfo.Format(World.CraftCost(ItemSlot.Weapon, 1))}; " +
                            $"стальное — {ResourceInfo.Format(World.CraftCost(ItemSlot.Weapon, 2))}; мифриловое — {ResourceInfo.Format(World.CraftCost(ItemSlot.Weapon, 3))}. Украшения дороже в золоте и самоцветах.</color>", smallLabel);

            if (orders.Count > 0) GUILayout.Label("<b>Очередь</b>", richLabel);
            foreach (var order in orders)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Items.BaseName(Items.Base(order.BaseId), order.Tier), smallLabel, GUILayout.Width(300f));
                var bar = GUILayoutUtility.GetRect(160f, 12f, GUILayout.Width(160f));
                DrawBar(new Rect(bar.x, bar.y + 4f, bar.width, 10f), order.Progress / World.ForgeTicks(order), new Color(0.9f, 0.6f, 0.25f));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
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
                    var affix = Dungeons.AffixName(run.Affix);
                    var room = $"Комната {run.RoomIndex + 1}/{run.Rooms.Count}: {run.Rooms[run.RoomIndex].Name}" +
                               (affix != null ? $" <color=#FFB74D>({affix})</color>" : "");
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
