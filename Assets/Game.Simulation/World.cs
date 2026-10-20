using System;
using System.Collections.Generic;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        readonly Queue<Command> commands = new Queue<Command>();
        readonly List<SimEvent> events = new List<SimEvent>();
        readonly Dictionary<int, Building> byId = new Dictionary<int, Building>();
        readonly Queue<int> frontier = new Queue<int>();
        readonly int[] roadDistance;
        readonly int[] lastShortageWarning = new int[ResourceInfo.Count];
        bool accessDirty = true;
        bool workforceDirty = true;

        public World(WorldState state, ContentLibrary content)
        {
            State = state;
            Content = content;
            Grid = new CityGrid(state.Width, state.Height);
            if (state.Stock.Length < ResourceInfo.Count) Array.Resize(ref state.Stock, ResourceInfo.Count);
            Stock = new Stockpile(state.Stock);
            if (state.StockLimits == null || state.StockLimits.Length == 0)
            {
                state.StockLimits = new int[ResourceInfo.Count];
                state.StockLimits[(int)ResourceType.Planks] = SimConstants.DefaultPlanksLimit;
            }
            else if (state.StockLimits.Length < ResourceInfo.Count)
            {
                Array.Resize(ref state.StockLimits, ResourceInfo.Count);
            }
            for (var i = 0; i < lastShortageWarning.Length; i++) lastShortageWarning[i] = -100;
            roadDistance = new int[state.Width * state.Height];

            foreach (var cell in state.Roads)
                Grid.SetCell(cell, CityGrid.Road);
            foreach (var building in state.Buildings)
            {
                byId[building.Id] = building;
                Grid.Fill(Footprint(building), building.Id);
            }

            RefreshAccess();
            AssignWorkers();
            if (state.Sites == null) state.Sites = new List<SiteState>();
            if (state.Scouts == null) state.Scouts = new List<ScoutRecord>();
            if (state.Raids == null) state.Raids = new List<RaidRecord>();
            if (state.Titles == null) state.Titles = new List<int>();
            EnsurePersonalities();
            EnsureItems();
            EnsureSites();
            RestoreExpeditions();
            RestoreRaids();
        }

        public WorldState State { get; }
        public ContentLibrary Content { get; }
        public CityGrid Grid { get; }
        public Stockpile Stock { get; }
        public int Version { get; private set; }
        public int Population => State.Citizens.Count;
        public int Adults { get; private set; }
        public int EmployedWorkers { get; private set; }
        public IReadOnlyList<Building> Buildings => State.Buildings;
        public IReadOnlyList<Citizen> Citizens => State.Citizens;
        public int Day => (int)(State.Tick / SimConstants.TicksPerDay) + 1;
        public float DayProgress => State.Tick % SimConstants.TicksPerDay / (float)SimConstants.TicksPerDay;

        public static World CreateNew(ContentLibrary content)
        {
            var size = SimConstants.MapSize;
            var state = new WorldState { Width = size, Height = size, Stock = new int[ResourceInfo.Count] };
            var world = new World(state, content);

            var center = size / 2;
            var storehouse = world.Place(BuildingKind.Storehouse, center - 1, center, false);
            storehouse.ConstructionLeft = 0;
            for (var x = center - 10; x <= center + 10; x++)
                world.AddRoad(x, center - 1);

            state.Stock[(int)ResourceType.Gold] = 200;
            state.Stock[(int)ResourceType.Logs] = 100;
            state.Stock[(int)ResourceType.Planks] = 70;
            state.Stock[(int)ResourceType.Grain] = 60;
            world.RefreshAccess();
            return world;
        }

        public BuildingDef Def(Building building) => Content.Get(building.Kind);

        public HouseLevelDef HouseLevel(Building house) => Content.HouseLevels[house.Level - 1];

        public CellRect Footprint(Building building) =>
            Footprint(building.Kind, building.X, building.Y, building.Rotated);

        public CellRect Footprint(BuildingKind kind, int x, int y, bool rotated)
        {
            var def = Content.Get(kind);
            return rotated ? new CellRect(x, y, def.Depth, def.Width) : new CellRect(x, y, def.Width, def.Depth);
        }

        public Building GetBuilding(int id) => byId.TryGetValue(id, out var building) ? building : null;

        public int AgeOf(in Citizen citizen) => (Day - citizen.BornDay) / SimConstants.DaysPerYear;

        public bool IsAdult(in Citizen citizen) => AgeOf(citizen) >= SimConstants.AdultAge;

        public CitizenActivity ActivityOf(in Citizen citizen)
        {
            var hour = DayProgress * 24f;
            if (citizen.OnExpedition) return CitizenActivity.Adventuring;
            if (citizen.IsHero)
            {
                if (hour >= 8f && hour < 18f) return CitizenActivity.Training;
                return hour >= 18f && hour < 22f ? CitizenActivity.Leisure : CitizenActivity.Sleeping;
            }
            if (!IsAdult(citizen))
                return hour >= 8f && hour < 20f ? CitizenActivity.Playing : CitizenActivity.Sleeping;
            if (citizen.WorkId == 0)
                return hour >= 8f && hour < 21f ? CitizenActivity.Leisure : CitizenActivity.Sleeping;
            if (hour < 6f || hour >= 22f) return CitizenActivity.Sleeping;
            if (hour < 7f) return CitizenActivity.GoingToWork;
            if (hour < 17f) return CitizenActivity.Working;
            if (hour < 18f) return CitizenActivity.GoingHome;
            return CitizenActivity.Leisure;
        }

        public Building BuildingAt(int x, int y)
        {
            if (!Grid.InBounds(x, y)) return null;
            var id = Grid[x, y];
            return id > 0 ? GetBuilding(id) : null;
        }

        public bool CanPlace(BuildingKind kind, int x, int y, bool rotated, out string reason)
        {
            var def = Content.Get(kind);
            if (!Grid.IsFree(Footprint(kind, x, y, rotated)))
            {
                reason = "Место занято";
                return false;
            }
            if (!Stock.CanAfford(def.Cost))
            {
                reason = "Не хватает: " + Stock.DescribeMissing(def.Cost);
                return false;
            }
            reason = null;
            return true;
        }

        public int StockLimit(ResourceType type) => State.StockLimits[(int)type];

        public void Enqueue(Command command) => commands.Enqueue(command);

        public void DrainEvents(List<SimEvent> target)
        {
            target.AddRange(events);
            events.Clear();
        }

        public void FlushCommands()
        {
            if (commands.Count == 0) return;
            while (commands.Count > 0)
            {
                var command = commands.Dequeue();
                if (!command.Execute(this, out var error) && error != null)
                    Report(error);
            }

            if (accessDirty) RefreshAccess();
            if (workforceDirty) AssignWorkers();
            foreach (var building in State.Buildings)
            {
                var recipe = Def(building).Recipe;
                building.LimitReached = recipe != null && LimitReached(recipe);
            }
        }

        public void Step()
        {
            FlushCommands();

            State.Tick++;
            TickConstruction();
            if (accessDirty) RefreshAccess();
            if (workforceDirty) AssignWorkers();
            TickProduction();
            TickForge();
            TickScouts();
            TickExpeditions();
            TickRaids();
            if (State.Tick % SimConstants.TicksPerDay == 0)
            {
                Households.DailyUpdate(this);
                DailyHeroes();
                DailyTraining();
                DailyRegion();
                WarnShortages();
                workforceDirty = true;
            }
        }

        internal void SetPaused(Building building, bool paused)
        {
            building.Paused = paused;
            workforceDirty = true;
        }

        internal void SetPriority(Building building, bool priority)
        {
            building.Priority = priority;
            workforceDirty = true;
        }

        internal void SetStockLimit(ResourceType type, int limit) => State.StockLimits[(int)type] = Math.Max(0, limit);

        void WarnShortages()
        {
            var flows = Economy.Compute(this);
            for (var i = 0; i < flows.Length; i++)
            {
                var type = (ResourceType)i;
                var daysLeft = Economy.DaysLeft(Stock[type], flows[i]);
                if (daysLeft >= SimConstants.ShortageWarningDays || Day - lastShortageWarning[i] < SimConstants.ShortageWarningDays)
                    continue;
                lastShortageWarning[i] = Day;
                Report(daysLeft < 1f
                    ? $"{ResourceInfo.Name(type)} на исходе: расход {flows[i].Consumed:0}/день, производство {flows[i].Produced:0}/день"
                    : $"{ResourceInfo.Name(type)} закончится через ~{daysLeft:0} дн.: расход {flows[i].Consumed:0}/день, производство {flows[i].Produced:0}/день");
            }
        }

        internal void AddCitizen(int homeId, int ageYears)
        {
            var female = Random(2) == 0;
            State.Citizens.Add(new Citizen
            {
                Id = State.NextCitizenId++,
                Female = female,
                FirstName = Random(CitizenInfo.FirstNameCount(female)),
                LastName = Random(CitizenInfo.LastNameCount),
                BornDay = Day - ageYears * SimConstants.DaysPerYear,
                HomeId = homeId
            });
            var home = GetBuilding(homeId);
            if (home != null) home.Residents++;
            workforceDirty = true;
        }

        internal int EvictNewest(int homeId, int keep)
        {
            var residents = 0;
            foreach (var citizen in State.Citizens)
                if (citizen.HomeId == homeId)
                    residents++;

            var evicted = 0;
            for (var i = State.Citizens.Count - 1; i >= 0 && residents > keep; i--)
            {
                if (State.Citizens[i].HomeId != homeId || keep > 0 && State.Citizens[i].IsHero) continue;
                State.Citizens.RemoveAt(i);
                residents--;
                evicted++;
            }
            var home = GetBuilding(homeId);
            if (home != null) home.Residents = residents;
            if (evicted > 0) workforceDirty = true;
            return evicted;
        }

        internal int Random(int maxExclusive)
        {
            var x = (uint)State.RngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State.RngState = (int)x;
            return (int)(x % (uint)maxExclusive);
        }

        internal Building Place(BuildingKind kind, int x, int y, bool rotated)
        {
            var def = Content.Get(kind);
            var building = new Building
            {
                Id = State.NextId++,
                Kind = kind,
                X = x,
                Y = y,
                Rotated = rotated,
                ConstructionLeft = def.BuildTicks
            };
            Stock.Spend(def.Cost);
            State.Buildings.Add(building);
            byId[building.Id] = building;
            Grid.Fill(Footprint(building), building.Id);
            MarkStructureChanged();
            return building;
        }

        internal bool AddRoad(int x, int y)
        {
            if (!Grid.InBounds(x, y) || Grid[x, y] != CityGrid.Empty) return false;
            var index = Grid.Index(x, y);
            Grid.SetCell(index, CityGrid.Road);
            State.Roads.Add(index);
            MarkStructureChanged();
            return true;
        }

        internal bool Demolish(int x, int y, out string error)
        {
            error = null;
            if (!Grid.InBounds(x, y)) return false;

            if (Grid[x, y] == CityGrid.Road)
            {
                var index = Grid.Index(x, y);
                Grid.SetCell(index, CityGrid.Empty);
                State.Roads.Remove(index);
                MarkStructureChanged();
                return true;
            }

            var building = BuildingAt(x, y);
            if (building == null) return false;
            if (building.Kind == BuildingKind.Storehouse && building.IsComplete &&
                CountComplete(BuildingKind.Storehouse) == 1)
            {
                error = "Нельзя снести последний склад";
                return false;
            }

            var def = Def(building);
            Stock.Refund(def.Cost, building.IsComplete ? SimConstants.DemolishRefund : 1f);
            Grid.Fill(Footprint(building), CityGrid.Empty);
            State.Buildings.Remove(building);
            byId.Remove(building.Id);
            var left = EvictNewest(building.Id, 0);
            MarkStructureChanged();
            Report(left > 0 ? $"Снесено: {def.Name}, жильцов ушло из города: {left}" : "Снесено: " + def.Name);
            return true;
        }

        internal void Report(string text) => events.Add(new SimEvent(State.Tick, text));

        void MarkStructureChanged()
        {
            Version++;
            accessDirty = true;
            workforceDirty = true;
        }

        int CountComplete(BuildingKind kind)
        {
            var count = 0;
            foreach (var building in State.Buildings)
                if (building.Kind == kind && building.IsComplete)
                    count++;
            return count;
        }

        void TickConstruction()
        {
            foreach (var building in State.Buildings)
            {
                if (building.ConstructionLeft <= 0) continue;
                building.ConstructionLeft--;
                if (building.ConstructionLeft == 0)
                {
                    Report("Построено: " + Def(building).Name);
                    MarkStructureChanged();
                }
            }
        }

        void RefreshAccess()
        {
            Array.Fill(roadDistance, -1);
            frontier.Clear();

            foreach (var building in State.Buildings)
            {
                if (building.Kind != BuildingKind.Storehouse || !building.IsComplete) continue;
                ForEachNeighbor(Footprint(building), index =>
                {
                    if (Grid.GetCell(index) != CityGrid.Road || roadDistance[index] >= 0) return;
                    roadDistance[index] = 0;
                    frontier.Enqueue(index);
                });
            }

            while (frontier.Count > 0)
            {
                var index = frontier.Dequeue();
                var x = index % Grid.Width;
                var y = index / Grid.Width;
                Visit(x + 1, y, roadDistance[index] + 1);
                Visit(x - 1, y, roadDistance[index] + 1);
                Visit(x, y + 1, roadDistance[index] + 1);
                Visit(x, y - 1, roadDistance[index] + 1);
            }

            var radius = Content.Get(BuildingKind.Storehouse).StorageRadius;
            foreach (var building in State.Buildings)
            {
                var best = -1;
                ForEachNeighbor(Footprint(building), index =>
                {
                    var distance = roadDistance[index];
                    if (distance >= 0 && (best < 0 || distance < best)) best = distance;
                });
                building.RoadDistance = best;
                building.Efficiency = best < 0
                    ? 0f
                    : Math.Max(SimConstants.MinLogisticsEfficiency,
                        1f - SimConstants.LogisticsPenaltyPerCell * Math.Max(0, best - radius));
            }

            accessDirty = false;
        }

        void Visit(int x, int y, int distance)
        {
            if (!Grid.IsRoad(x, y)) return;
            var index = Grid.Index(x, y);
            if (roadDistance[index] >= 0) return;
            roadDistance[index] = distance;
            frontier.Enqueue(index);
        }

        void ForEachNeighbor(CellRect rect, Action<int> visit)
        {
            for (var x = rect.X; x < rect.XMax; x++)
            {
                if (Grid.InBounds(x, rect.Y - 1)) visit(Grid.Index(x, rect.Y - 1));
                if (Grid.InBounds(x, rect.YMax)) visit(Grid.Index(x, rect.YMax));
            }
            for (var y = rect.Y; y < rect.YMax; y++)
            {
                if (Grid.InBounds(rect.X - 1, y)) visit(Grid.Index(rect.X - 1, y));
                if (Grid.InBounds(rect.XMax, y)) visit(Grid.Index(rect.XMax, y));
            }
        }

        void AssignWorkers()
        {
            foreach (var building in State.Buildings)
            {
                building.Residents = 0;
                building.Workers = 0;
            }

            var citizens = State.Citizens;
            var adults = 0;
            var employed = 0;
            for (var i = 0; i < citizens.Count; i++)
            {
                var citizen = citizens[i];
                var home = GetBuilding(citizen.HomeId);
                if (home != null) home.Residents++;
                if (citizen.IsHero) continue;
                if (!IsAdult(citizen))
                {
                    citizen.WorkId = 0;
                    citizens[i] = citizen;
                    continue;
                }

                adults++;
                var work = GetBuilding(citizen.WorkId);
                if (work != null && HasVacancy(work))
                {
                    work.Workers++;
                    employed++;
                }
                else
                {
                    citizen.WorkId = 0;
                    citizens[i] = citizen;
                }
            }

            employed += FillVacancies(true);
            StealForPriority();
            employed += FillVacancies(false);

            Adults = adults;
            EmployedWorkers = employed;
            workforceDirty = false;
        }

        bool LimitReached(Recipe recipe)
        {
            foreach (var output in recipe.Outputs)
            {
                var limit = StockLimit(output.Type);
                if (limit == 0 || Stock[output.Type] < limit) return false;
            }
            return true;
        }

        public bool IsPriority(Building building) =>
            building.Priority || building.Kind == BuildingKind.Forge && State.ForgeOrders.Count > 0;

        int FillVacancies(bool priorityOnly)
        {
            var citizens = State.Citizens;
            var hired = 0;
            var next = 0;
            for (var i = 0; i < citizens.Count; i++)
            {
                var citizen = citizens[i];
                if (citizen.WorkId != 0 || citizen.IsHero || !IsAdult(citizen)) continue;
                while (next < State.Buildings.Count &&
                       (!HasVacancy(State.Buildings[next]) || priorityOnly && !IsPriority(State.Buildings[next])))
                    next++;
                if (next == State.Buildings.Count) break;

                var work = State.Buildings[next];
                citizen.WorkId = work.Id;
                citizens[i] = citizen;
                work.Workers++;
                hired++;
            }
            return hired;
        }

        void StealForPriority()
        {
            var citizens = State.Citizens;
            var donor = citizens.Count - 1;
            foreach (var building in State.Buildings)
            {
                if (!IsPriority(building)) continue;
                while (HasVacancy(building) && donor >= 0)
                {
                    var citizen = citizens[donor];
                    var from = citizen.WorkId != 0 ? GetBuilding(citizen.WorkId) : null;
                    if (from == null || IsPriority(from))
                    {
                        donor--;
                        continue;
                    }

                    from.Workers--;
                    building.Workers++;
                    citizen.WorkId = building.Id;
                    citizens[donor] = citizen;
                    donor--;
                }
            }
        }

        bool HasVacancy(Building building) =>
            building.IsComplete && building.IsConnected && !building.Paused && building.Workers < Def(building).Workers;

        void TickProduction()
        {
            foreach (var building in State.Buildings)
            {
                var def = Def(building);
                var recipe = def.Recipe;
                building.LimitReached = recipe != null && LimitReached(recipe);
                if (recipe == null || !building.IsProducing)
                {
                    building.Stalled = false;
                    continue;
                }

                if (building.Progress < 1f)
                    building.Progress += building.Efficiency * building.Workers / def.Workers / recipe.CycleTicks;
                if (building.Progress < 1f) continue;

                if (!Stock.CanAfford(recipe.Inputs))
                {
                    building.Stalled = true;
                    continue;
                }

                Stock.Spend(recipe.Inputs);
                foreach (var output in recipe.Outputs)
                    Stock.Add(output.Type, output.Amount);
                building.Progress -= 1f;
                building.Stalled = false;
            }
        }
    }
}
