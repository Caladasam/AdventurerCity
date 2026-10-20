using System;
using System.Collections.Generic;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        public IReadOnlyList<SiteState> Sites => State.Sites;
        public IReadOnlyList<ScoutRecord> Scouts => State.Scouts;

        public SiteState FindSite(int siteId)
        {
            foreach (var site in State.Sites)
                if (site.Id == siteId)
                    return site;
            return null;
        }

        public bool IsDungeonOpen(int dungeonId)
        {
            var site = Region.ForDungeon(dungeonId);
            return site != null && FindSite(site.Id)?.Discovered == true;
        }

        public bool IsHeroicUnlocked(int dungeonId)
        {
            var site = Region.ForDungeon(dungeonId);
            return site != null && (FindSite(site.Id)?.TimesCleared ?? 0) > 0;
        }

        public ScoutRecord ScoutFor(int siteId)
        {
            foreach (var scout in State.Scouts)
                if (scout.SiteId == siteId)
                    return scout;
            return null;
        }

        void EnsureSites()
        {
            foreach (var def in Region.Sites)
            {
                if (FindSite(def.Id) != null) continue;
                State.Sites.Add(new SiteState { Id = def.Id, Discovered = def.StartsDiscovered });
            }
        }

        int FindScoutHero()
        {
            foreach (var citizen in State.Citizens)
                if (IsAvailable(citizen) && !citizen.Hungry)
                    return citizen.Id;
            return 0;
        }

        public bool CanScout(int siteId, out string reason)
        {
            var site = FindSite(siteId);
            if (site == null || site.Discovered)
            {
                reason = "Место уже разведано";
                return false;
            }
            if (FindGuild() == null)
            {
                reason = "Нужна гильдия";
                return false;
            }
            if (ScoutFor(siteId) != null)
            {
                reason = "Разведчик уже в пути";
                return false;
            }
            if (FindScoutHero() == 0)
            {
                reason = "Нет свободного сытого героя";
                return false;
            }
            reason = null;
            return true;
        }

        internal bool Scout(int siteId, out string error)
        {
            if (!CanScout(siteId, out error)) return false;
            var heroId = FindScoutHero();
            var index = CitizenIndex(heroId);
            var hero = State.Citizens[index];
            hero.OnExpedition = true;
            State.Citizens[index] = hero;
            var def = Region.Get(siteId);
            State.Scouts.Add(new ScoutRecord { HeroId = heroId, SiteId = siteId, TicksLeft = Region.ScoutTicksPerDistance * def.Distance });
            Report($"{CitizenInfo.FullName(hero)} ушёл(ла) на разведку");
            return true;
        }

        void TickScouts()
        {
            var scouts = State.Scouts;
            for (var i = scouts.Count - 1; i >= 0; i--)
            {
                var scout = scouts[i];
                scout.TicksLeft--;
                if (scout.TicksLeft > 0) continue;
                scouts.RemoveAt(i);

                var site = FindSite(scout.SiteId);
                if (site != null) site.Discovered = true;
                var index = CitizenIndex(scout.HeroId);
                if (index >= 0)
                {
                    var hero = State.Citizens[index];
                    hero.OnExpedition = false;
                    State.Citizens[index] = hero;
                }
                var def = Region.Get(scout.SiteId);
                Report(def.Kind == SiteKind.Dungeon
                    ? $"Разведка нашла данж: «{def.Name}»"
                    : $"Разведка нашла логово: «{def.Name}»");
            }
        }

        void DailyRegion()
        {
            foreach (var site in State.Sites)
            {
                var def = Region.Get(site.Id);
                if (def == null || def.Kind == SiteKind.Dungeon && !site.Discovered) continue;
                if (RaidFrom(site.Id) != null) continue;
                site.Tension = Math.Min(Region.TensionMax, site.Tension + def.TensionPerDay);
                if (site.Tension >= Region.TensionMax) StartRaid(site, def);
            }
        }

        void ApplyExpeditionToSite(Combat.Expedition run)
        {
            var def = Region.ForDungeon(run.Dungeon.Id);
            var site = def != null ? FindSite(def.Id) : null;
            if (site == null) return;
            if (run.Cleared)
            {
                site.TimesCleared++;
                site.Tension = 0f;
                if (site.TimesCleared == 1) Report($"«{run.Dungeon.Name}»: открыт героический уровень сложности");
            }
            else if (!run.Wiped)
            {
                site.Tension = Math.Max(0f, site.Tension - Region.TensionDropOnRetreat);
            }
        }
    }
}
