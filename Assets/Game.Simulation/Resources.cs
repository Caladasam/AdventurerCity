using System;
using System.Collections.Generic;
using System.Text;

namespace AdventurerCity.Simulation
{
    public enum ResourceType
    {
        Gold,
        Logs,
        Planks,
        Grain,
        Bread,
        Ore,
        Gems,
        Potions
    }

    [Serializable]
    public struct ResourceAmount
    {
        public ResourceType Type;
        public int Amount;

        public ResourceAmount(ResourceType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    public static class ResourceInfo
    {
        public const int Count = 8;

        static readonly string[] names = { "Золото", "Брёвна", "Доски", "Зерно", "Хлеб", "Руда", "Самоцветы", "Зелья" };

        public static string Name(ResourceType type) => names[(int)type];

        public static string Format(IReadOnlyList<ResourceAmount> amounts)
        {
            var sb = new StringBuilder();
            foreach (var amount in amounts)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(amount.Amount).Append(' ').Append(Name(amount.Type));
            }
            return sb.ToString();
        }
    }

    public sealed class Stockpile
    {
        readonly int[] amounts;

        public Stockpile(int[] amounts)
        {
            this.amounts = amounts;
        }

        public int this[ResourceType type] => amounts[(int)type];

        public bool Has(ResourceType type, int amount) => amounts[(int)type] >= amount;

        public void Add(ResourceType type, int amount) => amounts[(int)type] += amount;

        public int Take(ResourceType type, int amount)
        {
            var taken = Math.Min(amount, amounts[(int)type]);
            amounts[(int)type] -= taken;
            return taken;
        }

        public bool CanAfford(IReadOnlyList<ResourceAmount> cost)
        {
            foreach (var c in cost)
                if (!Has(c.Type, c.Amount))
                    return false;
            return true;
        }

        public void Spend(IReadOnlyList<ResourceAmount> cost)
        {
            foreach (var c in cost)
                amounts[(int)c.Type] -= c.Amount;
        }

        public void Refund(IReadOnlyList<ResourceAmount> cost, float fraction)
        {
            foreach (var c in cost)
                amounts[(int)c.Type] += (int)(c.Amount * fraction);
        }

        public string DescribeMissing(IReadOnlyList<ResourceAmount> cost)
        {
            var missing = new List<ResourceAmount>();
            foreach (var c in cost)
                if (!Has(c.Type, c.Amount))
                    missing.Add(new ResourceAmount(c.Type, c.Amount - amounts[(int)c.Type]));
            return ResourceInfo.Format(missing);
        }
    }
}
