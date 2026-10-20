using System.Collections.Generic;
using AdventurerCity.Simulation;
using AdventurerCity.Simulation.Combat;
using UnityEngine;

namespace AdventurerCity.View
{
    public static class UiText
    {
        public static string Short(int value) => value >= 10000 ? $"{value / 1000}k" : value.ToString();

        public static string Signed(float value, bool colored)
        {
            var rounded = Mathf.RoundToInt(value);
            var text = rounded > 0 ? "+" + rounded : rounded.ToString();
            if (!colored || rounded == 0) return text;
            return rounded > 0 ? $"<color=#8BE28B>{text}</color>" : $"<color=#FF8A80>{text}</color>";
        }

        public static string Gray(string text) => $"<color=#AAAAAA>{text}</color>";
        public static string Red(string text) => $"<color=#FF8A80>{text}</color>";
        public static string Green(string text) => $"<color=#8BE28B>{text}</color>";
        public static string Check(string text, bool met) => met ? Green("✓ " + text) : Red("✗ " + text);

        public static string PerDay(ResourceAmount[] amounts, float cycles, string sign)
        {
            var parts = new List<string>();
            foreach (var amount in amounts)
                parts.Add($"{sign}{cycles * amount.Amount:0.#} {ResourceInfo.Name(amount.Type).ToLower()}");
            return string.Join(", ", parts);
        }

        public static string Breakdown(ResourceFlow flow)
        {
            var parts = new List<string>();
            foreach (var entry in flow.Sources)
                parts.Add($"<color=#8BE28B>{Entry(entry)} +{entry.PerDay:0.#}</color>");
            foreach (var entry in flow.Sinks)
                parts.Add($"<color=#FF8A80>{Entry(entry)} −{entry.PerDay:0.#}</color>");
            return parts.Count == 0 ? Gray("нет ни производства, ни расхода") : string.Join("  ·  ", parts);
        }

        static string Entry(FlowEntry entry) => entry.Count > 1 ? $"{entry.Label} ×{entry.Count}" : entry.Label;

        public static string RoleShort(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Tank: return "танк";
                case CombatRole.Healer: return "лекарь";
                case CombatRole.Support: return "поддерж.";
                case CombatRole.Melee: return "ближн.";
                default: return "дальн.";
            }
        }

        public static string ClassLine(in Citizen hero)
        {
            var line = Classes.Get(hero.Class).Name;
            foreach (var def in Classes.All)
                if (def.Class != hero.Class && Classes.HasClass(hero.ClassMask, def.Class))
                    line += "/" + def.Name;
            return line;
        }

        public static string RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return "#8FA8FF";
                case ItemRarity.Rare: return "#FFE36E";
                case ItemRarity.Unique: return "#FF9A3C";
                default: return "#DDDDDD";
            }
        }

        public static string GearMarks(World world, int heroId)
        {
            var marks = "";
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                var item = world.Equipped(heroId, slot);
                marks += item == null ? "<color=#555555>■</color>" : $"<color={RarityColor(item.Rarity)}>■</color>";
            }
            return marks;
        }

        public static string ItemTitle(Item item) =>
            $"<color={RarityColor(item.Rarity)}><b>{item.Name}</b></color> <color=#999999>({Items.RarityName(item.Rarity)}, ур. предмета {item.ItemLevel}" +
            (item.Rarity == ItemRarity.Normal ? "" : $", {Items.BaseName(Items.Base(item.BaseId), item.BaseTier).ToLower()}") + ")</color>";

        public static string PhaseText(Expedition run)
        {
            switch (run.Phase)
            {
                case ExpeditionPhase.TravelOut: return "В пути к данжу";
                case ExpeditionPhase.Room:
                    var affix = Dungeons.AffixName(run.Affix);
                    var room = $"Комната {run.RoomIndex + 1}/{run.Rooms.Count}: {run.Rooms[run.RoomIndex].Name}" +
                               (affix != null ? $" <color=#FFB74D>({affix})</color>" : "");
                    return run.Battle != null ? room + " — <color=#FF8A80>бой</color>" : room;
                case ExpeditionPhase.Rest: return "Привал между комнатами";
                case ExpeditionPhase.TravelBack: return run.Cleared ? "Возвращается с победой" : "Возвращается в город";
                default: return "Вернулся";
            }
        }

        public static void AddPeople(World world, List<string> lines, int buildingId, bool residents)
        {
            const int maxShown = 8;
            var shown = 0;
            var total = 0;
            foreach (var citizen in world.Citizens)
            {
                if ((residents ? citizen.HomeId : citizen.WorkId) != buildingId) continue;
                total++;
                if (shown == maxShown) continue;
                shown++;
                var role = residents ? JobOf(world, citizen) : "";
                lines.Add($"· {CitizenInfo.FullName(citizen)}, {world.AgeOf(citizen)} — {role}{CitizenInfo.ActivityName(world.ActivityOf(citizen)).ToLower()}");
            }
            if (total > shown) lines.Add($"· и ещё {total - shown}");
        }

        static string JobOf(World world, in Citizen citizen)
        {
            if (!world.IsAdult(citizen)) return "ребёнок, ";
            var work = world.GetBuilding(citizen.WorkId);
            return work == null ? "без работы, " : world.Def(work).Name.ToLower() + ", ";
        }

        public static Color HpColor(Combatant unit) =>
            !unit.IsHero ? new Color(0.75f, 0.3f, 0.3f)
            : unit.HpRatio > 0.5f ? new Color(0.35f, 0.75f, 0.35f)
            : unit.HpRatio > 0.25f ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.9f, 0.3f, 0.25f);
    }
}
