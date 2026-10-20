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
        BuildTool tool;
        DungeonView dungeonView;
        float accumulator;
        float combatAccumulator;
        int lastAutosaveDay;

        public World Sim { get; private set; }
        public CameraRig Rig { get; private set; }
        public BuildTool Tool => tool;
        public int SpeedIndex { get; private set; }
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
            content = ContentLibrary.CreateDefault();
            Sim = SaveSystem.TryLoad(content) ?? World.CreateNew(content);
            Sim.RealtimeCombat = true;
            lastAutosaveDay = Sim.Day;

            var mapSize = new Vector2(Sim.Grid.Width * GridMath.CellSize, Sim.Grid.Height * GridMath.CellSize);
            var rig = gameObject.AddComponent<CameraRig>();
            rig.Init(new Vector3(mapSize.x * 0.5f, mapSize.y * 0.5f, 0f), mapSize);
            Rig = rig;
            cityRenderer = gameObject.AddComponent<CityRenderer>();
            cityRenderer.Init(this);
            var hud = gameObject.AddComponent<Hud>();
            tool = gameObject.AddComponent<BuildTool>();
            tool.Init(this, rig, hud);
            hud.Init(this, tool);
            dungeonView = gameObject.AddComponent<DungeonView>();
            dungeonView.Init(this, rig);
        }

        void Update()
        {
            Sim.FlushCommands();
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

            Sim.DrainEvents(Log);
            if (Log.Count > MaxLogEntries) Log.RemoveRange(0, Log.Count - MaxLogEntries);

            if (Sim.Day != lastAutosaveDay)
            {
                lastAutosaveDay = Sim.Day;
                SaveSystem.Save(Sim);
            }

            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();
        }

        void LateUpdate() => Sim.FlushCommands();

        void OnApplicationQuit() => SaveSystem.Save(Sim);

        public void WatchExpedition(int expeditionId)
        {
            tool.Cancel();
            dungeonView.Watch(expeditionId);
        }

        public void SetSpeed(int index) => SpeedIndex = Mathf.Clamp(index, 0, speeds.Length - 1);

        public void Save()
        {
            SaveSystem.Save(Sim);
            Log.Add(new SimEvent(Sim.State.Tick, "Игра сохранена"));
        }

        public void Load()
        {
            var loaded = SaveSystem.TryLoad(content);
            if (loaded == null)
            {
                Log.Add(new SimEvent(Sim.State.Tick, "Сохранение не найдено"));
                return;
            }
            Replace(loaded);
            Log.Add(new SimEvent(Sim.State.Tick, "Игра загружена"));
        }

        public void NewGame() => Replace(World.CreateNew(content));

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
        }
    }
}
