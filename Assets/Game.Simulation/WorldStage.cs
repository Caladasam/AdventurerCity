using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        public int Stage => State.Stage;
        public StageDef CurrentStage => Stages.Get(State.Stage);

        public bool IsUnlocked(BuildingKind kind) => Def(kind).Stage <= State.Stage;

        public BuildingDef Def(BuildingKind kind) => Content.Get(kind);

        public int MaxHouseLevel()
        {
            var levels = Content.HouseLevels;
            var max = 1;
            for (var i = 0; i < levels.Count; i++)
                if (levels[i].Stage <= State.Stage)
                    max = i + 1;
            return max;
        }

        public int TotalClears()
        {
            var clears = 0;
            foreach (var site in State.Sites) clears += site.TimesCleared;
            return clears;
        }

        int GoodHouses()
        {
            var count = 0;
            foreach (var building in State.Buildings)
                if (building.Kind == BuildingKind.House && building.IsComplete && building.Level >= 3)
                    count++;
            return count;
        }

        public List<(string Text, bool Met)> StageRequirements(StageDef next)
        {
            var list = new List<(string, bool)>();
            if (next.Population > 0) list.Add(($"Население {Population}/{next.Population}", Population >= next.Population));
            if (next.GoodHouses > 0) list.Add(($"«Добротных домов» {GoodHouses()}/{next.GoodHouses}", GoodHouses() >= next.GoodHouses));
            if (next.Clears > 0) list.Add(($"Зачисток данжей и логов {TotalClears()}/{next.Clears}", TotalClears() >= next.Clears));
            if (next.CatacombClears > 0)
            {
                var site = Region.ForDungeon(Dungeons.CatacombsId);
                var clears = site != null ? FindSite(site.Id)?.TimesCleared ?? 0 : 0;
                list.Add(($"Зачищены «{Dungeons.Get(Dungeons.CatacombsId).Name}»", clears >= next.CatacombClears));
            }
            foreach (var kind in next.Buildings)
                list.Add(($"Построено: {Def(kind).Name}", HasComplete(kind)));
            if (next.Cost.Length > 0) list.Add(("Казна: " + ResourceInfo.Format(next.Cost), Stock.CanAfford(next.Cost)));
            return list;
        }

        bool HasComplete(BuildingKind kind)
        {
            foreach (var building in State.Buildings)
                if (building.Kind == kind && building.IsComplete)
                    return true;
            return false;
        }

        public bool CanAdvanceStage(out string reason)
        {
            if (State.Stage >= Stages.Max)
            {
                reason = "Город достиг высшего этапа";
                return false;
            }
            foreach (var (text, met) in StageRequirements(Stages.Get(State.Stage + 1)))
                if (!met)
                {
                    reason = "Не выполнено: " + text;
                    return false;
                }
            reason = null;
            return true;
        }

        internal bool AdvanceStage(out string error)
        {
            if (!CanAdvanceStage(out error)) return false;
            var next = Stages.Get(State.Stage + 1);
            Stock.Spend(next.Cost);
            State.Stage = next.Number;
            Report($"Поселение стало: {next.Name}! Открыто: {next.Unlocks}");
            return true;
        }

        void EnsureStage()
        {
            if (State.Stage > 0) return;
            State.Stage = 1;
            var advanced = false;
            foreach (var building in State.Buildings)
                if (Def(building).Stage > 1 || building.Kind == BuildingKind.House && building.Level >= 3)
                    advanced = true;
            if (advanced || RequirementsMet(2)) State.Stage = 2;
            if (State.Stage == 2 && RequirementsMet(3)) State.Stage = 3;
        }

        bool RequirementsMet(int stage)
        {
            var def = Stages.Get(stage);
            foreach (var (text, met) in StageRequirements(def))
                if (!met && !text.StartsWith("Казна"))
                    return false;
            return true;
        }
    }
}
