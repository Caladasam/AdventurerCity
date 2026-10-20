using System;
using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public enum SiteKind
    {
        Dungeon,
        Lair
    }

    public sealed class SiteDef
    {
        public int Id;
        public string Name;
        public SiteKind Kind;
        public float MapX;
        public float MapY;
        public int Distance;
        public int DungeonId;
        public bool StartsDiscovered;
        public float TensionPerDay = Region.TensionPerDay;
        public EnemyKind[] RaidEnemies = new EnemyKind[0];
    }

    [Serializable]
    public sealed class SiteState
    {
        public int Id;
        public bool Discovered;
        public float Tension;
        public int TimesCleared;
        public int Raids;
    }

    [Serializable]
    public sealed class ScoutRecord
    {
        public int HeroId;
        public int SiteId;
        public int TicksLeft;
    }

    [Serializable]
    public sealed class RaidRecord
    {
        public int SiteId;
        public int TicksLeft;
    }

    public static class Region
    {
        public const float TensionPerDay = 4f;
        public const float TensionMax = 100f;
        public const float TensionDropOnRetreat = 30f;
        public const int ScoutTicksPerDistance = 40;
        public const int RaidTicksPerDistance = 30;
        public const float RaidScalePerRaid = 0.15f;
        public const float PillageShare = 0.2f;

        static readonly SiteDef[] sites =
        {
            new SiteDef
            {
                Id = 1, Name = Dungeons.Get(Dungeons.MineId).Name, Kind = SiteKind.Dungeon, MapX = -0.45f, MapY = 0.35f,
                Distance = Dungeons.Get(Dungeons.MineId).Distance, DungeonId = Dungeons.MineId, StartsDiscovered = true,
                RaidEnemies = new[] { EnemyKind.Ratman, EnemyKind.Ratman, EnemyKind.Digger }
            },
            new SiteDef
            {
                Id = 2, Name = Dungeons.Get(Dungeons.CatacombsId).Name, Kind = SiteKind.Dungeon, MapX = 0.62f, MapY = -0.45f,
                Distance = Dungeons.Get(Dungeons.CatacombsId).Distance, DungeonId = Dungeons.CatacombsId,
                RaidEnemies = new[] { EnemyKind.Skeleton, EnemyKind.Skeleton, EnemyKind.Ghoul }
            },
            new SiteDef
            {
                Id = 3, Name = Dungeons.Get(Dungeons.WolfDenId).Name, Kind = SiteKind.Lair, MapX = -0.7f, MapY = -0.55f,
                Distance = Dungeons.Get(Dungeons.WolfDenId).Distance, DungeonId = Dungeons.WolfDenId, TensionPerDay = 5f,
                RaidEnemies = new[] { EnemyKind.Wolf, EnemyKind.Wolf, EnemyKind.Wolf }
            },
            new SiteDef
            {
                Id = 4, Name = Dungeons.Get(Dungeons.BanditCampId).Name, Kind = SiteKind.Lair, MapX = 0.72f, MapY = 0.6f,
                Distance = Dungeons.Get(Dungeons.BanditCampId).Distance, DungeonId = Dungeons.BanditCampId, TensionPerDay = 4f,
                RaidEnemies = new[] { EnemyKind.Bandit, EnemyKind.Bandit, EnemyKind.Archer }
            }
        };

        public static IReadOnlyList<SiteDef> Sites => sites;

        public static SiteDef Get(int id)
        {
            foreach (var site in sites)
                if (site.Id == id)
                    return site;
            return null;
        }

        public static SiteDef ForDungeon(int dungeonId)
        {
            foreach (var site in sites)
                if (site.DungeonId == dungeonId)
                    return site;
            return null;
        }
    }
}
