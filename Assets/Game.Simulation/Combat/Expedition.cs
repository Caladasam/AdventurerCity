using System.Collections.Generic;

namespace AdventurerCity.Simulation.Combat
{
    public enum OrderKind
    {
        Retreat = 1,
        Focus = 2,
        Potion = 3,
        ClearFocus = 4
    }

    [System.Serializable]
    public sealed class CombatOrder
    {
        public int Step;
        public OrderKind Kind;
        public int Target;
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
        public const float RestSeconds = 10f;
        public const float RestRestore = 0.3f;
        public const float RetreatHpRatio = 0.4f;
        public const float PotionHeal = 0.5f;
        public const float PotionRevive = 0.3f;
        public const float AutoPotionHpRatio = 0.2f;

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
        public RoomAffix Affix { get; private set; }
        public List<Item> Drops { get; } = new List<Item>();
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
        public int Steps { get; private set; }
        public int Potions { get; set; }

        public string LootSummary => LootType == ResourceType.Gold || Loot == 0
            ? $"{Loot + Gold} золота"
            : $"{Loot} {ResourceInfo.Name(LootType).ToLower()}, {Gold} золота";
        public float EnemyScale => Heroic ? Dungeons.HeroicScale : 1f;
        public float PartyFactor => Party.Count / (float)Dungeons.StandardParty;
        public float EnemyDamageScale => EnemyScale * (float)System.Math.Pow(System.Math.Max(1f, PartyFactor), Dungeons.PartyDamageExponent);
        public float EnemyHpScale => EnemyScale * (float)System.Math.Pow(System.Math.Max(1f, PartyFactor), Dungeons.PartyHpExponent);

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
            Steps++;
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
                        else AutoPotions();
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

        void AutoPotions()
        {
            foreach (var hero in Party)
                if (Potions > 0 && hero.Active && hero.HpRatio < AutoPotionHpRatio)
                    Drink(hero);
        }

        bool Drink(Combatant hero)
        {
            if (Potions <= 0 || hero.Dead) return false;
            float healed;
            if (hero.Downed)
            {
                hero.Downed = false;
                hero.Hp = hero.MaxHp * PotionRevive;
                healed = hero.Hp;
            }
            else
            {
                healed = System.Math.Min(hero.MaxHp - hero.Hp, hero.MaxHp * PotionHeal);
                if (healed <= 0f) return false;
                hero.Hp += healed;
            }
            Potions--;
            if (Battle != null) Battle.DrinkPotion(hero, healed);
            else Write($"{hero.Name} пьёт зелье");
            return true;
        }

        public bool Apply(CombatOrder order, out string error)
        {
            error = null;
            if (IsDone || Phase == ExpeditionPhase.TravelBack)
            {
                error = "Отряд уже возвращается";
                return false;
            }
            switch (order.Kind)
            {
                case OrderKind.Retreat:
                    if (Battle != null) Battle.RetreatOrdered = true;
                    else
                    {
                        Retreated = true;
                        Write("Отряд поворачивает назад по приказу");
                        StartReturn();
                    }
                    return true;
                case OrderKind.Focus:
                    if (Battle == null || order.Target < 0 || order.Target >= Battle.Enemies.Count || !Battle.Enemies[order.Target].Active)
                    {
                        error = "Цели нет";
                        return false;
                    }
                    Battle.ForcedFocus = Battle.Enemies[order.Target];
                    return true;
                case OrderKind.ClearFocus:
                    if (Battle != null) Battle.ForcedFocus = null;
                    return true;
                case OrderKind.Potion:
                    if (Potions <= 0)
                    {
                        error = "Зелий не осталось";
                        return false;
                    }
                    if (order.Target < 0 || order.Target >= Party.Count || !Drink(Party[order.Target]))
                    {
                        error = "Зелье сейчас не поможет";
                        return false;
                    }
                    return true;
            }
            return false;
        }

        void EnterRoom()
        {
            Phase = ExpeditionPhase.Room;
            PhaseTime = 0f;
            var room = Rooms[RoomIndex];
            Affix = room.IsCombat ? RollAffix(room.Kind == RoomKind.Boss) : RoomAffix.None;
            Write("Комната: " + room.Name + (Affix != RoomAffix.None ? $" ({Dungeons.AffixName(Affix)})" : ""));

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
                    if (rng.Chance(0.5f)) Drop(room);
                    RoomDone();
                    return;
            }

            if (room.Kind == RoomKind.Boss) BossReached = true;
            var foes = new List<Combatant>();
            var damageScale = EnemyDamageScale * (Affix == RoomAffix.Fierce ? Dungeons.FierceDamage : 1f);
            var hpScale = EnemyHpScale * (Affix == RoomAffix.Tough ? Dungeons.ToughHp : Affix == RoomAffix.Weakened ? Dungeons.WeakenedHp : 1f);
            foreach (var kind in room.Enemies) foes.Add(Combatant.FromEnemy(kind, damageScale, hpScale));
            if (Affix == RoomAffix.Reinforced)
                foes.Add(Combatant.FromEnemy(room.Enemies[rng.Range(0, room.Enemies.Length - 1)], damageScale, hpScale));
            Battle = new Battle(Party, foes, rng, Log);
        }

        RoomAffix RollAffix(bool boss)
        {
            if (!rng.Chance(Dungeons.AffixChance)) return RoomAffix.None;
            var affix = (RoomAffix)rng.Range(1, 4);
            return boss && affix == RoomAffix.Reinforced ? RoomAffix.Weakened : affix;
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
                        hero.XpGained += (int)(Battle.XpPool * PartyFactor) / survivors;

            var room = Rooms[RoomIndex];
            Battle = null;
            switch (outcome)
            {
                case BattleOutcome.Won:
                    if (room.Kind == RoomKind.Boss) BossKilled = true;
                    AddLoot(room);
                    if (room.Kind == RoomKind.Boss || rng.Chance(Items.CombatRoomDropChance)) Drop(room);
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
                    Drops.Clear();
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
            Loot += (int)System.Math.Round(Roll(room.LootMin, room.LootMax) * multiplier * PartyFactor);
            Gold += (int)System.Math.Round(Roll(room.GoldMin, room.GoldMax) * multiplier * PartyFactor);
        }

        void Drop(RoomTemplate room)
        {
            var boss = room.Kind == RoomKind.Boss;
            var item = Items.Roll(rng, Items.ItemLevelFor(Dungeon, Heroic, boss), boss, Heroic, boss ? room.Enemies[0] : EnemyKind.Ratman);
            Drops.Add(item);
            Write($"Найдено: {item.Name} ({Items.RarityName(item.Rarity)})");
        }

        int Roll(int min, int max) => min == max ? min : rng.Range(min, max);

        void Write(string text) => Log?.Add($"[{Elapsed:0} с] {text}");
    }
}
