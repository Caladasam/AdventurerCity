using System.Collections.Generic;
using AdventurerCity.Simulation;
using AdventurerCity.Simulation.Combat;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed class GameRunner : MonoBehaviour
    {
        const float SecondsPerTick = 1f;
        const int MaxStepsPerFrame = 8;
        const int MaxLogEntries = 50;

        static readonly int[] speeds = { 1, 2, 3 };

        ContentLibrary content;
        CityRenderer cityRenderer;
        CitizensView citizensView;
        RaidView raidView;
        BuildTool tool;
        DungeonView dungeonView;
        float accumulator;
        float combatAccumulator;
        int lastAutosaveDay;

        public World Sim { get; private set; }
        public CameraRig Rig { get; private set; }
        public BuildTool Tool => tool;
        public int SpeedIndex { get; private set; }
        public bool Paused { get; set; }
        public bool Menu { get; set; } = true;
        public bool Running => !Paused && !Menu;
        public string LastSlot { get; private set; } = SaveSystem.AutoSlot;
        public IReadOnlyList<int> Speeds => speeds;
        public List<SimEvent> Log { get; } = new List<SimEvent>();
        public bool IsWatchingDungeon => dungeonView != null && dungeonView.IsWatching;
        public int WatchedExpeditionId => dungeonView != null ? dungeonView.WatchedExpeditionId : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameRunner>() == null)
                new GameObject("Game").AddComponent<GameRunner>();
        }

        void Awake()
        {
            GameSettings.Load();
            content = ContentLibrary.CreateDefault();
            var latest = SaveSystem.Latest();
            Sim = (latest != null ? SaveSystem.TryLoad(content, latest) : null) ?? World.CreateNew(content);
            if (latest != null) LastSlot = latest;
            Sim.RealtimeCombat = true;
            lastAutosaveDay = Sim.Day;

            var mapSize = new Vector2(Sim.Grid.Width * GridMath.CellSize, Sim.Grid.Height * GridMath.CellSize);
            var rig = gameObject.AddComponent<CameraRig>();
            rig.Init(new Vector3(mapSize.x * 0.5f, mapSize.y * 0.5f, 0f), mapSize);
            Rig = rig;
            cityRenderer = gameObject.AddComponent<CityRenderer>();
            cityRenderer.Init(this);
            citizensView = gameObject.AddComponent<CitizensView>();
            citizensView.Init(this);
            raidView = gameObject.AddComponent<RaidView>();
            raidView.Init(this);
            var hud = gameObject.AddComponent<GameUi>();
            tool = gameObject.AddComponent<BuildTool>();
            tool.Init(this, rig, hud);
            hud.Init(this, tool);
            dungeonView = gameObject.AddComponent<DungeonView>();
            dungeonView.Init(this, rig);
            gameObject.AddComponent<GameAudio>().Init();
        }

        void Update()
        {
            Sim.FlushCommands();
            if (!Running)
            {
                accumulator = 0f;
                combatAccumulator = 0f;
                DrainLog();
                return;
            }
            accumulator += Time.deltaTime * speeds[SpeedIndex];
            var steps = 0;
            while (accumulator >= SecondsPerTick && steps < MaxStepsPerFrame)
            {
                accumulator -= SecondsPerTick;
                Sim.Step();
                steps++;
            }

            combatAccumulator = Mathf.Min(combatAccumulator + Time.deltaTime * speeds[SpeedIndex], 1f);
            var combatSteps = (int)(combatAccumulator / CombatData.Tick);
            combatAccumulator -= combatSteps * CombatData.Tick;
            Sim.AdvanceCombat(combatSteps);

            DrainLog();
            GameAudio.Battle = IsWatchingDungeon || Sim.ActiveRaids.Count > 0;

            if (Sim.Day != lastAutosaveDay)
            {
                lastAutosaveDay = Sim.Day;
                SaveSystem.Save(Sim, SaveSystem.AutoSlot);
            }
        }

        void DrainLog()
        {
            var before = Log.Count;
            Sim.DrainEvents(Log);
            for (var i = before; i < Log.Count; i++) PlayEventSound(Log[i].Text);
            if (Log.Count > MaxLogEntries) Log.RemoveRange(0, Log.Count - MaxLogEntries);
        }

        void LateUpdate() => Sim.FlushCommands();

        static void PlayEventSound(string text)
        {
            if (text.Contains("враги у ворот") || text.StartsWith("Набег со стороны") || text.StartsWith("Вспышка в")) GameAudio.Play(Sfx.Alarm);
            else if (text.Contains("зачищено") || text.Contains("отбит")) GameAudio.Play(Sfx.Fanfare);
            else if (text.Contains("достигает")) GameAudio.Play(Sfx.LevelUp);
            else if (text.Contains("погиб") || text.Contains("не выстояли") || text.Contains("разграблен")) GameAudio.Play(Sfx.Sad);
            else if (text.StartsWith("Построено")) GameAudio.Play(Sfx.Built, 0.7f);
        }

        void OnApplicationQuit() => SaveSystem.Save(Sim, SaveSystem.AutoSlot);

        public void WatchRaid(int siteId)
        {
            tool.Cancel();
            dungeonView.WatchRaid(siteId);
        }

        public void WatchExpedition(int expeditionId)
        {
            tool.Cancel();
            dungeonView.Watch(expeditionId);
        }

        public void SetSpeed(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, speeds.Length - 1);
            Paused = false;
        }

        public void StopWatching()
        {
            if (dungeonView.IsWatching) dungeonView.StopWatching();
        }

        public bool Save(string slot)
        {
            var ok = SaveSystem.Save(Sim, slot);
            if (ok) LastSlot = slot;
            Log.Add(new SimEvent(Sim.State.Tick, ok ? $"Игра сохранена: {SaveSystem.TitleOf(slot)}" : "Не удалось сохранить игру"));
            return ok;
        }

        public bool Load(string slot)
        {
            var loaded = SaveSystem.TryLoad(content, slot);
            if (loaded == null)
            {
                Log.Add(new SimEvent(Sim.State.Tick, $"Сохранение «{SaveSystem.TitleOf(slot)}» не найдено или повреждено"));
                return false;
            }
            Replace(loaded);
            LastSlot = slot;
            Log.Add(new SimEvent(Sim.State.Tick, $"Игра загружена: {SaveSystem.TitleOf(slot)}"));
            return true;
        }

        public void NewGame()
        {
            Replace(World.CreateNew(content));
            Log.Clear();
            Log.Add(new SimEvent(Sim.State.Tick, "Новая игра: поставьте дома у дороги, ферму и лесоруба"));
        }

        public void Quit()
        {
            SaveSystem.Save(Sim, SaveSystem.AutoSlot);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Replace(World world)
        {
            Sim = world;
            Sim.RealtimeCombat = true;
            accumulator = 0f;
            combatAccumulator = 0f;
            if (dungeonView.IsWatching) dungeonView.StopWatching();
            lastAutosaveDay = world.Day;
            tool.Cancel();
            tool.SelectedId = 0;
            cityRenderer.ResetView();
            citizensView.ResetView();
            raidView.ResetView();
        }
    }
}
