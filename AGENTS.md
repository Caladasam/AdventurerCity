# AdventurerCity — заметки для агента

Градостроитель + гильдия героев + данжи. Unity 6000.6.6f1, URP 2D, ПК. Хобби-проект Артура: делает один с ИИ, без сроков. Общаться по-русски.

## Где что лежит

- `README.md` — источник правды по управлению, структуре и балансным константам. Меняешь структуру или числа — обнови README.
- `docs/design.md` — геймдизайн-документ (выгрузка из Claude Docs от 2026-10-29). Решения дописывать сюда, в нужный раздел.
- `Assets/Game.Simulation` — чистый C# без `UnityEngine` (C# 9 / .NET Standard 2.1). Проверяется headless через отдельный dotnet-проект вне репо.
- `Assets/Game.View` — Unity-слой: bootstrap, камера, рендер, инструмент стройки, IMGUI HUD, сохранения.
- Git: приватный https://github.com/Caladasam/AdventurerCity, ветка `main`. Коммитить от личной почты — она уже в локальном `git config`, глобальный конфиг рабочий.

## Как работать с Артуром

- Решения по дизайну принимает Артур и часто правит предложенные варианты. Давать 2–3 варианта с рекомендацией, принятое сразу фиксировать в `docs/design.md`. Детали, которые додумал сам, явно помечать.
- Ограничения в игре — через экономику, а не через прямые лимиты (например, отряды ограничены пайком и стоимостью найма, а не числом).
- Визуал: не 3D. Современный 2D-пиксель в духе Sea of Stars / Eastward, ставка на «сочность» — 2D-свет, частицы, пост-обработка, звук. Арт — из готовых паков одного семейства. Симуляция при смене визуала не меняется, переписывается только View.

## Состояние на 2026-10-20

- Этап 1 (скелет города) и этап 2 (герои) сделаны: экономика с панелью Tab, гильдия, найм, руда, экспедиции; поход сохраняется детерминированным пересчётом по seed.
- Боевая симуляция `Game.Simulation/Combat` сбалансирована headless: ур.1 ~56% зачистки, ур.3 100%, без жреца/стража 0%. Страж берёт ~75% урона, босс 1450 HP / 35 урона.
- Отзывы с плейтестов: экономика «обычный градострой», герои «нормально, но без азарта».
- Жители — отдельные агенты (2–10k простых, «для живости»), экономика абстрактная; без Entities — Burst + GPU-инстансинг; ходьба отложена до этапа 4.

## Unity CLI

- `C:\Users\artur\AppData\Local\Unity\bin\unity.exe` (в PATH пользователя; если не находится — по полному пути). Гайд: `unity skill show`.
- Основное: `unity status --until-ready --project-path <p>`, `unity recompile --project-path <p> --format json`, `unity command <name> --project-path <p> --result-only` (`editor_play`, `editor_stop`, `editor_status`, `console --tail N`, `capture_game_view`, `eval_file --file`). Нужен пакет `com.unity.pipeline`.
- `unity open <путь>` сначала пробовать напрямую.

## Грабли (выучено на ошибках)

- **Артур сам играет в Play между шагами.** Перед `recompile` / `editor_stop` проверить `command editor_status` (поле `playMode`; `runtime_status.IsPlaying` врёт). Если Play идёт не от твоего теста — не трогать, спросить.
- Перед правкой `.cs` — `editor_stop`: перекомпиляция в Play обнуляет `GameRunner.Sim` и сыплет тысячами NRE.
- **Сейвы.** Лежат в `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AdventurerCity`: `city.json` (авто), `quick.json`, `slot1–3.json` + `.bak`. Перед любым тестом в игре скопировать папку и после вернуть, созданное тестом удалить. Копию делать ДО `NewGame` и до прогона тиков: `GameRunner` автосейвит при смене дня и перетирает сохранение (так уже терялся сейв). Если eval вернул «no sim» — всё равно сначала копия.
- Не править `.cs` через Windows PowerShell 5.1 `Get-Content`/`Set-Content`: UTF-8 без BOM читается как cp1251 и портит кириллицу. Кириллица в аргументах PowerShell тоже ломает парсер.
- `eval` с кавычками из PowerShell ломается — писать код в `.cs` и вызывать `eval_file`. Скрипты для eval держать вне `<проект>/Temp` (Unity чистит его при запуске). Любые `while` — только со счётчиком-ограничителем: бесконечный цикл вешает редактор.
- `capture_game_view --save_path` пишет только внутрь проекта (в `Assets/…`) — после снимка удалить файл и `.meta`.
- Вход в Play перезагружает домен: дождаться `status --until-ready` перед следующей командой.
- Play в редакторе, окно которого в фоне, не крутит кадры. Для тестов через eval выставить `Application.runInBackground = true` (только на сессию, в ProjectSettings не писать).
- URP 2D: ассет `Assets/Settings/UniversalRP-2D.asset`. `package_add` может падать с Network error — тогда дописать модуль в `Packages/manifest.json` руками. После правки asmdef нужен `AssetDatabase.ImportAsset(..., ForceUpdate)` + Refresh. Light2D живёт в сборке `Unity.RenderPipelines.Universal.2D.Runtime`.
- В Unity 6 цвет `SpriteRenderer` не доходит до самописного CG-шейдера — передавать через `MaterialPropertyBlock` `_Color`.
- Если при Play сыпятся `InvalidOperationException` про Input — Player → Active Input Handling: «Both» или «Input Manager (Old)».
