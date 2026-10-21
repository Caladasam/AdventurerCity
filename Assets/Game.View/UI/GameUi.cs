using System.Collections.Generic;
using AdventurerCity.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdventurerCity.View
{
    public sealed partial class GameUi : MonoBehaviour
    {
        const int LogLines = 8;

        static readonly (string Name, BuildingKind[] Kinds)[] categories =
        {
            ("Жильё и склад", new[] { BuildingKind.House, BuildingKind.Storehouse }),
            ("Производство", new[] { BuildingKind.Woodcutter, BuildingKind.Farm, BuildingKind.Sawmill, BuildingKind.Bakery }),
            ("Гильдия и герои", new[] { BuildingKind.Guild, BuildingKind.Forge, BuildingKind.Temple, BuildingKind.Tavern, BuildingKind.TrainingYard }),
            ("Город", new[] { BuildingKind.Watchtower, BuildingKind.Market, BuildingKind.Barracks, BuildingKind.TownHall })
        };

        readonly List<UiWindow> windows = new List<UiWindow>();
        GameRunner runner;
        BuildTool tool;
        UIDocument document;
        VisualElement root;
        Label tooltip;
        Label hint;
        VisualElement tipOwner;
        string tipText;
        bool refreshAll;

        UiWindow topBar, toolbar, categoryBar, log, selection, heroWindow, expeditionsWidget, raidWarning, economy, map, stage, victory;
        ResourceFlow[] flows;
        int flowsFrame = -1;
        int category = -1;
        bool economyOpen, mapOpen, stageOpen, victoryClosed;

        public void Init(GameRunner owner, BuildTool buildTool)
        {
            runner = owner;
            tool = buildTool;

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/GameTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));

            var left = Layer();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.FlexStart;
            left.style.top = 38f;
            left.style.left = 4f;
            left.style.bottom = 64f;
            var right = Layer();
            right.style.left = StyleKeyword.Auto;
            right.style.right = 4f;
            right.style.top = 38f;
            right.style.width = 350f;
            var center = Layer();
            center.style.top = 40f;
            center.style.alignItems = Align.Center;
            var bottomLeft = Layer();
            bottomLeft.style.top = StyleKeyword.Auto;
            bottomLeft.style.right = StyleKeyword.Auto;
            bottomLeft.style.bottom = 84f;
            bottomLeft.style.left = 4f;
            var bottomCenter = Layer();
            bottomCenter.style.top = StyleKeyword.Auto;
            bottomCenter.style.bottom = 84f;
            bottomCenter.style.alignItems = Align.Center;
            var bottom = Layer();
            bottom.style.top = StyleKeyword.Auto;
            bottom.style.bottom = 0f;

            selection = Window(left, BuildSelection);
            heroWindow = Window(left, BuildHeroPanel);
            log = Window(right, BuildLog);
            economy = Window(center, BuildEconomy);
            map = Window(center, BuildMap);
            stage = Window(center, BuildStage);
            victory = Window(center, BuildVictory);
            expeditionsWidget = Window(bottomLeft, BuildExpeditionsWidget);
            raidWarning = Window(bottomCenter, BuildRaidWarning);
            categoryBar = Window(bottom, BuildCategoryBar);
            categoryBar.Root.style.alignSelf = Align.FlexStart;
            categoryBar.Root.style.marginLeft = 80f;
            toolbar = Window(bottom, BuildToolbar, "bar-strip");
            toolbar.Root.style.marginBottom = 0f;
            toolbar.Root.style.marginLeft = 0f;
            toolbar.Root.style.marginRight = 0f;
            topBar = Window(root, BuildTopBar, "bar-strip");
            topBar.Root.style.position = Position.Absolute;
            topBar.Root.style.left = 0f;
            topBar.Root.style.right = 0f;
            topBar.Root.style.top = 0f;
            topBar.Root.style.marginTop = 0f;
            topBar.Root.style.marginLeft = 0f;
            topBar.Root.style.marginRight = 0f;

            BuildMenus();
            hint = new Label();
            hint.AddToClassList("hint");
            hint.pickingMode = PickingMode.Ignore;
            root.Add(hint);
            tooltip = new Label { enableRichText = true, pickingMode = PickingMode.Ignore };
            tooltip.AddToClassList("tooltip");
            tooltip.style.display = DisplayStyle.None;
            root.Add(tooltip);
        }

        VisualElement Layer()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("layer");
            root.Add(layer);
            return layer;
        }

        UiWindow Window(VisualElement parent, System.Action<Ui> build, string cls = null)
        {
            var window = new UiWindow(this, parent, build, cls);
            windows.Add(window);
            return window;
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

        public void RefreshAll() => refreshAll = true;

        void Update()
        {
            if (root == null) return;
            HandleKeys();
            UpdateMenus();

            var world = runner.Sim;
            var watching = runner.IsWatchingDungeon || mainMenuOpen;
            var building = world.GetBuilding(tool.SelectedId);
            topBar.Visible = !mainMenuOpen;
            log.Visible = world != null && runner.Log.Count > 0;
            toolbar.Visible = !watching;
            categoryBar.Visible = !watching && category >= 0;
            selection.Visible = !watching && building != null;
            heroWindow.Visible = !watching && building != null && building.Kind == BuildingKind.Guild && building.IsComplete && SelectedHero(world) >= 0;
            expeditionsWidget.Visible = !watching && world.Expeditions.Count > 0 && (building == null || building.Kind != BuildingKind.Guild);
            raidWarning.Visible = !watching && world.Raids.Count > 0;
            economy.Visible = !watching && economyOpen;
            map.Visible = !watching && mapOpen;
            stage.Visible = !watching && stageOpen;
            victory.Visible = !watching && world.Victory && !victoryClosed;
            hint.style.display = watching || category >= 0 ? DisplayStyle.None : DisplayStyle.Flex;
            hint.text = $"ЛКМ — строить/выбрать · ПКМ/Esc — отмена · {Key(GameKey.Rotate)} — поворот · {Key(GameKey.CameraUp)}{Key(GameKey.CameraLeft)}{Key(GameKey.CameraDown)}{Key(GameKey.CameraRight)}, колесо, СКМ — камера · " +
                        $"{Key(GameKey.Pause)} — пауза · {Key(GameKey.Economy)} — экономика · {Key(GameKey.Map)} — карта · Esc — меню";

            var force = refreshAll;
            refreshAll = false;
            foreach (var window in windows)
            {
                if (!window.Visible) continue;
                if (force) window.Refresh(true);
                else window.Tick();
            }
            UpdateTooltip();
        }

        static string Key(GameKey key) => GameSettings.CodeName(GameSettings.Get(key));

        public bool IsPointerOverUi(Vector3 mousePosition)
        {
            if (root?.panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mousePosition.x, Screen.height - mousePosition.y));
            return root.panel.Pick(point) != null;
        }

        internal void AttachTip(VisualElement element, string text)
        {
            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                tipOwner = element;
                tipText = text;
            });
            element.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (tipOwner == element) tipOwner = null;
            });
        }

        internal void HideTipIfInside(VisualElement window)
        {
            if (tipOwner != null && window.Contains(tipOwner)) tipOwner = null;
        }

        void UpdateTooltip()
        {
            var text = tipOwner != null ? tipText : runner.IsWatchingDungeon ? null : tool.HoverMessage;
            if (string.IsNullOrEmpty(text) || root.panel == null)
            {
                tooltip.style.display = DisplayStyle.None;
                return;
            }
            var mouse = Input.mousePosition;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            tooltip.text = text;
            tooltip.style.display = DisplayStyle.Flex;
            var width = root.layout.width;
            tooltip.style.left = Mathf.Min(point.x + 16f, Mathf.Max(0f, width - 390f));
            tooltip.style.top = point.y + 18f;
        }

        void BuildTopBar(Ui ui)
        {
            var world = runner.Sim;
            var resourceFlows = Flows(world);
            ui.Row(() =>
            {
                for (var i = 0; i < ResourceInfo.Count; i++)
                {
                    var type = (ResourceType)i;
                    ui.Text($"{ResourceInfo.Name(type)}: <b>{UiText.Short(world.Stock[type])}</b> {UiText.Signed(resourceFlows[i].Net, true)}", 0f, "small")
                        .style.marginRight = 8f;
                }
                var minutes = Mathf.FloorToInt(world.DayProgress * 24f * 60f);
                ui.Text($"День {world.Day}, {minutes / 60:00}:{minutes % 60:00}", 0f, "small").style.marginRight = 8f;
                ui.Text($"Жители: {world.Population} (раб. {world.EmployedWorkers}/{world.Adults})", 0f, "small");
                ui.Button($"{world.CurrentStage.Name} · слава {world.Fame}", () => stageOpen = !stageOpen, true,
                    "Этап поселения, следующий этап и цель игры", 0f, 0f, stageOpen, world.CanAdvanceStage(out _) && !stageOpen ? "ready" : null);
                ui.Flex();
                if (runner.Paused) ui.Text("<color=#FFD54F><b>ПАУЗА</b></color>").style.marginRight = 6f;
                ui.Button(runner.Paused ? "▶" : "||", () => runner.Paused = !runner.Paused, true, $"Пауза ({Key(GameKey.Pause)})", 34f, 0f, runner.Paused);
                for (var i = 0; i < runner.Speeds.Count; i++)
                {
                    var index = i;
                    ui.Button($"x{runner.Speeds[i]}", () => runner.SetSpeed(index), true, $"Скорость x{runner.Speeds[i]} ({Key((GameKey)((int)GameKey.Speed1 + i))})", 34f, 0f,
                        !runner.Paused && i == runner.SpeedIndex);
                }
                ui.Button(GameAudio.Muted ? "Тихо" : "Звук", () => GameAudio.Muted = !GameAudio.Muted, true, "Громкость — в настройках", 52f);
                ui.Button("Меню", () => pauseOpen = true, true, "Сохранение, загрузка, настройки (Esc)", 64f);
            });
        }

        void BuildToolbar(Ui ui)
        {
            ui.Row(() =>
            {
                ui.Button($"Дорога\n{SimConstants.RoadGoldCost} зол./клетка", () => { tool.SelectRoad(); category = -1; }, true, "Зажать ЛКМ и тянуть", 0f, 0f,
                    tool.Mode == ToolMode.Road, "tool");
                for (var i = 0; i < categories.Length; i++)
                {
                    var index = i;
                    var active = category == i || tool.Mode == ToolMode.Build && System.Array.IndexOf(categories[i].Kinds, tool.Kind) >= 0;
                    ui.Button(categories[i].Name, () => category = category == index ? -1 : index, true, null, 0f, 0f, active, "tool");
                }
                ui.Button("Снос", () => { tool.SelectDemolish(); category = -1; }, true, "Вернёт половину стоимости", 0f, 0f, tool.Mode == ToolMode.Demolish, "tool");
                ui.Flex();
                ui.Button("Экономика\n(Tab)", () => economyOpen = !economyOpen, true, null, 0f, 0f, economyOpen, "tool");
                ui.Button("Карта\n(M)", () => mapOpen = !mapOpen, true, null, 0f, 0f, mapOpen, "tool");
            });
        }

        void BuildCategoryBar(Ui ui)
        {
            if (category < 0) return;
            var world = runner.Sim;
            ui.Row(() =>
            {
                foreach (var kind in categories[category].Kinds)
                {
                    var def = world.Content.Get(kind);
                    var unlocked = world.IsUnlocked(kind);
                    var active = tool.Mode == ToolMode.Build && tool.Kind == kind;
                    ui.Column(() =>
                    {
                        ui.Swatch(Palette.Building(kind));
                        ui.Button(unlocked ? $"{def.Name}\n<size=10>{ResourceInfo.Format(def.Cost)}</size>" : $"{def.Name}\n<size=10>этап «{Stages.Get(def.Stage).Name}»</size>",
                            () => tool.SelectBuilding(kind), unlocked, Describe(def), 150f, 0f, active, "tool");
                    });
                }
            });
        }

        static string Describe(BuildingDef def)
        {
            var parts = new List<string> { $"<b>{def.Name}</b> {def.Width}×{def.Depth}" };
            if (def.Workers > 0) parts.Add($"рабочих: {def.Workers}");
            if (def.Recipe != null)
                parts.Add((def.Recipe.Inputs.Length > 0 ? ResourceInfo.Format(def.Recipe.Inputs) + " → " : "") +
                          $"{ResourceInfo.Format(def.Recipe.Outputs)} за {def.Recipe.CycleTicks} с");
            if (def.Upkeep > 0) parts.Add($"содержание {def.Upkeep} зол./день");
            return string.Join(" · ", parts);
        }

        void BuildLog(Ui ui)
        {
            var entries = runner.Log;
            for (var i = entries.Count - 1; i >= 0 && i >= entries.Count - LogLines; i--)
                ui.Small($"<color=#999999>День {entries[i].Day}:</color> {entries[i].Text}");
        }
    }
}
