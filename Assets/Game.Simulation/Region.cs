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
    }

    [Serializable]
    public sealed class SiteState
    {
        public int Id;
        public bool Discovered;
        public float Tension;
        public int TimesCleared;
    }

    [Serializable]
    public sealed class ScoutRecord
    {
        public int HeroId;
        public int SiteId;
        public int TicksLeft;
    }

    public static class Region
    {
        public const float TensionPerDay = 4f;
        public const float TensionMax = 100f;
        public const float TensionDropOnRetreat = 30f;
        public const int ScoutTicksPerDistance = 40;

        static readonly SiteDef[] sites =
        {
            new SiteDef
            {
                Id = 1, Name = Dungeons.Get(Dungeons.MineId).Name, Kind = SiteKind.Dungeon, MapX = -0.45f, MapY = 0.35f,
                Distance = Dungeons.Get(Dungeons.MineId).Distance, DungeonId = Dungeons.MineId, StartsDiscovered = true
            },
            new SiteDef
            {
                Id = 2, Name = Dungeons.Get(Dungeons.CatacombsId).Name, Kind = SiteKind.Dungeon, MapX = 0.62f, MapY = -0.45f,
                Distance = Dungeons.Get(Dungeons.CatacombsId).Distance, DungeonId = Dungeons.CatacombsId
            },
            new SiteDef { Id = 3, Name = "Волчье логово", Kind = SiteKind.Lair, MapX = -0.7f, MapY = -0.55f, Distance = 1 },
            new SiteDef { Id = 4, Name = "Лагерь разбойников", Kind = SiteKind.Lair, MapX = 0.72f, MapY = 0.6f, Distance = 2 }
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
                if (site.Kind == SiteKind.Dungeon && site.DungeonId == dungeonId)
                    return site;
            return null;
        }
    }
}
