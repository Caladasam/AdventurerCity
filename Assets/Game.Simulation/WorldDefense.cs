using System;
using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        static readonly ResourceType[] Plunder = { ResourceType.Gold, ResourceType.Bread, ResourceType.Grain, ResourceType.Planks };

        public IReadOnlyList<RaidRecord> Raids => State.Raids;
        public List<string> LastRaidLog { get; } = new List<string>();

        public RaidRecord RaidFrom(int siteId)
        {
            foreach (var raid in State.Raids)
                if (raid.SiteId == siteId)
                    return raid;
            return null;
        }

        public void CountDefenders(out int heroes, out int guards)
        {
            heroes = 0;
            guards = 0;
            foreach (var citizen in State.Citizens)
            {
                if (citizen.IsHero && !citizen.OnExpedition) heroes++;
                else if (IsGuard(citizen)) guards++;
            }
        }

        bool IsGuard(in Citizen citizen)
        {
            if (citizen.IsHero || citizen.WorkId == 0) return false;
            var work = GetBuilding(citizen.WorkId);
            return work != null && work.Kind == BuildingKind.Watchtower && work.IsComplete;
        }

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
            for (var i = raids.Count - 1; i >= 0; i--)
            {
                var raid = raids[i];
                raid.TicksLeft--;
                if (raid.TicksLeft > 0) continue;
                raids.RemoveAt(i);
                ResolveRaid(raid);
            }
        }

        void ResolveRaid(RaidRecord raid)
        {
            var def = Region.Get(raid.SiteId);
            var site = FindSite(raid.SiteId);
            if (def == null || site == null) return;

            if (!site.Discovered)
            {
                site.Discovered = true;
                Report($"По следам набега найдено: «{def.Name}»");
            }

            var scale = 1f + Region.RaidScalePerRaid * site.Raids;
            site.Raids++;
            var foes = new List<Combatant>();
            foreach (var kind in def.RaidEnemies) foes.Add(Combatant.FromEnemy(kind, scale));

            var defenders = new List<Combatant>();
            var defenderIds = new List<int>();
            foreach (var citizen in State.Citizens)
            {
                if (citizen.IsHero && !citizen.OnExpedition)
                {
                    defenders.Add(Combatant.FromHero(CitizenInfo.FullName(citizen), citizen.Class, citizen.HeroLevel, citizen.InjuryDays > 0,
                        citizen.WeaponTier, citizen.ArmorTier));
                    defenderIds.Add(citizen.Id);
                }
                else if (IsGuard(citizen))
                {
                    defenders.Add(Combatant.FromHero("Стражник " + CitizenInfo.FullName(citizen), HeroClass.Warrior, 1));
                    defenderIds.Add(citizen.Id);
                }
            }

            LastRaidLog.Clear();
            var title = def.Kind == SiteKind.Dungeon ? $"Монстры из «{def.Name}»" : $"Набег: «{def.Name}»";
            if (defenders.Count == 0)
            {
                Report($"{title} — город некому защищать");
                Pillage();
                return;
            }

            var battle = new Battle(defenders, foes, new Rng(Random(1000000000)), LastRaidLog);
            var outcome = BattleOutcome.Ongoing;
            while (outcome == BattleOutcome.Ongoing) outcome = battle.Step();

            if (outcome == BattleOutcome.Won) Report($"{title} — отбит! Защитников: {defenders.Count}");
            else Report($"{title} — защитники не выстояли");
            ApplyDefenseResults(defenders, defenderIds, outcome == BattleOutcome.Won ? battle.XpPool : 0);
            if (outcome != BattleOutcome.Won) Pillage();
        }

        void ApplyDefenseResults(List<Combatant> defenders, List<int> ids, int xpPool)
        {
            var survivors = 0;
            foreach (var fighter in defenders)
                if (!fighter.Dead)
                    survivors++;

            var citizens = State.Citizens;
            for (var i = 0; i < defenders.Count; i++)
            {
                var index = CitizenIndex(ids[i]);
                if (index < 0) continue;
                var citizen = citizens[index];
                var fighter = defenders[i];
                if (fighter.Dead)
                {
                    Report($"{CitizenInfo.FullName(citizen)} погиб(ла), защищая город");
                    citizens.RemoveAt(index);
                    workforceDirty = true;
                    continue;
                }
                if (!citizen.IsHero) continue;
                if (fighter.Injured && citizen.InjuryDays == 0) citizen.InjuryDays = SimConstants.InjuryDays;
                if (survivors > 0) GrantHeroXp(ref citizen, xpPool / survivors);
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
