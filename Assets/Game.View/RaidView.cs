using System.Collections.Generic;
using AdventurerCity.Simulation;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AdventurerCity.View
{
    public sealed class RaidView : MonoBehaviour
    {
        const float EdgeDistance = 26f;

        sealed class Band
        {
            public Transform Root;
            public readonly List<SpriteRenderer> Bodies = new List<SpriteRenderer>();
            public Light2D Torch;
        }

        readonly Dictionary<RaidRecord, Band> bands = new Dictionary<RaidRecord, Band>();
        readonly HashSet<RaidRecord> seen = new HashSet<RaidRecord>();
        readonly List<RaidRecord> gone = new List<RaidRecord>();
        readonly HashSet<RaidRecord> announced = new HashSet<RaidRecord>();

        GameRunner runner;
        Transform root;

        public void Init(GameRunner owner)
        {
            runner = owner;
            root = new GameObject("Raids").transform;
        }

        public void ResetView()
        {
            foreach (var band in bands.Values) Destroy(band.Root.gameObject);
            bands.Clear();
            announced.Clear();
        }

        void LateUpdate()
        {
            var world = runner.Sim;
            root.gameObject.SetActive(!runner.IsWatchingDungeon);
            var gate = Gate(world);

            seen.Clear();
            foreach (var raid in world.Raids)
            {
                seen.Add(raid);
                if (!bands.TryGetValue(raid, out var band))
                {
                    band = Spawn(raid);
                    bands.Add(raid, band);
                }
                Place(world, raid, band, gate);

                if (raid.Fighting && !announced.Contains(raid))
                {
                    announced.Add(raid);
                    if (!runner.IsWatchingDungeon) runner.WatchRaid(raid.SiteId);
                }
            }

            gone.Clear();
            foreach (var raid in bands.Keys)
                if (!seen.Contains(raid))
                    gone.Add(raid);
            foreach (var raid in gone)
            {
                Destroy(bands[raid].Root.gameObject);
                bands.Remove(raid);
                announced.Remove(raid);
            }
        }

        Band Spawn(RaidRecord raid)
        {
            var def = Region.Get(raid.SiteId);
            var band = new Band { Root = new GameObject("Raid").transform };
            band.Root.SetParent(root, false);
            var enemies = def != null ? def.RaidEnemies : new Simulation.Combat.EnemyKind[0];
            for (var i = 0; i < enemies.Length; i++)
            {
                var body = PixelArt.Create(enemies[i].ToString(), band.Root, PixelArt.Character(enemies[i].ToString()));
                body.transform.localPosition = new Vector3((i - (enemies.Length - 1) * 0.5f) * 0.8f, (i % 2) * 0.35f, 0f);
                body.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                band.Bodies.Add(body);
            }
            band.Torch = Effects.PointLight("RaidTorch", band.Root, new Color(1f, 0.35f, 0.25f), 3.5f, 1.2f);
            return band;
        }

        void Place(World world, RaidRecord raid, Band band, Vector3 gate)
        {
            var def = Region.Get(raid.SiteId);
            var direction = def != null ? new Vector3(def.MapX, def.MapY, 0f).normalized : Vector3.right;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.right;
            var start = gate + direction * EdgeDistance;
            var total = def != null ? Region.RaidTicksPerDistance * def.Distance : 30;
            var progress = raid.Fighting ? 1f : Mathf.Clamp01(1f - raid.TicksLeft / (float)total);
            var stop = gate + direction * 2.5f;
            var position = Vector3.Lerp(start, stop, progress);
            band.Root.position = position;

            var marching = !raid.Fighting;
            for (var i = 0; i < band.Bodies.Count; i++)
            {
                var body = band.Bodies[i];
                var bob = Mathf.Abs(Mathf.Sin(Time.time * (marching ? 7f : 14f) + i)) * (marching ? 0.08f : 0.15f);
                var local = body.transform.localPosition;
                local.y = (i % 2) * 0.35f + bob;
                body.transform.localPosition = local;
                var scale = body.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (direction.x > 0f ? -1f : 1f);
                body.transform.localScale = scale;
                body.sortingOrder = PixelArt.SortOrder(position.y + local.y) + 3;
                body.color = raid.Fighting && Mathf.Repeat(Time.time * 4f + i, 1f) < 0.2f ? new Color(1f, 0.6f, 0.6f) : Color.white;
            }
            band.Torch.intensity = 1f + 0.3f * Mathf.PerlinNoise(Time.time * 5f, raid.SiteId);
        }

        static Vector3 Gate(World world)
        {
            foreach (var building in world.Buildings)
                if (building.Kind == BuildingKind.Storehouse)
                    return GridMath.RectCenter(world.Footprint(building));
            return GridMath.CellCenter(world.Grid.Width / 2, world.Grid.Height / 2);
        }
    }
}
