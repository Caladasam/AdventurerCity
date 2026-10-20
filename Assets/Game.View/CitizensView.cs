using System.Collections.Generic;
using AdventurerCity.Simulation;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed class CitizensView : MonoBehaviour
    {
        const float WalkSpeed = 2.4f;
        const float HeroScale = 0.72f;
        const int WanderRadius = 6;

        enum Goal
        {
            None,
            Inside,
            Stand,
            Wander
        }

        sealed class Walker
        {
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public Vector3 Position;
            public readonly List<Vector3> Path = new List<Vector3>();
            public int PathIndex;
            public int TargetId = -1;
            public Goal Goal;
            public float Wait;
            public string SpriteKey;
            public float Phase;
        }

        static readonly Vector2Int[] steps = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        readonly Dictionary<int, Walker> walkers = new Dictionary<int, Walker>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly List<int> gone = new List<int>();
        readonly Dictionary<int, Vector2Int> doors = new Dictionary<int, Vector2Int>();
        readonly Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        readonly List<Vector2Int> cells = new List<Vector2Int>();
        readonly System.Random random = new System.Random(7);

        GameRunner runner;
        Transform root;
        int doorsVersion = -1;

        public void Init(GameRunner owner)
        {
            runner = owner;
            root = new GameObject("Citizens").transform;
        }

        public void ResetView()
        {
            foreach (var walker in walkers.Values)
            {
                Destroy(walker.Body.gameObject);
                Destroy(walker.Shadow.gameObject);
            }
            walkers.Clear();
            doors.Clear();
            doorsVersion = -1;
        }

        void LateUpdate()
        {
            var world = runner.Sim;
            root.gameObject.SetActive(!runner.IsWatchingDungeon);
            if (runner.IsWatchingDungeon) return;
            if (world.Version != doorsVersion)
            {
                doors.Clear();
                doorsVersion = world.Version;
            }

            var guild = world.FindGuild();
            seen.Clear();
            foreach (var citizen in world.Citizens)
            {
                if (citizen.OnExpedition) continue;
                seen.Add(citizen.Id);
                if (!walkers.TryGetValue(citizen.Id, out var walker))
                {
                    walker = Spawn(world, citizen);
                    walkers.Add(citizen.Id, walker);
                }
                UpdateSprite(walker, citizen);
                Steer(world, walker, citizen, guild);
                Move(walker);
            }

            gone.Clear();
            foreach (var id in walkers.Keys)
                if (!seen.Contains(id))
                    gone.Add(id);
            foreach (var id in gone)
            {
                Destroy(walkers[id].Body.gameObject);
                Destroy(walkers[id].Shadow.gameObject);
                walkers.Remove(id);
            }
        }

        Walker Spawn(World world, in Citizen citizen)
        {
            var walker = new Walker
            {
                Body = PixelArt.Create("Citizen", root, null),
                Shadow = PixelArt.Create("Shadow", root, PixelArt.Dot()),
                Phase = (citizen.Id * 0.37f) % 1f
            };
            walker.Shadow.color = new Color(0f, 0f, 0f, 0.35f);
            walker.Shadow.transform.localScale = new Vector3(0.45f, 0.18f, 1f);
            var home = world.GetBuilding(citizen.HomeId);
            walker.Position = home != null ? DoorPoint(world, home) : GridMath.CellCenter(world.Grid.Width / 2, world.Grid.Height / 2);
            return walker;
        }

        void UpdateSprite(Walker walker, in Citizen citizen)
        {
            string key;
            if (citizen.IsHero) key = citizen.Class.ToString();
            else if (!runner.Sim.IsAdult(citizen)) key = citizen.Id % 2 == 0 ? "Child" : "Child1";
            else
            {
                var variant = citizen.Id % 4;
                key = (citizen.Female ? "Villagess" : "Villager") + (variant == 0 ? "" : variant.ToString());
            }
            if (walker.SpriteKey == key) return;
            walker.SpriteKey = key;
            walker.Body.sprite = PixelArt.Character(key);
            var scale = citizen.IsHero ? HeroScale : 1f;
            walker.Body.transform.localScale = new Vector3(scale, scale, 1f);
        }

        void Steer(World world, Walker walker, in Citizen citizen, Building guild)
        {
            var activity = world.ActivityOf(citizen);
            var home = world.GetBuilding(citizen.HomeId);
            var work = world.GetBuilding(citizen.WorkId);
            Building target = null;
            var goal = Goal.Wander;
            switch (activity)
            {
                case CitizenActivity.Sleeping:
                    target = home;
                    goal = Goal.Inside;
                    break;
                case CitizenActivity.GoingToWork:
                case CitizenActivity.Working:
                    target = work;
                    goal = Goal.Inside;
                    break;
                case CitizenActivity.GoingHome:
                    target = home;
                    goal = Goal.Stand;
                    break;
                case CitizenActivity.Training:
                    target = guild;
                    goal = Goal.Stand;
                    break;
            }
            if (target == null && goal != Goal.Wander) goal = Goal.Wander;

            if (goal == Goal.Wander)
            {
                if (walker.Goal != Goal.Wander)
                {
                    walker.Goal = Goal.Wander;
                    walker.TargetId = -1;
                    walker.Path.Clear();
                }
                if (walker.PathIndex < walker.Path.Count) return;
                walker.Wait -= Time.deltaTime;
                if (walker.Wait > 0f) return;
                var anchor = home != null ? Door(world, home) : WorldToCell(walker.Position);
                var radius = world.IsAdult(citizen) ? WanderRadius : 3;
                var destination = RandomRoadNear(world, anchor, radius);
                SetPath(world, walker, WorldToCell(walker.Position), destination, GridMath.CellCenter(destination.x, destination.y) + Jitter());
                walker.Wait = 1f + (float)random.NextDouble() * 3f;
                return;
            }

            if (walker.Goal == goal && walker.TargetId == target.Id) return;
            walker.Goal = goal;
            walker.TargetId = target.Id;
            var door = Door(world, target);
            var end = goal == Goal.Stand ? GridMath.CellCenter(door.x, door.y) + Jitter() * 2f : DoorPoint(world, target);
            SetPath(world, walker, WorldToCell(walker.Position), door, end);
        }

        void Move(Walker walker)
        {
            var moving = walker.PathIndex < walker.Path.Count;
            if (moving)
            {
                var next = walker.Path[walker.PathIndex];
                walker.Position = Vector3.MoveTowards(walker.Position, next, WalkSpeed * Time.deltaTime * runner.Speeds[runner.SpeedIndex]);
                if ((walker.Position - next).sqrMagnitude < 0.0004f) walker.PathIndex++;
            }

            var inside = !moving && walker.Goal == Goal.Inside;
            walker.Body.enabled = !inside;
            walker.Shadow.enabled = !inside;
            if (inside) return;

            var bob = moving ? Mathf.Abs(Mathf.Sin((Time.time * 2.2f + walker.Phase) * Mathf.PI * 2f)) * 0.06f : 0f;
            walker.Body.transform.localPosition = walker.Position + new Vector3(0f, bob, 0f);
            walker.Shadow.transform.localPosition = walker.Position + new Vector3(0f, 0.02f, 0f);
            if (moving && walker.PathIndex < walker.Path.Count)
            {
                var dx = walker.Path[walker.PathIndex].x - walker.Position.x;
                if (Mathf.Abs(dx) > 0.01f)
                {
                    var scale = walker.Body.transform.localScale;
                    scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dx);
                    walker.Body.transform.localScale = scale;
                }
            }
            var order = PixelArt.SortOrder(walker.Position.y) + 2;
            walker.Body.sortingOrder = order;
            walker.Shadow.sortingOrder = order - 1;
        }

        void SetPath(World world, Walker walker, Vector2Int from, Vector2Int to, Vector3 end)
        {
            walker.Path.Clear();
            walker.PathIndex = 0;
            if (FindPath(world, from, to))
                foreach (var cell in cells)
                    walker.Path.Add(GridMath.CellCenter(cell.x, cell.y) + Jitter() * 0.6f);
            walker.Path.Add(end);
        }

        bool FindPath(World world, Vector2Int from, Vector2Int to)
        {
            cells.Clear();
            if (from == to) return true;
            cameFrom.Clear();
            frontier.Clear();
            frontier.Enqueue(from);
            cameFrom[from] = from;
            var found = false;
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                if (cell == to)
                {
                    found = true;
                    break;
                }
                foreach (var step in steps)
                {
                    var next = cell + step;
                    if (cameFrom.ContainsKey(next)) continue;
                    if (next != to && !world.Grid.IsRoad(next.x, next.y)) continue;
                    cameFrom[next] = cell;
                    frontier.Enqueue(next);
                }
            }
            if (!found) return false;
            for (var cell = to; cell != from; cell = cameFrom[cell]) cells.Add(cell);
            cells.Reverse();
            return true;
        }

        Vector2Int RandomRoadNear(World world, Vector2Int anchor, int radius)
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var cell = anchor + new Vector2Int(random.Next(-radius, radius + 1), random.Next(-radius, radius + 1));
                if (world.Grid.IsRoad(cell.x, cell.y)) return cell;
            }
            return anchor;
        }

        Vector2Int Door(World world, Building building)
        {
            if (doors.TryGetValue(building.Id, out var door)) return door;
            var rect = world.Footprint(building);
            var best = new Vector2Int(rect.X + rect.Width / 2, rect.Y - 1);
            var bestScore = float.MaxValue;
            for (var x = rect.X - 1; x <= rect.XMax; x++)
            for (var y = rect.Y - 1; y <= rect.YMax; y++)
            {
                if (rect.Contains(x, y) || !world.Grid.IsRoad(x, y)) continue;
                var score = Mathf.Abs(x - (rect.X + rect.Width * 0.5f)) + (y < rect.Y ? 0f : 2f);
                if (score >= bestScore) continue;
                bestScore = score;
                best = new Vector2Int(x, y);
            }
            doors[building.Id] = best;
            return best;
        }

        Vector3 DoorPoint(World world, Building building)
        {
            var rect = world.Footprint(building);
            return new Vector3((rect.X + rect.Width * 0.5f) * GridMath.CellSize, rect.Y * GridMath.CellSize + 0.05f, 0f);
        }

        Vector3 Jitter() => new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, 0f) * 0.5f;

        static Vector2Int WorldToCell(Vector3 position) => GridMath.WorldToCell(position);
    }
}
