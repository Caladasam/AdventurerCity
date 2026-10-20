using System.Collections.Generic;

namespace AdventurerCity.Simulation.Combat
{
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
        public const float RestSeconds = 10f;
        public const float RestRestore = 0.3f;
        public const float RetreatHpRatio = 0.4f;

        readonly Rng rng;

        public Expedition(List<Combatant> party, int seed, DungeonDef dungeon, bool heroic, List<string> log = null)
        {
            Party = party;
            Dungeon = dungeon;
            Heroic = heroic;
            Log = log;
            rng = new Rng(seed);
            Rooms = GenerateRooms(dungeon, rng);
            Rooms.Add(dungeon.Boss);
        }

        public List<Combatant> Party { get; }
        public DungeonDef Dungeon { get; }
        public bool Heroic { get; }
        public List<RoomTemplate> Rooms { get; }
        public List<string> Log { get; }
        public ExpeditionPhase Phase { get; private set; }
        public float PhaseTime { get; private set; }
        public float Elapsed { get; private set; }
        public int RoomIndex { get; private set; }
        public Battle Battle { get; private set; }
        public int Loot { get; private set; }
        public int Gold { get; private set; }
        public ResourceType LootType => Dungeon.LootType;
        public bool Cleared { get; private set; }
        public bool Retreated { get; private set; }
        public bool Wiped { get; private set; }
        public bool BossReached { get; private set; }
        public bool BossKilled { get; private set; }
        public bool IsDone => Phase == ExpeditionPhase.Done;

        public string LootSummary => LootType == ResourceType.Gold || Loot == 0
            ? $"{Loot + Gold} золота"
            : $"{Loot} {ResourceInfo.Name(LootType).ToLower()}, {Gold} золота";
        public float EnemyScale => Heroic ? Dungeons.HeroicScale : 1f;

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

        public static List<RoomTemplate> GenerateRooms(DungeonDef dungeon, Rng rng)
        {
            var pool = dungeon.Pool;
            while (true)
            {
                var rooms = new List<RoomTemplate>();
                var combat = 0;
                for (var i = 0; i < dungeon.RoomsBeforeBoss; i++)
                {
                    var room = pool[rng.Range(0, pool.Length - 1)];
                    rooms.Add(room);
                    if (room.IsCombat) combat++;
                }
                if (combat >= dungeon.MinCombatRooms) return rooms;
            }
        }

        public static Expedition RunToEnd(List<Combatant> party, int seed, DungeonDef dungeon, bool heroic = false, List<string> log = null)
        {
            var expedition = new Expedition(party, seed, dungeon, heroic, log);
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
                    if (PhaseTime >= Dungeon.TravelSeconds) EnterRoom();
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
                    if (PhaseTime >= Dungeon.TravelSeconds)
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
            Write("Комната: " + room.Name);

            switch (room.Kind)
            {
                case RoomKind.Trap:
                    foreach (var hero in Party)
                    {
                        if (!hero.Active || rng.Chance(hero.DodgeChance * 2f)) continue;
                        hero.Hp = System.Math.Max(1f, hero.Hp - room.TrapDamage * EnemyScale);
                    }
                    Write("Ловушка! Отряд выбирается, потрёпанный");
                    RoomDone();
                    return;
                case RoomKind.Stash:
                    AddLoot(room);
                    RoomDone();
                    return;
            }

            if (room.Kind == RoomKind.Boss) BossReached = true;
            var foes = new List<Combatant>();
            foreach (var kind in room.Enemies) foes.Add(Combatant.FromEnemy(kind, EnemyScale));
            Battle = new Battle(Party, foes, rng, Log);
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
                    if (room.Kind == RoomKind.Boss) BossKilled = true;
                    AddLoot(room);
                    RoomDone();
                    break;
                case BattleOutcome.Retreated:
                    Retreated = true;
                    StartReturn();
                    break;
                default:
                    Wiped = true;
                    Loot = 0;
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
                Write($"{Dungeon.Name}: зачищено");
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

        void AddLoot(RoomTemplate room)
        {
            var multiplier = Heroic ? Dungeons.HeroicLootMultiplier : 1;
            Loot += Roll(room.LootMin, room.LootMax) * multiplier;
            Gold += Roll(room.GoldMin, room.GoldMax) * multiplier;
        }

        int Roll(int min, int max) => min == max ? min : rng.Range(min, max);

        void Write(string text) => Log?.Add($"[{Elapsed:0} с] {text}");
    }
}
