using System.Collections.Generic;
using AdventurerCity.Simulation.Combat;

namespace AdventurerCity.Simulation
{
    public sealed partial class World
    {
        readonly List<float> forgeRates = new List<float>();

        public IReadOnlyList<ForgeOrder> ForgeOrders => State.ForgeOrders;

        public bool HasForge()
        {
            foreach (var building in State.Buildings)
                if (building.Kind == BuildingKind.Forge && building.IsComplete && building.IsConnected)
                    return true;
            return false;
        }

        public ForgeOrder PendingOrder(int heroId, GearSlot slot)
        {
            foreach (var order in State.ForgeOrders)
                if (order.HeroId == heroId && order.Slot == slot)
                    return order;
            return null;
        }

        public static int ForgeTicks(ForgeOrder order) => GearData.Tier(order.Tier).ForgeTicks;

        public bool CanOrderGear(int heroId, GearSlot slot, out int nextTier, out string reason)
        {
            nextTier = 0;
            var index = CitizenIndex(heroId);
            if (index < 0 || !State.Citizens[index].IsHero)
            {
                reason = "Героя нет";
                return false;
            }

            var hero = State.Citizens[index];
            nextTier = (slot == GearSlot.Weapon ? hero.WeaponTier : hero.ArmorTier) + 1;
            if (nextTier > GearData.MaxTier)
            {
                reason = "Уже лучшее снаряжение";
                return false;
            }
            if (!HasForge())
            {
                reason = "Нужна достроенная кузница с дорогой к складу";
                return false;
            }
            if (PendingOrder(heroId, slot) != null)
            {
                reason = "Заказ уже в работе";
                return false;
            }
            var cost = GearData.Cost(slot, nextTier);
            if (!Stock.CanAfford(cost))
            {
                reason = "Не хватает: " + Stock.DescribeMissing(cost);
                return false;
            }
            reason = null;
            return true;
        }

        internal bool OrderGear(int heroId, GearSlot slot, out string error)
        {
            if (!CanOrderGear(heroId, slot, out var tier, out error)) return false;

            Stock.Spend(GearData.Cost(slot, tier));
            State.ForgeOrders.Add(new ForgeOrder { HeroId = heroId, Slot = slot, Tier = tier });
            workforceDirty = true;
            var hero = State.Citizens[CitizenIndex(heroId)];
            Report($"Кузница приняла заказ: {GearData.Name(slot, tier)} для {CitizenInfo.FullName(hero)}");
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

                var index = CitizenIndex(order.HeroId);
                if (index < 0) continue;
                var hero = State.Citizens[index];
                if (order.Slot == GearSlot.Weapon) hero.WeaponTier = System.Math.Max(hero.WeaponTier, order.Tier);
                else hero.ArmorTier = System.Math.Max(hero.ArmorTier, order.Tier);
                State.Citizens[index] = hero;
                Report($"Кузница выковала: {GearData.Name(order.Slot, order.Tier)} для {CitizenInfo.FullName(hero)}");
            }
        }
    }
}
