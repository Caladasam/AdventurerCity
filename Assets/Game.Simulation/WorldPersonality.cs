using System;
using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        public IReadOnlyList<FallenHero> Fallen => State.Fallen;

        void EnsurePersonalities()
        {
            if (State.Relations == null) State.Relations = new List<Relation>();
            if (State.Chronicle == null) State.Chronicle = new List<ChronicleEntry>();
            if (State.Fallen == null) State.Fallen = new List<FallenHero>();
            for (var i = 0; i < State.Citizens.Count; i++)
            {
                var citizen = State.Citizens[i];
                if (!citizen.IsHero || citizen.Trait1 != HeroTrait.None) continue;
                AssignTraits(ref citizen);
                State.Citizens[i] = citizen;
            }
        }

        void AssignTraits(ref Citizen hero)
        {
            var pool = Traits.All.Count - 2;
            hero.Trait1 = (HeroTrait)(1 + Random(pool));
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var second = (HeroTrait)(1 + Random(pool));
                if (Traits.Conflict(hero.Trait1, second)) continue;
                hero.Trait2 = second;
                break;
            }
        }

        public static void ApplyTraits(Combatant fighter, in Citizen hero)
        {
            foreach (var trait in Traits.Of(hero))
            {
                var def = Traits.Get(trait);
                fighter.DamageBonus += def.Damage;
                fighter.MaxHp *= 1f + def.Health;
                fighter.DodgeChance += def.Dodge;
                fighter.CritChance += def.Crit;
                fighter.HealPower *= 1f + def.Healing;
                fighter.DownedScale *= def.DownedScale;
            }
            fighter.Hp = fighter.MaxHp;
        }

        static float TraitXp(in Citizen hero)
        {
            var bonus = 1f;
            foreach (var trait in Traits.Of(hero)) bonus += Traits.Get(trait).Xp;
            return Math.Max(0.1f, bonus);
        }

        float LootBonus(IReadOnlyList<int> ids)
        {
            var bonus = 0f;
            foreach (var id in ids)
            {
                var index = CitizenIndex(id);
                if (index < 0) continue;
                foreach (var trait in Traits.Of(State.Citizens[index])) bonus += Traits.Get(trait).Loot;
            }
            return bonus;
        }

        public int RelationBetween(int a, int b)
        {
            var relation = FindRelation(a, b, false);
            return relation?.Value ?? 0;
        }

        Relation FindRelation(int a, int b, bool create)
        {
            if (a > b) (a, b) = (b, a);
            foreach (var relation in State.Relations)
                if (relation.A == a && relation.B == b)
                    return relation;
            if (!create) return null;
            var created = new Relation { A = a, B = b };
            State.Relations.Add(created);
            return created;
        }

        public List<(int HeroId, int Value)> RelationsOf(int heroId)
        {
            var list = new List<(int, int)>();
            foreach (var relation in State.Relations)
            {
                if (relation.Value == 0) continue;
                if (relation.A == heroId) list.Add((relation.B, relation.Value));
                else if (relation.B == heroId) list.Add((relation.A, relation.Value));
            }
            list.Sort((x, y) => Math.Abs(y.Item2).CompareTo(Math.Abs(x.Item2)));
            return list;
        }

        void ApplyBonds(List<Combatant> party, IReadOnlyList<int> ids)
        {
            for (var i = 0; i < party.Count && i < ids.Count; i++)
            for (var j = i + 1; j < party.Count && j < ids.Count; j++)
            {
                var value = RelationBetween(ids[i], ids[j]);
                var bonus = value >= Traits.FriendThreshold ? Traits.BondDamage : value <= Traits.RivalThreshold ? -Traits.BondDamage : 0f;
                party[i].DamageBonus += bonus;
                party[j].DamageBonus += bonus;
            }
        }

        void ShareExperience(List<int> survivors)
        {
            for (var i = 0; i < survivors.Count; i++)
            for (var j = i + 1; j < survivors.Count; j++)
            {
                var a = CitizenIndex(survivors[i]);
                var b = CitizenIndex(survivors[j]);
                if (a < 0 || b < 0 || !State.Citizens[a].IsHero || !State.Citizens[b].IsHero) continue;
                var first = State.Citizens[a];
                var second = State.Citizens[b];
                var delta = 1;
                if (Has(first, HeroTrait.Brave) && Has(second, HeroTrait.Coward) || Has(first, HeroTrait.Coward) && Has(second, HeroTrait.Brave)) delta = -2;
                else if (Has(first, HeroTrait.Greedy) && Has(second, HeroTrait.Greedy)) delta = -1;
                var relation = FindRelation(first.Id, second.Id, true);
                var before = relation.Value;
                relation.Value = Math.Max(-Traits.RelationMax, Math.Min(Traits.RelationMax, relation.Value + delta));
                if (before < Traits.FriendThreshold && relation.Value >= Traits.FriendThreshold)
                {
                    AddChronicle(first.Id, $"подружился(ась) с {CitizenInfo.FullName(second)}");
                    AddChronicle(second.Id, $"подружился(ась) с {CitizenInfo.FullName(first)}");
                    Report($"{CitizenInfo.FullName(first)} и {CitizenInfo.FullName(second)} стали друзьями: вместе бьют на 5% сильнее");
                }
                if (before > Traits.RivalThreshold && relation.Value <= Traits.RivalThreshold)
                {
                    AddChronicle(first.Id, $"рассорился(ась) с {CitizenInfo.FullName(second)}");
                    AddChronicle(second.Id, $"рассорился(ась) с {CitizenInfo.FullName(first)}");
                    Report($"{CitizenInfo.FullName(first)} и {CitizenInfo.FullName(second)} стали соперниками: вместе бьют на 5% слабее");
                }
            }
        }

        static bool Has(in Citizen hero, HeroTrait trait) => hero.Trait1 == trait || hero.Trait2 == trait || hero.Trait3 == trait;

        internal void AddChronicle(int heroId, string text)
        {
            State.Chronicle.Add(new ChronicleEntry { HeroId = heroId, Day = Day, Text = text });
            var count = 0;
            for (var i = State.Chronicle.Count - 1; i >= 0; i--)
            {
                if (State.Chronicle[i].HeroId != heroId) continue;
                count++;
                if (count > Traits.ChroniclePerHero) State.Chronicle.RemoveAt(i);
            }
        }

        public List<ChronicleEntry> ChronicleOf(int heroId)
        {
            var list = new List<ChronicleEntry>();
            foreach (var entry in State.Chronicle)
                if (entry.HeroId == heroId)
                    list.Add(entry);
            return list;
        }

        void Bury(in Citizen hero, string cause)
        {
            AddChronicle(hero.Id, cause);
            State.Fallen.Add(new FallenHero { Hero = hero, Day = Day, Cause = cause });
            foreach (var (otherId, value) in RelationsOf(hero.Id))
                if (value >= Traits.FriendThreshold)
                    AddChronicle(otherId, $"потерял(а) друга — {CitizenInfo.FullName(hero)}");
        }

        public Building FindTemple()
        {
            foreach (var building in State.Buildings)
                if (building.Kind == BuildingKind.Temple && building.IsComplete && building.IsConnected)
                    return building;
            return null;
        }

        public static int ResurrectGold(in Citizen hero) => Traits.ResurrectGoldPerLevel * hero.HeroLevel;

        public static int ResurrectOre(in Citizen hero) => Traits.ResurrectOrePerLevel * hero.HeroLevel;

        public bool CanResurrect(int heroId, out string reason)
        {
            var fallen = FindFallen(heroId);
            if (fallen == null)
            {
                reason = "Героя нет среди павших";
                return false;
            }
            if (FindTemple() == null)
            {
                reason = "Нужен достроенный храм с дорогой к складу";
                return false;
            }
            var gold = ResurrectGold(fallen.Hero);
            var ore = ResurrectOre(fallen.Hero);
            if (!Stock.Has(ResourceType.Gold, gold) || !Stock.Has(ResourceType.Ore, ore))
            {
                reason = $"Нужно {gold} золота и {ore} руды";
                return false;
            }
            reason = null;
            return true;
        }

        FallenHero FindFallen(int heroId)
        {
            foreach (var fallen in State.Fallen)
                if (fallen.Hero.Id == heroId)
                    return fallen;
            return null;
        }

        internal bool Resurrect(int heroId, out string error)
        {
            if (!CanResurrect(heroId, out error)) return false;
            var fallen = FindFallen(heroId);
            var hero = fallen.Hero;
            Stock.Take(ResourceType.Gold, ResurrectGold(hero));
            Stock.Take(ResourceType.Ore, ResurrectOre(hero));
            State.Fallen.Remove(fallen);

            hero.HeroLevel = Math.Max(1, hero.HeroLevel - 1);
            hero.HeroXp = 0;
            hero.InjuryDays = Traits.ResurrectInjuryDays;
            hero.OnExpedition = false;
            hero.Hungry = false;
            hero.TrainingClass = 0;
            hero.WorkId = 0;
            if (!Has(hero, HeroTrait.Deathtouched))
            {
                if (hero.Trait3 == HeroTrait.None) hero.Trait3 = HeroTrait.Deathtouched;
                else hero.Trait2 = HeroTrait.Deathtouched;
            }
            hero.HomeId = FindHomeFor(hero.HomeId);
            if (hero.HomeId == 0) hero.HomeId = MakeRoom(fallen.Hero.HomeId);
            State.Citizens.Add(hero);
            workforceDirty = true;
            AddChronicle(hero.Id, "воскрешён(а) в храме — уже не тот(та), что прежде");
            Report($"{CitizenInfo.FullName(hero)} воскрешён(а) в храме: {hero.HeroLevel} ур., {Traits.ResurrectInjuryDays} дн. восстанавливается");
            return true;
        }

        int FindHomeFor(int previous)
        {
            var old = GetBuilding(previous);
            if (old != null && old.Kind == BuildingKind.House && old.Residents < HouseLevel(old).Capacity) return previous;
            foreach (var building in State.Buildings)
                if (building.Kind == BuildingKind.House && building.IsComplete && building.Residents < HouseLevel(building).Capacity)
                    return building.Id;
            return 0;
        }

        int MakeRoom(int previous)
        {
            var house = GetBuilding(previous);
            if (house == null || house.Kind != BuildingKind.House || !house.IsComplete) house = null;
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var building in State.Buildings)
                {
                    if (house != null && building != house) continue;
                    if (building.Kind != BuildingKind.House || !building.IsComplete) continue;
                    for (var i = State.Citizens.Count - 1; i >= 0; i--)
                    {
                        var citizen = State.Citizens[i];
                        if (citizen.HomeId != building.Id || citizen.IsHero) continue;
                        State.Citizens.RemoveAt(i);
                        workforceDirty = true;
                        Report($"{CitizenInfo.FullName(citizen)} уступил(а) место в доме герою и покинул(а) город");
                        return building.Id;
                    }
                }
                house = null;
            }
            return 0;
        }

        void RehouseHeroes()
        {
            for (var i = 0; i < State.Citizens.Count; i++)
            {
                var hero = State.Citizens[i];
                if (!hero.IsHero) continue;
                var home = GetBuilding(hero.HomeId);
                if (home != null && home.Kind == BuildingKind.House) continue;
                var found = FindHomeFor(0);
                if (found != 0) GetBuilding(found).Residents++;
                else found = MakeRoom(0);
                if (found == 0) return;
                i = CitizenIndex(hero.Id);
                hero.HomeId = found;
                State.Citizens[i] = hero;
                workforceDirty = true;
                Report($"{CitizenInfo.FullName(hero)} получил(а) жильё");
            }
        }

        int DailyRecovery(in Citizen hero)
        {
            var recovery = 1;
            foreach (var trait in Traits.Of(hero)) recovery += Traits.Get(trait).InjuryRecovery;
            if (FindTemple() != null) recovery += Traits.TempleInjuryRecovery;
            return recovery;
        }

        static int TraitWage(in Citizen hero)
        {
            var wage = 0;
            foreach (var trait in Traits.Of(hero)) wage += Traits.Get(trait).Wage;
            return wage;
        }

        static int TraitTrainingDays(in Citizen hero)
        {
            var days = 0;
            foreach (var trait in Traits.Of(hero)) days += Traits.Get(trait).TrainingDays;
            return days;
        }
    }
}
