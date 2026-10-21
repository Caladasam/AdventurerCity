using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdventurerCity.View
{
    public sealed partial class GameUi
    {
        const string SaveMode = "save";
        const string LoadMode = "load";

        VisualElement modalLayer;
        UiWindow mainMenu, pauseMenu, slots, settingsWindow, confirm;
        bool mainMenuOpen = true, pauseOpen, settingsOpen;
        string slotsMode;
        string confirmText;
        Action confirmAction;
        int settingsTab;
        GameKey? rebinding;
        List<SaveInfo> slotInfos = new List<SaveInfo>();

        bool AnyMenu => mainMenuOpen || pauseOpen || settingsOpen || slotsMode != null || confirmAction != null;

        void BuildMenus()
        {
            modalLayer = Layer();
            modalLayer.pickingMode = PickingMode.Position;
            modalLayer.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.72f);
            modalLayer.style.alignItems = Align.Center;
            modalLayer.style.justifyContent = Justify.Center;
            mainMenu = Window(modalLayer, BuildMainMenu);
            pauseMenu = Window(modalLayer, BuildPauseMenu);
            slots = Window(modalLayer, BuildSlots);
            settingsWindow = Window(modalLayer, BuildSettings);
            confirm = Window(modalLayer, BuildConfirm);
            GameSettings.Changed += ApplyUiScale;
            ApplyUiScale();
        }

        void ApplyUiScale()
        {
            if (document == null || document.panelSettings == null) return;
            var scale = GameSettings.UiScale;
            document.panelSettings.referenceResolution = new Vector2Int(Mathf.RoundToInt(1600f / scale), Mathf.RoundToInt(900f / scale));
        }

        void OnDestroy() => GameSettings.Changed -= ApplyUiScale;

        void UpdateMenus()
        {
            var anyMenu = AnyMenu;
            runner.Menu = anyMenu;
            modalLayer.style.display = anyMenu ? DisplayStyle.Flex : DisplayStyle.None;
            var top = confirmAction != null ? confirm : settingsOpen ? settingsWindow : slotsMode != null ? slots : pauseOpen ? pauseMenu : mainMenuOpen ? mainMenu : null;
            foreach (var window in new[] { mainMenu, pauseMenu, slots, settingsWindow, confirm }) window.Visible = window == top;
        }

        void HandleKeys()
        {
            if (rebinding != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    rebinding = null;
                    RefreshAll();
                    return;
                }
                foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
                {
                    if (code == KeyCode.None || code >= KeyCode.Mouse0 || !Input.GetKeyDown(code)) continue;
                    GameSettings.Bind(rebinding.Value, code);
                    rebinding = null;
                    RefreshAll();
                    break;
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Back();
                RefreshAll();
                return;
            }
            if (AnyMenu) return;

            if (GameSettings.Down(GameKey.Economy)) economyOpen = !economyOpen;
            if (GameSettings.Down(GameKey.Map)) mapOpen = !mapOpen;
            if (GameSettings.Down(GameKey.Pause)) runner.Paused = !runner.Paused;
            if (GameSettings.Down(GameKey.Speed1)) runner.SetSpeed(0);
            if (GameSettings.Down(GameKey.Speed2)) runner.SetSpeed(1);
            if (GameSettings.Down(GameKey.Speed3)) runner.SetSpeed(2);
            if (GameSettings.Down(GameKey.QuickSave)) runner.Save(SaveSystem.QuickSlot);
            if (GameSettings.Down(GameKey.QuickLoad) && SaveSystem.Exists(SaveSystem.QuickSlot)) runner.Load(SaveSystem.QuickSlot);
        }

        void Back()
        {
            if (confirmAction != null) CloseConfirm();
            else if (settingsOpen) settingsOpen = false;
            else if (slotsMode != null) slotsMode = null;
            else if (pauseOpen) pauseOpen = false;
            else if (mainMenuOpen) return;
            else if (runner.IsWatchingDungeon) runner.StopWatching();
            else if (tool.Back()) category = -1;
            else if (category >= 0) category = -1;
            else if (economyOpen || mapOpen || stageOpen) economyOpen = mapOpen = stageOpen = false;
            else pauseOpen = true;
        }

        void Ask(string text, Action action)
        {
            confirmText = text;
            confirmAction = action;
        }

        void CloseConfirm()
        {
            confirmText = null;
            confirmAction = null;
        }

        void OpenSlots(string mode)
        {
            slotsMode = mode;
            slotInfos = new List<SaveInfo>();
            foreach (var slot in SaveSystem.AllSlots()) slotInfos.Add(SaveSystem.Info(slot));
        }

        void MenuButton(Ui ui, string text, Action action, bool enabled = true, string tip = null) =>
            ui.Button(text, action, enabled, tip, 300f, 38f);

        void BuildMainMenu(Ui ui)
        {
            ui.Text("<size=30><color=#FFD27A><b>Город авантюристов</b></color></size>").style.unityTextAlign = TextAnchor.MiddleCenter;
            ui.Small(UiText.Gray("город, гильдия и подземелья")).style.unityTextAlign = TextAnchor.MiddleCenter;
            ui.Space(10f);
            var world = runner.Sim;
            ui.Column(() =>
            {
                MenuButton(ui, $"Продолжить\n<size=10>{SaveSystem.TitleOf(runner.LastSlot)} · день {world.Day} · {world.CurrentStage.Name}</size>", () => mainMenuOpen = false);
                MenuButton(ui, "Новая игра", () => Ask("Начать новую игру? Ручные слоты и быстрое сохранение останутся, автосохранение перезапишется в конце первого дня.", () =>
                {
                    runner.NewGame();
                    mainMenuOpen = false;
                    victoryClosed = false;
                    tool.SelectedId = 0;
                }));
                MenuButton(ui, "Загрузить", () => OpenSlots(LoadMode));
                MenuButton(ui, "Настройки", () => settingsOpen = true);
                MenuButton(ui, "Выход", runner.Quit);
            });
        }

        void BuildPauseMenu(Ui ui)
        {
            ui.Text("<size=22><b>Пауза</b></size>").style.unityTextAlign = TextAnchor.MiddleCenter;
            ui.Small(UiText.Gray($"День {runner.Sim.Day} · {runner.Sim.CurrentStage.Name}")).style.unityTextAlign = TextAnchor.MiddleCenter;
            ui.Space(8f);
            ui.Column(() =>
            {
                MenuButton(ui, "Продолжить", () => pauseOpen = false);
                MenuButton(ui, "Сохранить", () => OpenSlots(SaveMode));
                MenuButton(ui, "Загрузить", () => OpenSlots(LoadMode));
                MenuButton(ui, "Настройки", () => settingsOpen = true);
                MenuButton(ui, "В главное меню", () =>
                {
                    runner.Save(SaveSystem.AutoSlot);
                    pauseOpen = false;
                    mainMenuOpen = true;
                }, true, "Игра сохранится в автосохранение");
                MenuButton(ui, "Выйти из игры", runner.Quit, true, "Игра сохранится в автосохранение");
            });
        }

        void BuildSlots(Ui ui)
        {
            ui.Width(560f);
            var saving = slotsMode == SaveMode;
            CloseRow(ui, saving ? "Сохранить игру" : "Загрузить игру", () => slotsMode = null, "Esc");
            foreach (var info in slotInfos)
            {
                if (saving && info.Slot == SaveSystem.AutoSlot) continue;
                var slot = info.Slot;
                var details = !info.Exists ? UiText.Gray("пусто")
                    : info.Broken ? UiText.Red("файл повреждён — будет взята резервная копия, если она есть")
                    : $"День {info.Day} · {info.Stage} · жителей {info.Population}, героев {info.Heroes}{(info.Victory ? " · <color=#FFD54F>победа</color>" : "")}\n" +
                      UiText.Gray(info.Time.ToString("dd.MM.yyyy HH:mm"));
                ui.Row(() =>
                {
                    ui.Column(() =>
                    {
                        ui.Text($"<b>{info.Title}</b>{(slot == runner.LastSlot ? UiText.Gray(" · текущая игра") : "")}");
                        ui.Small(details);
                    }, 400f);
                    ui.Flex();
                    if (saving)
                        ui.Button("Сохранить", () =>
                        {
                            void Write()
                            {
                                runner.Save(slot);
                                OpenSlots(SaveMode);
                            }
                            if (info.Exists) Ask($"Перезаписать «{info.Title}»?", Write);
                            else Write();
                        }, true, null, 110f, 34f);
                    else
                        ui.Button("Загрузить", () => Ask($"Загрузить «{info.Title}»? Несохранённый прогресс текущей игры пропадёт.", () =>
                        {
                            if (!runner.Load(slot)) return;
                            slotsMode = null;
                            pauseOpen = false;
                            mainMenuOpen = false;
                            victoryClosed = false;
                            tool.SelectedId = 0;
                        }), info.Exists, null, 110f, 34f);
                });
                ui.Space(4f);
            }
        }

        void BuildConfirm(Ui ui)
        {
            ui.Width(440f);
            ui.Text(confirmText ?? "");
            ui.Space(8f);
            ui.Row(() =>
            {
                ui.Flex();
                ui.Button("Да", () =>
                {
                    var action = confirmAction;
                    CloseConfirm();
                    action?.Invoke();
                }, true, null, 110f, 32f);
                ui.Button("Отмена", CloseConfirm, true, null, 110f, 32f);
            });
        }

        void BuildSettings(Ui ui)
        {
            ui.Width(560f);
            CloseRow(ui, "Настройки", () =>
            {
                settingsOpen = false;
                rebinding = null;
            }, "Esc");
            ui.Row(() =>
            {
                var tabs = new[] { "Звук", "Экран", "Управление" };
                for (var t = 0; t < tabs.Length; t++)
                {
                    var tab = t;
                    ui.Button(tabs[t], () =>
                    {
                        settingsTab = tab;
                        rebinding = null;
                    }, true, null, 140f, 0f, settingsTab == t, "tab");
                }
            });
            ui.Space(6f);
            if (settingsTab == 0) BuildSoundSettings(ui);
            else if (settingsTab == 1) BuildScreenSettings(ui);
            else BuildControlSettings(ui);
        }

        void BuildSoundSettings(Ui ui)
        {
            void Volume(string name, float value, Action<float> set)
            {
                ui.Row(() =>
                {
                    ui.Text(name, 160f);
                    ui.Slider(value, 0f, 1f, set, 280f);
                    ui.Text($"{Mathf.RoundToInt(value * 100f)}%", 50f);
                });
            }
            Volume("Общая громкость", GameSettings.MasterVolume, v => GameSettings.SetVolumes(v, GameSettings.MusicVolume, GameSettings.SfxVolume));
            Volume("Музыка", GameSettings.MusicVolume, v => GameSettings.SetVolumes(GameSettings.MasterVolume, v, GameSettings.SfxVolume));
            Volume("Эффекты", GameSettings.SfxVolume, v => GameSettings.SetVolumes(GameSettings.MasterVolume, GameSettings.MusicVolume, v));
            ui.Row(() =>
            {
                ui.Check(GameSettings.Muted, () => GameSettings.SetMuted(!GameSettings.Muted));
                ui.Text("Без звука");
            });
        }

        void BuildScreenSettings(Ui ui)
        {
            var fullscreen = Screen.fullScreen;
            var resolutions = GameSettings.Resolutions();
            var current = -1;
            for (var i = 0; i < resolutions.Count; i++)
                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                    current = i;
            ui.Row(() =>
            {
                ui.Check(fullscreen, () => GameSettings.SetResolution(Screen.width, Screen.height, !fullscreen));
                ui.Text("Полноэкранный режим");
            });
            ui.Row(() =>
            {
                ui.Text("Разрешение", 160f);
                ui.Button("◀", () =>
                {
                    if (resolutions.Count == 0) return;
                    var next = resolutions[current <= 0 ? resolutions.Count - 1 : current - 1];
                    GameSettings.SetResolution(next.width, next.height, fullscreen);
                }, resolutions.Count > 0, null, 30f);
                ui.Text($"{Screen.width}×{Screen.height}", 120f).style.unityTextAlign = TextAnchor.MiddleCenter;
                ui.Button("▶", () =>
                {
                    if (resolutions.Count == 0) return;
                    var next = resolutions[(current + 1) % resolutions.Count];
                    GameSettings.SetResolution(next.width, next.height, fullscreen);
                }, resolutions.Count > 0, null, 30f);
            });
            ui.Row(() =>
            {
                ui.Text("Масштаб интерфейса", 160f);
                ui.Button("−", () => GameSettings.SetUiScale(GameSettings.UiScale - 0.1f), GameSettings.UiScale > 0.75f, null, 30f);
                ui.Text($"{Mathf.RoundToInt(GameSettings.UiScale * 100f)}%", 120f).style.unityTextAlign = TextAnchor.MiddleCenter;
                ui.Button("+", () => GameSettings.SetUiScale(GameSettings.UiScale + 0.1f), GameSettings.UiScale < 1.45f, null, 30f);
            });
            if (Application.isEditor) ui.Small(UiText.Gray("В редакторе разрешение и полноэкранный режим не меняются — только в собранной игре."));
        }

        void BuildControlSettings(Ui ui)
        {
            ui.Small(UiText.Gray("Клик по клавише — затем нажмите новую (Esc — отмена). Если клавиша занята, действия поменяются местами."));
            foreach (GameKey key in Enum.GetValues(typeof(GameKey)))
            {
                var action = key;
                ui.Row(() =>
                {
                    ui.Text(GameSettings.KeyName(key), 200f);
                    var waiting = rebinding == key;
                    ui.Button(waiting ? "нажмите клавишу…" : GameSettings.CodeName(GameSettings.Get(key)), () => rebinding = action, true, null, 160f, 0f, waiting);
                });
            }
            ui.Space(4f);
            ui.Small(UiText.Gray("Всегда: ЛКМ — строить и выбирать, ПКМ или Esc — отмена и назад, стрелки — камера, колесо — зум, СКМ — панорама."));
            ui.Button("Сбросить клавиши", () =>
            {
                GameSettings.ResetKeys();
                rebinding = null;
            }, true, null, 180f);
        }
    }
}
