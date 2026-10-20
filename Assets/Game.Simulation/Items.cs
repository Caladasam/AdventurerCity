using System;
using System.Collections.Generic;
using System.Text;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public enum ItemSlot
    {
        Weapon,
        Armor,
        Trinket
    }

    public enum ItemRarity
    {
        Normal,
        Magic,
        Rare,
        Unique
    }

    public enum StatKind
    {
        WeaponDamage,
        DamagePct,
        Health,
        HealthPct,
        Armor,
        HealingPct,
        CritPct,
        DodgePct,
        AttackSpeedPct,
        ManaRegen,
        CooldownPct,
        Lifesteal,
        Thorns,
        ExecutePct
    }

    [Serializable]
    public sealed class ItemMod
    {
        public StatKind Stat;
        public float Value;
        public int AffixId;
        public int Tier;
        public bool Implicit;
    }

    [Serializable]
    public sealed class Item
    {
        public int Id;
        public int BaseId;
        public int BaseTier;
        public ItemRarity Rarity;
        public int ItemLevel;
        public string Name;
        public int UniqueId;
        public int EquippedBy;
        public List<ItemMod> Mods = new List<ItemMod>();
    }

    public sealed class ItemBase
    {
        public int Id;
        public string Noun;
        public char Gender;
        public ItemSlot Slot;
        public StatKind Main = StatKind.WeaponDamage;
        public float[] MainValues = new float[0];
        public StatKind Implicit;
        public float[] ImplicitValues;
    }

    public sealed class AffixDef
    {
        public int Id;
        public bool Prefix;
        public string Male;
        public string Female;
        public string Neuter;
        public StatKind Stat;
        public ItemSlot[] Slots;
        public float[] Min;
        public float[] Max;
    }

    public sealed class UniqueDef
    {
        public int Id;
        public string Name;
        public int BaseId;
        public int BaseTier;
        public EnemyKind Boss;
        public string Lore;
        public (StatKind Stat, float Value)[] Mods;
    }

    public sealed class ItemStats
    {
        public readonly float[] Values = new float[Enum.GetValues(typeof(StatKind)).Length];

        public float this[StatKind stat] => Values[(int)stat];

        public void Add(Item item)
        {
            foreach (var mod in item.Mods) Values[(int)mod.Stat] += mod.Value;
        }
    }

    public static class Items
    {
        public const int MaxBaseTier = 3;
        public const int Version = 1;
        const float OldAffixScale = 0.4f;
        public static readonly int[] BaseTierLevels = { 1, 3, 6, 9 };
        public static readonly int[] AffixTierLevels = { 1, 4, 7 };
        public const float CombatRoomDropChance = 0.25f;
        public const float BossUniqueChance = 0.15f;
        public const float HeroicUniqueChance = 0.3f;

        static readonly string[][] tierWords =
        {
            new[] { "Простой", "Простая", "Простое" },
            new[] { "Железный", "Железная", "Железное" },
            new[] { "Стальной", "Стальная", "Стальное" },
            new[] { "Мифриловый", "Мифриловая", "Мифриловое" }
        };

        static readonly ItemBase[] bases =
        {
            new ItemBase { Id = 1, Noun = "меч", Gender = 'm', Slot = ItemSlot.Weapon, MainValues = new[] { 0f, 0.5f, 1.5f, 2.5f }, Implicit = StatKind.DamagePct, ImplicitValues = new[] { 0.01f, 0.025f, 0.035f, 0.05f } },
            new ItemBase { Id = 2, Noun = "кинжал", Gender = 'm', Slot = ItemSlot.Weapon, MainValues = new[] { 0f, 0.5f, 1f, 2f }, Implicit = StatKind.CritPct, ImplicitValues = new[] { 0.005f, 0.015f, 0.02f, 0.03f } },
            new ItemBase { Id = 3, Noun = "посох", Gender = 'm', Slot = ItemSlot.Weapon, MainValues = new[] { 0f, 0f, 0.5f, 1f }, Implicit = StatKind.HealingPct, ImplicitValues = new[] { 0.02f, 0.1f, 0.2f, 0.3f } },
            new ItemBase { Id = 4, Noun = "лук", Gender = 'm', Slot = ItemSlot.Weapon, MainValues = new[] { 0f, 0.5f, 1f, 2f }, Implicit = StatKind.AttackSpeedPct, ImplicitValues = new[] { 0.01f, 0.025f, 0.035f, 0.05f } },
            new ItemBase { Id = 5, Noun = "жезл", Gender = 'm', Slot = ItemSlot.Weapon, MainValues = new[] { 0f, 0.5f, 1f, 1.5f }, Implicit = StatKind.CooldownPct, ImplicitValues = new[] { 0.01f, 0.025f, 0.035f, 0.045f } },
            new ItemBase { Id = 11, Noun = "кираса", Gender = 'f', Slot = ItemSlot.Armor, Main = StatKind.Armor, MainValues = new[] { 0.5f, 2.5f, 4.5f, 7f }, Implicit = StatKind.Health, ImplicitValues = new[] { 1.5f, 5f, 10f, 15f } },
            new ItemBase { Id = 12, Noun = "кольчуга", Gender = 'f', Slot = ItemSlot.Armor, Main = StatKind.Armor, MainValues = new[] { 0.5f, 1.5f, 3f, 4.5f }, Implicit = StatKind.HealthPct, ImplicitValues = new[] { 0.005f, 0.015f, 0.02f, 0.025f } },
            new ItemBase { Id = 13, Noun = "кожанка", Gender = 'f', Slot = ItemSlot.Armor, Main = StatKind.Armor, MainValues = new[] { 0f, 1f, 2f, 3f }, Implicit = StatKind.DodgePct, ImplicitValues = new[] { 0.005f, 0.015f, 0.02f, 0.025f } },
            new ItemBase { Id = 14, Noun = "мантия", Gender = 'f', Slot = ItemSlot.Armor, Main = StatKind.Armor, MainValues = new[] { 0f, 0.5f, 1f, 1.5f }, Implicit = StatKind.ManaRegen, ImplicitValues = new[] { 0.1f, 0.3f, 0.5f, 0.8f } },
            new ItemBase { Id = 21, Noun = "кольцо", Gender = 'n', Slot = ItemSlot.Trinket, Implicit = StatKind.CritPct, ImplicitValues = new[] { 0.005f, 0.01f, 0.015f, 0.02f } },
            new ItemBase { Id = 22, Noun = "амулет", Gender = 'm', Slot = ItemSlot.Trinket, Implicit = StatKind.HealthPct, ImplicitValues = new[] { 0.005f, 0.015f, 0.025f, 0.03f } },
            new ItemBase { Id = 23, Noun = "талисман", Gender = 'm', Slot = ItemSlot.Trinket, Implicit = StatKind.HealingPct, ImplicitValues = new[] { 0.01f, 0.03f, 0.04f, 0.05f } }
        };

        static readonly ItemSlot[] weapon = { ItemSlot.Weapon };
        static readonly ItemSlot[] armor = { ItemSlot.Armor };
        static readonly ItemSlot[] weaponTrinket = { ItemSlot.Weapon, ItemSlot.Trinket };
        static readonly ItemSlot[] armorTrinket = { ItemSlot.Armor, ItemSlot.Trinket };
        static readonly ItemSlot[] any = { ItemSlot.Weapon, ItemSlot.Armor, ItemSlot.Trinket };

        static readonly AffixDef[] affixes =
        {
            new AffixDef { Id = 1, Prefix = true, Male = "Острый", Female = "Острая", Neuter = "Острое", Stat = StatKind.WeaponDamage, Slots = weapon, Min = new[] { 0.5f, 1f, 2f }, Max = new[] { 1f, 1.5f, 3f } },
            new AffixDef { Id = 2, Prefix = true, Male = "Жестокий", Female = "Жестокая", Neuter = "Жестокое", Stat = StatKind.DamagePct, Slots = weaponTrinket, Min = new[] { 0.015f, 0.03f, 0.05f }, Max = new[] { 0.025f, 0.045f, 0.065f } },
            new AffixDef { Id = 3, Prefix = true, Male = "Крепкий", Female = "Крепкая", Neuter = "Крепкое", Stat = StatKind.Health, Slots = armorTrinket, Min = new[] { 3f, 6f, 10f }, Max = new[] { 5f, 9f, 14f } },
            new AffixDef { Id = 4, Prefix = true, Male = "Живучий", Female = "Живучая", Neuter = "Живучее", Stat = StatKind.HealthPct, Slots = armor, Min = new[] { 0.01f, 0.02f, 0.03f }, Max = new[] { 0.015f, 0.03f, 0.04f } },
            new AffixDef { Id = 5, Prefix = true, Male = "Укреплённый", Female = "Укреплённая", Neuter = "Укреплённое", Stat = StatKind.Armor, Slots = armor, Min = new[] { 0.5f, 1.5f, 3f }, Max = new[] { 1f, 2.5f, 4f } },
            new AffixDef { Id = 6, Prefix = true, Male = "Благословенный", Female = "Благословенная", Neuter = "Благословенное", Stat = StatKind.HealingPct, Slots = weaponTrinket, Min = new[] { 0.02f, 0.035f, 0.055f }, Max = new[] { 0.03f, 0.05f, 0.07f } },
            new AffixDef { Id = 11, Male = "меткости", Stat = StatKind.CritPct, Slots = weaponTrinket, Min = new[] { 0.005f, 0.01f, 0.02f }, Max = new[] { 0.01f, 0.015f, 0.025f } },
            new AffixDef { Id = 12, Male = "ловкости", Stat = StatKind.DodgePct, Slots = armorTrinket, Min = new[] { 0.005f, 0.01f, 0.02f }, Max = new[] { 0.01f, 0.015f, 0.025f } },
            new AffixDef { Id = 13, Male = "быстроты", Stat = StatKind.AttackSpeedPct, Slots = weaponTrinket, Min = new[] { 0.01f, 0.025f, 0.035f }, Max = new[] { 0.02f, 0.03f, 0.05f } },
            new AffixDef { Id = 14, Male = "мудрости", Stat = StatKind.ManaRegen, Slots = any, Min = new[] { 0.1f, 0.3f, 0.5f }, Max = new[] { 0.2f, 0.4f, 0.7f } },
            new AffixDef { Id = 15, Male = "сосредоточенности", Stat = StatKind.CooldownPct, Slots = weaponTrinket, Min = new[] { 0.01f, 0.015f, 0.03f }, Max = new[] { 0.015f, 0.025f, 0.035f } },
            new AffixDef { Id = 16, Male = "вампира", Stat = StatKind.Lifesteal, Slots = weapon, Min = new[] { 0.005f, 0.01f, 0.015f }, Max = new[] { 0.01f, 0.015f, 0.02f } },
            new AffixDef { Id = 17, Male = "шипов", Stat = StatKind.Thorns, Slots = armor, Min = new[] { 0.015f, 0.03f, 0.045f }, Max = new[] { 0.025f, 0.04f, 0.06f } }
        };

        static readonly UniqueDef[] uniques =
        {
            new UniqueDef
            {
                Id = 1, Name = "Кирка бригадира", BaseId = 1, BaseTier = 1, Boss = EnemyKind.Grok,
                Lore = "Грок пробивал ей своды. И головы.",
                Mods = new[] { (StatKind.WeaponDamage, 2f), (StatKind.DamagePct, 0.1f), (StatKind.ExecutePct, 0.15f) }
            },
            new UniqueDef
            {
                Id = 2, Name = "Корона склепа", BaseId = 22, BaseTier = 2, Boss = EnemyKind.CryptKeeper,
                Lore = "Холодная, но голова от неё не остывает.",
                Mods = new[] { (StatKind.HealthPct, 0.06f), (StatKind.Lifesteal, 0.03f), (StatKind.ManaRegen, 0.5f) }
            },
            new UniqueDef
            {
                Id = 3, Name = "Шкура вожака", BaseId = 13, BaseTier = 1, Boss = EnemyKind.PackLeader,
                Lore = "Пахнет волком. Волки это чуют.",
                Mods = new[] { (StatKind.Armor, 2f), (StatKind.DodgePct, 0.04f), (StatKind.Thorns, 0.075f) }
            },
            new UniqueDef
            {
                Id = 4, Name = "Сабля атамана", BaseId = 2, BaseTier = 1, Boss = EnemyKind.Chieftain,
                Lore = "Добивает тех, кто уже не встанет.",
                Mods = new[] { (StatKind.WeaponDamage, 1.5f), (StatKind.CritPct, 0.04f), (StatKind.ExecutePct, 0.2f) }
            }
        };

        static readonly string[] rareFirst = { "Кровавый", "Мрачный", "Грозовой", "Пепельный", "Ледяной", "Вороний", "Багровый", "Пустой", "Гнилой", "Ржавый" };
        static readonly string[] rareWeapon = { "клык", "жнец", "удар", "коготь", "приговор" };
        static readonly string[] rareArmor = { "панцирь", "оплот", "покров", "кожух" };
        static readonly string[] rareTrinket = { "знак", "глаз", "узел", "оберег" };

        public static IReadOnlyList<ItemBase> Bases => bases;
        public static IReadOnlyList<UniqueDef> Uniques => uniques;

        public static ItemBase Base(int id)
        {
            foreach (var b in bases)
                if (b.Id == id)
                    return b;
            return bases[0];
        }

        public static AffixDef Affix(int id)
        {
            foreach (var a in affixes)
                if (a.Id == id)
                    return a;
            return null;
        }

        public static UniqueDef Unique(int id)
        {
            foreach (var u in uniques)
                if (u.Id == id)
                    return u;
            return null;
        }

        public static string BaseName(ItemBase b, int tier)
        {
            var words = tierWords[Math.Min(tier, MaxBaseTier)];
            var adjective = b.Gender == 'f' ? words[1] : b.Gender == 'n' ? words[2] : words[0];
            return adjective + " " + b.Noun;
        }

        public static string RarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return "магический";
                case ItemRarity.Rare: return "редкий";
                case ItemRarity.Unique: return "уникальный";
                default: return "обычный";
            }
        }

        public static string SlotName(ItemSlot slot) => slot == ItemSlot.Weapon ? "Оружие" : slot == ItemSlot.Armor ? "Броня" : "Украшение";

        public static string StatText(StatKind stat, float value)
        {
            switch (stat)
            {
                case StatKind.WeaponDamage: return $"+{value:0.#} к урону оружия";
                case StatKind.DamagePct: return $"+{value * 100f:0.#}% урона";
                case StatKind.Health: return $"+{value:0} здоровья";
                case StatKind.HealthPct: return $"+{value * 100f:0.#}% здоровья";
                case StatKind.Armor: return $"+{value:0.#} брони";
                case StatKind.HealingPct: return $"+{value * 100f:0.#}% силы лечения";
                case StatKind.CritPct: return $"+{value * 100f:0.#}% шанса крита";
                case StatKind.DodgePct: return $"+{value * 100f:0.#}% уклонения";
                case StatKind.AttackSpeedPct: return $"+{value * 100f:0.#}% скорости атаки";
                case StatKind.ManaRegen: return $"+{value:0.#} маны в секунду";
                case StatKind.CooldownPct: return $"−{value * 100f:0.#}% перезарядки навыков";
                case StatKind.Lifesteal: return $"{value * 100f:0.#}% урона возвращается здоровьем";
                case StatKind.Thorns: return $"{value * 100f:0.#}% полученного урона отражается во врага";
                default: return $"+{value * 100f:0}% урона по врагам с здоровьем ниже 35%";
            }
        }

        public static string Describe(Item item)
        {
            var text = new StringBuilder();
            foreach (var mod in item.Mods)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(mod.Implicit ? "· " : "• ").Append(StatText(mod.Stat, mod.Value));
                if (mod.Tier > 0) text.Append($" <color=#888888>[T{mod.Tier}]</color>");
            }
            return text.ToString();
        }

        public static int ItemLevelFor(DungeonDef dungeon, bool heroic, bool boss) =>
            dungeon.ItemLevel + (heroic ? 3 : 0) + (boss ? 1 : 0);

        public static int BaseTierFor(int itemLevel, Rng rng)
        {
            var top = 0;
            for (var t = 0; t <= MaxBaseTier; t++)
                if (itemLevel >= BaseTierLevels[t])
                    top = t;
            return top > 0 && rng.Chance(0.3f) ? top - 1 : top;
        }

        public static Item MakeBase(int baseId, int tier, int itemLevel)
        {
            var b = Base(baseId);
            var item = new Item { BaseId = baseId, BaseTier = tier, ItemLevel = itemLevel, Rarity = ItemRarity.Normal, Name = BaseName(b, tier) };
            if (b.MainValues.Length > tier && b.MainValues[tier] > 0f)
                item.Mods.Add(new ItemMod { Stat = b.Main, Value = b.MainValues[tier], Implicit = true });
            item.Mods.Add(new ItemMod { Stat = b.Implicit, Value = b.ImplicitValues[tier], Implicit = true });
            return item;
        }

        public static Item MakeUnique(UniqueDef def, int itemLevel)
        {
            var item = MakeBase(def.BaseId, def.BaseTier, itemLevel);
            item.Rarity = ItemRarity.Unique;
            item.UniqueId = def.Id;
            item.Name = def.Name;
            foreach (var (stat, value) in def.Mods) item.Mods.Add(new ItemMod { Stat = stat, Value = value });
            return item;
        }

        public static void Rescale(Item item)
        {
            if (item.UniqueId != 0)
            {
                var unique = MakeUnique(Unique(item.UniqueId), item.ItemLevel);
                item.Mods = unique.Mods;
                return;
            }
            var fresh = MakeBase(item.BaseId, item.BaseTier, item.ItemLevel);
            item.Mods.RemoveAll(m => m.Implicit);
            item.Mods.InsertRange(0, fresh.Mods);
            foreach (var mod in item.Mods)
                if (!mod.Implicit)
                    mod.Value = RoundAffix(mod.Stat, mod.Value * OldAffixScale);
        }

        static float RoundAffix(StatKind stat, float value)
        {
            if (stat == StatKind.Health) return (float)Math.Round(value);
            if (stat == StatKind.WeaponDamage || stat == StatKind.Armor) return (float)Math.Round(value * 2f) / 2f;
            if (stat == StatKind.ManaRegen) return (float)Math.Round(value, 1);
            return (float)Math.Round(value, 3);
        }

        public static Item Roll(Rng rng, int itemLevel, bool boss, bool heroic, EnemyKind bossKind)
        {
            if (boss)
            {
                var chance = heroic ? HeroicUniqueChance : BossUniqueChance;
                foreach (var def in uniques)
                    if (def.Boss == bossKind && rng.Chance(chance))
                        return MakeUnique(def, itemLevel);
            }

            var baseDef = bases[rng.Range(0, bases.Length - 1)];
            var item = MakeBase(baseDef.Id, BaseTierFor(itemLevel, rng), itemLevel);
            var roll = rng.Value;
            var rareChance = boss ? 0.35f : 0.1f;
            var magicChance = boss ? 0.6f : 0.35f;
            if (roll < rareChance) item.Rarity = ItemRarity.Rare;
            else if (roll < rareChance + magicChance || boss) item.Rarity = ItemRarity.Magic;
            if (item.Rarity == ItemRarity.Normal) return item;

            var prefixes = 0;
            var suffixes = 0;
            int maxPrefixes, maxSuffixes, count;
            if (item.Rarity == ItemRarity.Magic)
            {
                maxPrefixes = 1;
                maxSuffixes = 1;
                count = rng.Range(1, 2);
            }
            else
            {
                maxPrefixes = 2;
                maxSuffixes = 2;
                count = rng.Range(3, 4);
            }

            AffixDef firstPrefix = null, firstSuffix = null;
            for (var attempt = 0; attempt < 40 && prefixes + suffixes < count; attempt++)
            {
                var affix = affixes[rng.Range(0, affixes.Length - 1)];
                if (Array.IndexOf(affix.Slots, baseDef.Slot) < 0) continue;
                if (affix.Prefix && prefixes >= maxPrefixes || !affix.Prefix && suffixes >= maxSuffixes) continue;
                if (item.Mods.Exists(m => m.AffixId == affix.Id)) continue;
                var tier = 0;
                for (var t = 0; t < AffixTierLevels.Length; t++)
                    if (itemLevel >= AffixTierLevels[t])
                        tier = t;
                if (tier > 0 && rng.Chance(0.35f)) tier--;
                var value = affix.Min[tier] + (affix.Max[tier] - affix.Min[tier]) * rng.Value;
                value = RoundAffix(affix.Stat, value);
                item.Mods.Add(new ItemMod { Stat = affix.Stat, Value = value, AffixId = affix.Id, Tier = AffixTierLevels.Length - tier });
                if (affix.Prefix)
                {
                    prefixes++;
                    firstPrefix = firstPrefix ?? affix;
                }
                else
                {
                    suffixes++;
                    firstSuffix = firstSuffix ?? affix;
                }
            }

            if (item.Rarity == ItemRarity.Magic)
            {
                var name = BaseName(baseDef, item.BaseTier);
                if (firstPrefix != null)
                {
                    var adjective = baseDef.Gender == 'f' ? firstPrefix.Female : baseDef.Gender == 'n' ? firstPrefix.Neuter : firstPrefix.Male;
                    name = adjective + " " + char.ToLower(name[0]) + name.Substring(1);
                }
                if (firstSuffix != null) name += " " + firstSuffix.Male;
                item.Name = name;
            }
            else
            {
                var second = baseDef.Slot == ItemSlot.Weapon ? rareWeapon : baseDef.Slot == ItemSlot.Armor ? rareArmor : rareTrinket;
                var first = rareFirst[rng.Range(0, rareFirst.Length - 1)];
                var noun = second[rng.Range(0, second.Length - 1)];
                item.Name = $"{first} {noun}";
            }
            return item;
        }
    }
}
