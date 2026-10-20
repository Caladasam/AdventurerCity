using System;
using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        readonly List<float> forgeRates = new List<float>();
        readonly ItemStats statsBuffer = new ItemStats();

        public IReadOnlyList<Item> AllItems => State.Items;
        public IReadOnlyList<ForgeOrder> ForgeOrders => State.ForgeOrders;

        public static int RequiredLevel(Item item) => Math.Max(1, item.BaseTier);

        public static ItemSlot SlotOf(Item item) => Items.Base(item.BaseId).Slot;

        public Item FindItem(int itemId)
        {
            foreach (var item in State.Items)
                if (item.Id == itemId)
                    return item;
            return null;
        }

        public Item Equipped(int heroId, ItemSlot slot)
        {
            foreach (var item in State.Items)
                if (item.EquippedBy == heroId && SlotOf(item) == slot)
                    return item;
            return null;
        }

        public List<Item> Stash(ItemSlot? slot = null)
        {
            var list = new List<Item>();
            foreach (var item in State.Items)
                if (item.EquippedBy == 0 && (slot == null || SlotOf(item) == slot))
                    list.Add(item);
            list.Sort((a, b) => b.Rarity != a.Rarity ? b.Rarity.CompareTo(a.Rarity) : b.ItemLevel.CompareTo(a.ItemLevel));
            return list;
        }

        void EnsureItems()
        {
            if (State.Items == null) State.Items = new List<Item>();
            if (State.NextItemId <= 0) State.NextItemId = 1;
            for (var i = 0; i < State.Citizens.Count; i++)
            {
                var hero = State.Citizens[i];
                if (!hero.IsHero || hero.WeaponTier == 0 && hero.ArmorTier == 0) continue;
                if (hero.WeaponTier > 0) AddItem(Items.MakeBase(1, hero.WeaponTier, Items.BaseTierLevels[hero.WeaponTier]), false).EquippedBy = hero.Id;
                if (hero.ArmorTier > 0) AddItem(Items.MakeBase(12, hero.ArmorTier, Items.BaseTierLevels[hero.ArmorTier]), false).EquippedBy = hero.Id;
                hero.WeaponTier = 0;
                hero.ArmorTier = 0;
                State.Citizens[i] = hero;
            }
            foreach (var order in State.ForgeOrders)
                if (order.BaseId == 0)
                    order.BaseId = order.Slot == GearSlot.Weapon ? 1 : 12;
            if (State.ItemsVersion < Items.Version)
            {
                foreach (var item in State.Items) Items.Rescale(item);
                State.ItemsVersion = Items.Version;
            }
        }

        Item AddItem(Item item, bool report)
        {
            item.Id = State.NextItemId++;
            State.Items.Add(item);
            if (report) Report($"На склад гильдии: {item.Name} ({Items.RarityName(item.Rarity)})");
            return item;
        }

        public Combatant HeroCombatant(in Citizen hero)
        {
            var fighter = Combatant.FromHero(CitizenInfo.FullName(hero), hero.Class, hero.HeroLevel, hero.InjuryDays > 0, 0, 0,
                HeroAbilities(hero), HeroTitle(hero));
            ApplyItems(fighter, hero.Id);
            ApplyTraits(fighter, hero);
            return fighter;
        }

        void ApplyItems(Combatant fighter, int heroId)
        {
            Array.Clear(statsBuffer.Values, 0, statsBuffer.Values.Length);
            foreach (var item in State.Items)
                if (item.EquippedBy == heroId)
                    statsBuffer.Add(item);
            var s = statsBuffer;
            fighter.Weapon += s[StatKind.WeaponDamage];
            fighter.DamageBonus += s[StatKind.DamagePct];
            fighter.MaxHp = (fighter.MaxHp + s[StatKind.Health]) * (1f + s[StatKind.HealthPct]);
            fighter.Armor += (int)Math.Round(s[StatKind.Armor], MidpointRounding.AwayFromZero);
            fighter.HealPower *= 1f + s[StatKind.HealingPct];
            fighter.CritChance += s[StatKind.CritPct];
            fighter.DodgeChance += s[StatKind.DodgePct];
            fighter.AttackInterval /= 1f + s[StatKind.AttackSpeedPct];
            fighter.ManaRegen += s[StatKind.ManaRegen];
            fighter.CooldownReduction = Math.Min(0.5f, s[StatKind.CooldownPct]);
            fighter.Lifesteal = s[StatKind.Lifesteal];
            fighter.Thorns = s[StatKind.Thorns];
            fighter.Execute = s[StatKind.ExecutePct];
            fighter.Hp = fighter.MaxHp;
        }

        public bool CanEquip(int heroId, int itemId, out string reason)
        {
            var index = CitizenIndex(heroId);
            var item = FindItem(itemId);
            if (index < 0 || item == null || !State.Citizens[index].IsHero)
            {
                reason = "Нет героя или предмета";
                return false;
            }
            var hero = State.Citizens[index];
            if (hero.OnExpedition)
            {
                reason = "Герой не в городе";
                return false;
            }
            if (item.EquippedBy != 0 && item.EquippedBy != heroId)
            {
                reason = "Предмет на другом герое";
                return false;
            }
            if (hero.HeroLevel < RequiredLevel(item))
            {
                reason = $"Нужен {RequiredLevel(item)}-й уровень";
                return false;
            }
            reason = null;
            return true;
        }

        internal bool Equip(int heroId, int itemId, out string error)
        {
            if (!CanEquip(heroId, itemId, out error)) return false;
            var item = FindItem(itemId);
            var current = Equipped(heroId, SlotOf(item));
            if (current != null) current.EquippedBy = 0;
            item.EquippedBy = heroId;
            return true;
        }

        internal bool Unequip(int heroId, ItemSlot slot, out string error)
        {
            error = null;
            var index = CitizenIndex(heroId);
            if (index >= 0 && State.Citizens[index].OnExpedition)
            {
                error = "Герой не в городе";
                return false;
            }
            var item = Equipped(heroId, slot);
            if (item == null) return false;
            item.EquippedBy = 0;
            return true;
        }

        public static ResourceAmount[] SalvageYield(Item item)
        {
            switch (item.Rarity)
            {
                case ItemRarity.Unique: return new[] { new ResourceAmount(ResourceType.Gems, 3), new ResourceAmount(ResourceType.Gold, 60) };
                case ItemRarity.Rare: return new[] { new ResourceAmount(ResourceType.Ore, 4 + 2 * item.BaseTier), new ResourceAmount(ResourceType.Gems, 1) };
                case ItemRarity.Magic: return new[] { new ResourceAmount(ResourceType.Ore, 2 + 2 * item.BaseTier), new ResourceAmount(ResourceType.Gold, 15) };
                default: return new[] { new ResourceAmount(ResourceType.Ore, 1 + item.BaseTier) };
            }
        }

        internal bool Salvage(int itemId, out string error)
        {
            error = null;
            var item = FindItem(itemId);
            if (item == null) return false;
            if (item.EquippedBy != 0)
            {
                error = "Сначала снимите предмет с героя";
                return false;
            }
            State.Items.Remove(item);
            foreach (var amount in SalvageYield(item)) Stock.Add(amount.Type, amount.Amount);
            Report($"Разобрано: {item.Name} → {ResourceInfo.Format(SalvageYield(item))}");
            return true;
        }

        void ReturnGear(int heroId)
        {
            foreach (var item in State.Items)
                if (item.EquippedBy == heroId)
                    item.EquippedBy = 0;
        }

        void StoreDrops(Expedition run, IReadOnlyList<int> party)
        {
            foreach (var drop in run.Drops)
            {
                var item = AddItem(drop, true);
                if (item.Rarity != ItemRarity.Unique) continue;
                foreach (var id in party)
                    if (CitizenIndex(id) >= 0)
                        AddChronicle(id, $"добыл(а) уникальный предмет «{item.Name}»");
            }
        }

        public bool HasForge()
        {
            foreach (var building in State.Buildings)
                if (building.Kind == BuildingKind.Forge && building.IsComplete && building.IsConnected)
                    return true;
            return false;
        }

        public static ResourceAmount[] CraftCost(ItemSlot slot, int tier)
        {
            if (slot == ItemSlot.Trinket)
            {
                switch (tier)
                {
                    case 0: return new[] { new ResourceAmount(ResourceType.Gold, 30) };
                    case 1: return new[] { new ResourceAmount(ResourceType.Ore, 5), new ResourceAmount(ResourceType.Gold, 80) };
                    case 2: return new[] { new ResourceAmount(ResourceType.Ore, 10), new ResourceAmount(ResourceType.Gems, 2), new ResourceAmount(ResourceType.Gold, 150) };
                    default: return new[] { new ResourceAmount(ResourceType.Gems, 6), new ResourceAmount(ResourceType.Gold, 300) };
                }
            }
            switch (tier)
            {
                case 0: return new[] { new ResourceAmount(ResourceType.Planks, 5), new ResourceAmount(ResourceType.Gold, 10) };
                case 1: return new[] { new ResourceAmount(ResourceType.Ore, 10), new ResourceAmount(ResourceType.Planks, 10), new ResourceAmount(ResourceType.Gold, 60) };
                case 2: return new[] { new ResourceAmount(ResourceType.Ore, 25), new ResourceAmount(ResourceType.Planks, 15), new ResourceAmount(ResourceType.Gold, 150) };
                default: return new[] { new ResourceAmount(ResourceType.Ore, 40), new ResourceAmount(ResourceType.Gems, 5), new ResourceAmount(ResourceType.Gold, 300) };
            }
        }

        public static int ForgeTicks(ForgeOrder order) => order.UniqueId != 0 ? City.UniqueForgeTicks : new[] { 15, 30, 60, 90 }[Math.Min(order.Tier, Items.MaxBaseTier)];

        public bool CanCraft(int baseId, int tier, out string reason)
        {
            if (!HasForge())
            {
                reason = "Нужна достроенная кузница с дорогой к складу";
                return false;
            }
            var cost = CraftCost(Items.Base(baseId).Slot, tier);
            if (!Stock.CanAfford(cost))
            {
                reason = "Не хватает: " + Stock.DescribeMissing(cost);
                return false;
            }
            reason = null;
            return true;
        }

        internal bool Craft(int baseId, int tier, out string error)
        {
            if (!CanCraft(baseId, tier, out error)) return false;
            Stock.Spend(CraftCost(Items.Base(baseId).Slot, tier));
            State.ForgeOrders.Add(new ForgeOrder { BaseId = baseId, Tier = tier });
            workforceDirty = true;
            Report($"Кузница приняла заказ: {Items.BaseName(Items.Base(baseId), tier)}");
            return true;
        }

        void TickForge()
        {
            var orders = State.ForgeOrders;
            if (orders.Count == 0) return;

            forgeRates.Clear();
            foreach (var building in State.Buildings)
            {
                if (building.Kind != BuildingKind.Forge || !building.IsComplete || building.Workers == 0) continue;
                forgeRates.Add(building.Efficiency * building.Workers / Def(building).Workers);
            }

            for (var i = 0; i < forgeRates.Count && i < orders.Count; i++)
                orders[i].Progress += forgeRates[i];

            for (var i = orders.Count - 1; i >= 0; i--)
            {
                var order = orders[i];
                if (order.Progress < ForgeTicks(order)) continue;
                orders.RemoveAt(i);
                workforceDirty = true;
                var item = order.UniqueId != 0
                    ? AddItem(Items.MakeUnique(Items.Unique(order.UniqueId), City.UniqueItemLevel), false)
                    : AddItem(Items.MakeBase(order.BaseId, order.Tier, Items.BaseTierLevels[Math.Min(order.Tier, Items.MaxBaseTier)]), false);
                Report($"Кузница выковала: {item.Name} — лежит на складе гильдии");
            }
        }
    }
}
