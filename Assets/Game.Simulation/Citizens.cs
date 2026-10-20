using System;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    [Serializable]
    public struct Citizen
    {
        public int Id;
        public bool Female;
        public int FirstName;
        public int LastName;
        public int BornDay;
        public int HomeId;
        public int WorkId;
        public bool IsHero;
        public HeroClass Class;
        public int HeroLevel;
        public int HeroXp;
        public int InjuryDays;
        public bool OnExpedition;
        public bool Hungry;
        public int WeaponTier;
        public int ArmorTier;
        public int ClassMask;
        public Ability SlotA;
        public Ability SlotB;
        public int TrainingClass;
        public int TrainingDays;
    }

    public enum CitizenActivity
    {
        Sleeping,
        GoingToWork,
        Working,
        GoingHome,
        Leisure,
        Playing,
        Training,
        Adventuring
    }

    public static class CitizenInfo
    {
        static readonly string[] maleNames =
        {
            "Альрик", "Бранд", "Эдвин", "Гарет", "Тобиас", "Ульф", "Освальд", "Родрик", "Мартин", "Хальвар",
            "Бертольд", "Конрад", "Лорен", "Фенрик", "Ивар", "Седрик", "Тиль", "Арнольд", "Гуннар", "Вальтер"
        };

        static readonly string[] femaleNames =
        {
            "Эльза", "Марта", "Ильва", "Агата", "Рована", "Гретта", "Лиена", "Ода", "Хильда", "Мирна",
            "Сабина", "Ирма", "Фрея", "Берта", "Альма", "Нелла", "Ингрид", "Клара", "Ульрика", "Ада"
        };

        static readonly string[] lastNames =
        {
            "Мельник", "Кузнец", "Бочар", "Ткач", "Пастух", "Гончар", "Пахарь", "Дубняк", "Ольховец", "Каменщик",
            "Рыбак", "Ловчий", "Стрелок", "Пивовар", "Свечник", "Кожевник", "Бортник", "Плотник", "Ручейник", "Лесовик"
        };

        static readonly string[] activityNames = { "Спит", "Идёт на работу", "Работает", "Идёт домой", "Отдыхает", "Играет", "Тренируется", "В походе" };

        internal static int FirstNameCount(bool female) => female ? femaleNames.Length : maleNames.Length;

        internal static int LastNameCount => lastNames.Length;

        public static string FullName(in Citizen citizen)
        {
            var first = citizen.Female ? femaleNames[citizen.FirstName] : maleNames[citizen.FirstName];
            return first + " " + lastNames[citizen.LastName];
        }

        public static string ActivityName(CitizenActivity activity) => activityNames[(int)activity];
    }
}
