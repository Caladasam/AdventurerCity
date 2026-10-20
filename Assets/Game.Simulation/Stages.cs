namespace AdventurerCity.Simulation
{
    public sealed class StageDef
    {
        public int Number;
        public string Name;
        public int Population;
        public int GoodHouses;
        public int Clears;
        public int CatacombClears;
        public BuildingKind[] Buildings = new BuildingKind[0];
        public ResourceAmount[] Cost = new ResourceAmount[0];
        public float TensionScale = 1f;
        public int VisitorMaxLevel;
        public float CaravanScale = 1f;
        public string Unlocks;
    }

    public static class Stages
    {
        public const int Max = 3;

        static readonly StageDef[] stages =
        {
            new StageDef
            {
                Number = 1, Name = "Деревня", VisitorMaxLevel = 2,
                Unlocks = "дома до 2-го уровня, лесоруб, ферма, лесопилка, пекарня, гильдия, сторожевая башня; шахта и волчье логово"
            },
            new StageDef
            {
                Number = 2, Name = "Городок", Population = 30, Clears = 1, Buildings = new[] { BuildingKind.Guild, BuildingKind.Bakery },
                Cost = new[] { new ResourceAmount(ResourceType.Gold, 100), new ResourceAmount(ResourceType.Planks, 40) },
                TensionScale = 1.25f, VisitorMaxLevel = 3,
                Unlocks = "«Добротный дом», кузница, храм (лечение и зелья), таверна (странствующие авантюристы), рынок (караваны); разведка катакомб и лагеря разбойников. Логова злее: напряжение растёт на 25% быстрее"
            },
            new StageDef
            {
                Number = 3, Name = "Город", Population = 70, GoodHouses = 4, CatacombClears = 1,
                Buildings = new[] { BuildingKind.Tavern, BuildingKind.Market, BuildingKind.Temple },
                Cost = new[] { new ResourceAmount(ResourceType.Gold, 300), new ResourceAmount(ResourceType.Planks, 80), new ResourceAmount(ResourceType.Ore, 20), new ResourceAmount(ResourceType.Gems, 5) },
                TensionScale = 1.5f, VisitorMaxLevel = 5, CaravanScale = 1.5f,
                Unlocks = "«Особняк» (8 жильцов, 5 золота налога), авантюристы до 5-го уровня, караваны в полтора раза богаче. Набеги ещё чаще: напряжение +50%"
            }
        };

        public static StageDef Get(int number) => stages[System.Math.Max(1, System.Math.Min(Max, number)) - 1];
    }
}
