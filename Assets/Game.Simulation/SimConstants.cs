namespace AdventurerCity.Simulation
{
    public static class SimConstants
    {
        public const int TicksPerDay = 30;
        public const int DaysPerYear = 30;
        public const int AdultAge = 16;
        public const int ImmigrantMinAge = 18;
        public const int ImmigrantMaxAge = 40;
        public const int BirthChancePercent = 10;
        public const int MapSize = 64;
        public const int FoodPerResident = 1;
        public const int FirewoodPerHouse = 1;
        public const int DaysToUpgrade = 3;
        public const int DaysToDowngrade = 3;
        public const int RoadGoldCost = 1;
        public const float LogisticsPenaltyPerCell = 0.05f;
        public const float MinLogisticsEfficiency = 0.5f;
        public const float DemolishRefund = 0.5f;
        public const int StockLimitStep = 10;
        public const int DefaultPlanksLimit = 150;
        public const float ShortageWarningDays = 3f;
        public const int HeroHireGold = 50;
        public const int HeroWageGold = 2;
        public const int ProvisionsPerHero = 2;
        public const int InjuryDays = 3;
        public const int MaxPartySize = 5;
        public const int HeroHirePlanks = 10;
        public const int HeroBreadPerDay = 1;
        public const int CombatStepsPerTick = 10;
    }
}
