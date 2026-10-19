using System.Collections.Generic;

namespace AdventurerCity.Simulation.Combat
{
    public enum RoomType
    {
        RatNest,
        Dig,
        Camp,
        Cave,
        Stash,
        Boss
    }

    public enum ExpeditionPhase
    {
        TravelOut,
        Room,
        Rest,
        TravelBack,
        Done
    }

    public sealed class Expedition
    {
        public const float TravelSeconds = 20f;
        public const float RestSeconds = 10f;
        public const float RestRestore = 0.3f;
        public const float RetreatHpRatio = 0.4f;
        public const float TrapDamage = 15f;
        public const int RoomsBeforeBoss = 4;
        public const int MinCombatRooms = 3;

        static readonly RoomType[] pool = { RoomType.RatNest, RoomType.Dig, RoomType.Camp, RoomType.Cave, RoomType.Stash };
        static readonly string[] roomNames =
        {
            "Крысиное гнездо", "Забой", "Капище", "Просевший свод", "Заброшенная кладовая", "Логово бригадира"
        };

        readonly Rng rng;

        public Expedition(List<Combatant> party, int seed, List<string> log = null)
        {
            Party = party;
            Log = log;
            rng = new Rng(seed);
            Rooms = GenerateRooms(rng);
            Rooms.Add(RoomType.Boss);
        }

        public List<Combatant> Party { get; }
        public List<RoomType> Rooms { get; }
        public List<string> Log { get; }
        public ExpeditionPhase Phase { get; private set; }
        public float PhaseTime { get; private set; }
        public float Elapsed { get; private set; }
        public int RoomIndex { get; private set; }
        public Battle Battle { get; private set; }
        public int Ore { get; private set; }
        public int Gold { get; private set; }
        public bool Cleared { get; private set; }
        public bool Retreated { get; private set; }
        public bool Wiped { get; private set; }
        public bool BossReached { get; private set; }
        public bool BossKilled { get; private set; }
        public bool IsDone => Phase == ExpeditionPhase.Done;

        public int Deaths
        {
            get
            {
                var deaths = 0;
                foreach (var hero in Party)
                    if (hero.Dead)
                        deaths++;
                return deaths;
            }
        }

        public static string RoomName(RoomType room) => roomNames[(int)room];

        public static bool IsCombat(RoomType room) => room != RoomType.Cave && room != RoomType.Stash;

        public static List<RoomType> GenerateRooms(Rng rng)
        {
            while (true)
            {
                var rooms = new List<RoomType>();
                var combat = 0;
                for (var i = 0; i < RoomsBeforeBoss; i++)
                {
                    var room = pool[rng.Range(0, pool.Length - 1)];
                    rooms.Add(room);
                    if (IsCombat(room)) combat++;
                }
                if (combat >= MinCombatRooms) return rooms;
            }
        }

        public static Expedition RunToEnd(List<Combatant> party, int seed, List<string> log = null)
        {
            var expedition = new Expedition(party, seed, log);
            while (!expedition.IsDone) expedition.Step();
            return expedition;
        }

        public void Step()
        {
            if (IsDone) return;
            Elapsed += CombatData.Tick;
            PhaseTime += CombatData.Tick;

            switch (Phase)
            {
                case ExpeditionPhase.TravelOut:
                    if (PhaseTime >= TravelSeconds) EnterRoom();
                    break;
                case ExpeditionPhase.Room:
                    if (Battle != null)
                    {
                        var outcome = Battle.Step();
                        if (outcome != BattleOutcome.Ongoing) FinishBattle(outcome);
                    }
                    break;
                case ExpeditionPhase.Rest:
                    if (PhaseTime >= RestSeconds) FinishRest();
                    break;
                case ExpeditionPhase.TravelBack:
                    if (PhaseTime >= TravelSeconds)
                    {
                        Phase = ExpeditionPhase.Done;
                        Write("Отряд вернулся в город");
                    }
                    break;
            }
        }

        void EnterRoom()
        {
            Phase = ExpeditionPhase.Room;
            PhaseTime = 0f;
            var room = Rooms[RoomIndex];
            Write("Комната: " + RoomName(room));

            switch (room)
            {
                case RoomType.Cave:
                    foreach (var hero in Party)
                    {
                        if (!hero.Active || rng.Chance(hero.DodgeChance * 2f)) continue;
                        hero.Hp = System.Math.Max(1f, hero.Hp - TrapDamage);
                    }
                    Write("Свод осыпается, отряд выбирается из-под камней");
                    RoomDone();
                    return;
                case RoomType.Stash:
                    AddLoot(rng.Range(5, 10), rng.Range(20, 40));
                    RoomDone();
                    return;
            }

            if (room == RoomType.Boss) BossReached = true;
            Battle = new Battle(Party, Enemies(room), rng, Log);
        }

        static IEnumerable<Combatant> Enemies(RoomType room)
        {
            switch (room)
            {
                case RoomType.RatNest:
                    yield return Combatant.FromEnemy(EnemyKind.Ratman);
                    yield return Combatant.FromEnemy(EnemyKind.Ratman);
                    yield return Combatant.FromEnemy(EnemyKind.Ratman);
                    break;
                case RoomType.Dig:
                    yield return Combatant.FromEnemy(EnemyKind.Digger);
                    yield return Combatant.FromEnemy(EnemyKind.Digger);
                    yield return Combatant.FromEnemy(EnemyKind.Ratman);
                    break;
                case RoomType.Camp:
                    yield return Combatant.FromEnemy(EnemyKind.Digger);
                    yield return Combatant.FromEnemy(EnemyKind.Shaman);
                    break;
                case RoomType.Boss:
                    yield return Combatant.FromEnemy(EnemyKind.Grok);
                    break;
            }
        }

        void FinishBattle(BattleOutcome outcome)
        {
            var survivors = 0;
            foreach (var hero in Party)
                if (!hero.Dead)
                    survivors++;
            if (survivors > 0)
                foreach (var hero in Party)
                    if (!hero.Dead)
                        hero.XpGained += Battle.XpPool / survivors;

            var room = Rooms[RoomIndex];
            Battle = null;
            switch (outcome)
            {
                case BattleOutcome.Won:
                    switch (room)
                    {
                        case RoomType.RatNest: AddLoot(rng.Range(2, 4), rng.Range(10, 20)); break;
                        case RoomType.Boss:
                            BossKilled = true;
                            AddLoot(15, 100);
                            break;
                        default: AddLoot(rng.Range(3, 5), rng.Range(15, 25)); break;
                    }
                    RoomDone();
                    break;
                case BattleOutcome.Retreated:
                    Retreated = true;
                    StartReturn();
                    break;
                default:
                    Wiped = true;
                    Ore = 0;
                    Gold = 0;
                    Phase = ExpeditionPhase.Done;
                    break;
            }
        }

        void RoomDone()
        {
            RoomIndex++;
            if (RoomIndex >= Rooms.Count)
            {
                Cleared = true;
                Write("Шахта зачищена");
                StartReturn();
                return;
            }
            Phase = ExpeditionPhase.Rest;
            PhaseTime = 0f;
        }

        void FinishRest()
        {
            var hp = 0f;
            var maxHp = 0f;
            foreach (var hero in Party)
            {
                hero.RestoreFraction(RestRestore);
                maxHp += hero.MaxHp;
                if (hero.Active) hp += hero.Hp;
            }

            if (hp < maxHp * RetreatHpRatio)
            {
                Retreated = true;
                Write("Отряд слишком изранен и отступает");
                StartReturn();
                return;
            }
            EnterRoom();
        }

        void StartReturn()
        {
            Phase = ExpeditionPhase.TravelBack;
            PhaseTime = 0f;
        }

        void AddLoot(int ore, int gold)
        {
            Ore += ore;
            Gold += gold;
        }

        void Write(string text) => Log?.Add($"[{Elapsed:0} с] {text}");
    }
}
