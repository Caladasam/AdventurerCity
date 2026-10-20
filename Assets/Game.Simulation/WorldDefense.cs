using System;
using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed class ActiveRaid
    {
        public RaidRecord Record;
        public Battle Battle;
        public List<Combatant> Defenders;
        public string Title;
        public readonly List<string> Log = new List<string>();
    }

    public sealed partial class World
    {
        static readonly ResourceType[] Plunder = { ResourceType.Gold, ResourceType.Bread, ResourceType.Grain, ResourceType.Planks };

        readonly List<ActiveRaid> activeRaids = new List<ActiveRaid>();

        public IReadOnlyList<RaidRecord> Raids => State.Raids;
        public IReadOnlyList<ActiveRaid> ActiveRaids => activeRaids;

        public RaidRecord RaidFrom(int siteId)
        {
            foreach (var raid in State.Raids)
                if (raid.SiteId == siteId)
                    return raid;
            return null;
        }

        public ActiveRaid ActiveRaidFrom(int siteId)
        {
            foreach (var raid in activeRaids)
                if (raid.Record.SiteId == siteId)
                    return raid;
            return null;
        }

        public void CountDefenders(out int heroes, out int guards)
        {
            heroes = 0;
            guards = 0;
            foreach (var citizen in State.Citizens)
            {
                if (citizen.OnExpedition) continue;
                if (citizen.IsHero) heroes++;
                else if (IsGuard(citizen)) guards++;
            }
        }

        bool IsGuard(in Citizen citizen)
        {
            if (citizen.IsHero || citizen.WorkId == 0) return false;
            var work = GetBuilding(citizen.WorkId);
            return work != null && work.Kind == BuildingKind.Watchtower && work.IsComplete;
        }

        static string RaidTitle(SiteDef def) =>
            def.Kind == SiteKind.Dungeon ? $"Монстры из «{def.Name}»" : $"Набег: «{def.Name}»";

        void StartRaid(SiteState site, SiteDef def)
        {
            site.Tension = 0f;
            var ticks = Region.RaidTicksPerDistance * def.Distance;
            State.Raids.Add(new RaidRecord { SiteId = site.Id, TicksLeft = ticks });
            var source = site.Discovered ? $"«{def.Name}»" : "неизведанных земель";
            Report(def.Kind == SiteKind.Dungeon
                ? $"Вспышка в «{def.Name}»: монстры идут на город, будут через {ticks} с"
                : $"Набег со стороны {source}: враги будут через {ticks} с");
        }

        void TickRaids()
        {
            var raids = State.Raids;
            var busy = activeRaids.Count > 0;
            for (var i = raids.Count - 1; i >= 0; i--)
            {
                var raid = raids[i];
                if (raid.Fighting) continue;
                if (raid.TicksLeft > 0) raid.TicksLeft--;
                if (raid.TicksLeft > 0 || busy) continue;
                BeginDefense(raid, i);
                busy = activeRaids.Count > 0;
            }
        }

        void BeginDefense(RaidRecord raid, int index)
        {
            var def = Region.Get(raid.SiteId);
            var site = FindSite(raid.SiteId);
            if (def == null || site == null)
            {
                State.Raids.RemoveAt(index);
                return;
            }

            if (!site.Discovered)
            {
                site.Discovered = true;
                Report($"По следам набега найдено: «{def.Name}»");
            }

            raid.Scale = 1f + Region.RaidScalePerRaid * site.Raids;
            site.Raids++;
            raid.DefenderIds = new List<int>();
            for (var i = 0; i < State.Citizens.Count; i++)
            {
                var citizen = State.Citizens[i];
                if (citizen.OnExpedition || !citizen.IsHero && !IsGuard(citizen)) continue;
                raid.DefenderIds.Add(citizen.Id);
                citizen.OnExpedition = true;
                State.Citizens[i] = citizen;
            }

            var title = RaidTitle(def);
            if (raid.DefenderIds.Count == 0)
            {
                State.Raids.RemoveAt(index);
                Report($"{title} — город некому защищать");
                Pillage();
                return;
            }

            raid.Fighting = true;
            raid.Seed = Random(1000000000);
            activeRaids.Add(LaunchRaid(raid));
            Report($"{title}: враги у ворот, защитников {raid.DefenderIds.Count}");
        }

        ActiveRaid LaunchRaid(RaidRecord raid)
        {
            var def = Region.Get(raid.SiteId);
            var defenders = new List<Combatant>();
            foreach (var id in raid.DefenderIds)
            {
                var index = CitizenIndex(id);
                if (index < 0) continue;
                var citizen = State.Citizens[index];
                defenders.Add(citizen.IsHero
                    ? HeroCombatant(citizen)
                    : Combatant.FromHero("Стражник " + CitizenInfo.FullName(citizen), HeroClass.Barbarian, 1));
            }
            var foes = new List<Combatant>();
            foreach (var kind in def.RaidEnemies) foes.Add(Combatant.FromEnemy(kind, raid.Scale));
            ApplyBonds(defenders, raid.DefenderIds);
            var active = new ActiveRaid { Record = raid, Defenders = defenders, Title = RaidTitle(def) };
            active.Battle = new Battle(defenders, foes, new Rng(raid.Seed), active.Log);
            return active;
        }

        void RestoreRaids()
        {
            foreach (var raid in State.Raids)
            {
                if (!raid.Fighting) continue;
                var active = LaunchRaid(raid);
                var steps = (int)Math.Round(raid.Elapsed / CombatData.Tick);
                for (var i = 0; i < steps && active.Battle.Outcome == BattleOutcome.Ongoing; i++) active.Battle.Step();
                activeRaids.Add(active);
            }
        }

        void StepRaids(int steps)
        {
            for (var r = activeRaids.Count - 1; r >= 0; r--)
            {
                var active = activeRaids[r];
                var outcome = active.Battle.Outcome;
                for (var i = 0; i < steps && outcome == BattleOutcome.Ongoing; i++) outcome = active.Battle.Step();
                active.Record.Elapsed = active.Battle.Time;
                if (outcome == BattleOutcome.Ongoing) continue;

                activeRaids.RemoveAt(r);
                State.Raids.Remove(active.Record);
                if (outcome == BattleOutcome.Won)
                {
                    Report($"{active.Title} — отбит! Защитников: {active.Defenders.Count}");
                    State.Fame++;
                }
                else Report($"{active.Title} — защитники не выстояли");
                ApplyDefenseResults(active.Defenders, active.Record.DefenderIds, outcome == BattleOutcome.Won ? active.Battle.XpPool : 0,
                    outcome == BattleOutcome.Won, active.Title);
                if (outcome != BattleOutcome.Won) Pillage();
            }
        }

        void ApplyDefenseResults(List<Combatant> defenders, List<int> ids, int xpPool, bool won, string title)
        {
            var survivors = 0;
            foreach (var fighter in defenders)
                if (!fighter.Dead)
                    survivors++;

            var citizens = State.Citizens;
            for (var i = 0; i < defenders.Count && i < ids.Count; i++)
            {
                var index = CitizenIndex(ids[i]);
                if (index < 0) continue;
                var citizen = citizens[index];
                var fighter = defenders[i];
                if (fighter.Dead)
                {
                    Report($"{CitizenInfo.FullName(citizen)} погиб(ла), защищая город");
                    if (citizen.IsHero)
                    {
                        Bury(citizen, "пал(а), защищая город");
                        ReturnGear(citizen.Id);
                    }
                    citizens.RemoveAt(index);
                    workforceDirty = true;
                    continue;
                }
                citizen.OnExpedition = false;
                if (citizen.IsHero)
                {
                    if (fighter.Injured && citizen.InjuryDays == 0) citizen.InjuryDays = SimConstants.InjuryDays;
                    if (survivors > 0) GrantHeroXp(ref citizen, (int)(xpPool / survivors * TraitXp(citizen)));
                    AddChronicle(citizen.Id, won ? $"отбил(а) набег — {title}" : $"не удержал(а) ворота — {title}");
                }
                citizens[index] = citizen;
            }
        }

        void Pillage()
        {
            var parts = new List<string>();
            foreach (var type in Plunder)
            {
                var amount = (int)Math.Floor(Stock[type] * Region.PillageShare);
                if (amount <= 0) continue;
                Stock.Take(type, amount);
                parts.Add($"{ResourceInfo.Name(type).ToLower()} {amount}");
            }
            Report(parts.Count > 0 ? "Город разграблен, унесли: " + string.Join(", ", parts) : "Враги ушли ни с чем: склады пусты");
        }
    }
}
