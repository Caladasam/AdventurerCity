using System;
using System.Collections.Generic;
using System.IO;
using AdventurerCity.Simulation;
using UnityEngine;

namespace AdventurerCity.View
{
    public sealed class SaveInfo
    {
        public string Slot;
        public string Title;
        public bool Exists;
        public bool Broken;
        public DateTime Time;
        public int Day;
        public string Stage;
        public int Population;
        public int Heroes;
        public bool Victory;
    }

    public static class SaveSystem
    {
        public const string AutoSlot = "auto";
        public const string QuickSlot = "quick";
        public static readonly string[] ManualSlots = { "slot1", "slot2", "slot3" };

        static string PathOf(string slot) => Path.Combine(Application.persistentDataPath, slot == AutoSlot ? "city.json" : slot + ".json");

        public static string TitleOf(string slot)
        {
            switch (slot)
            {
                case AutoSlot: return "Автосохранение";
                case QuickSlot: return "Быстрое (F5)";
                default: return "Слот " + slot.Substring(4);
            }
        }

        public static IEnumerable<string> AllSlots()
        {
            yield return AutoSlot;
            yield return QuickSlot;
            foreach (var slot in ManualSlots) yield return slot;
        }

        public static bool Save(World world, string slot)
        {
            var path = PathOf(slot);
            var temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(world.State));
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Не удалось сохранить {path}: {e.Message}");
                return false;
            }
        }

        public static World TryLoad(ContentLibrary content, string slot)
        {
            var path = PathOf(slot);
            return Read(path, content) ?? Read(path + ".bak", content);
        }

        public static bool Exists(string slot) => File.Exists(PathOf(slot)) || File.Exists(PathOf(slot) + ".bak");

        static WorldState ReadState(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var state = JsonUtility.FromJson<WorldState>(File.ReadAllText(path));
                if (state == null || state.Format != WorldState.CurrentFormat) return null;
                if (state.Stock == null || state.Stock.Length > ResourceInfo.Count || state.Width <= 0) return null;
                return state;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Не удалось прочитать сохранение {path}: {e.Message}");
                return null;
            }
        }

        static World Read(string path, ContentLibrary content)
        {
            var state = ReadState(path);
            if (state == null) return null;
            try
            {
                return new World(state, content);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Сохранение {path} повреждено: {e.Message}");
                return null;
            }
        }

        public static SaveInfo Info(string slot)
        {
            var path = PathOf(slot);
            var info = new SaveInfo { Slot = slot, Title = TitleOf(slot) };
            var source = File.Exists(path) ? path : path + ".bak";
            if (!File.Exists(source)) return info;
            info.Exists = true;
            info.Time = File.GetLastWriteTime(source);
            var state = ReadState(source);
            if (state == null)
            {
                info.Broken = true;
                return info;
            }
            info.Day = (int)(state.Tick / SimConstants.TicksPerDay) + 1;
            info.Stage = Stages.Get(Math.Max(1, state.Stage)).Name;
            foreach (var citizen in state.Citizens)
            {
                info.Population++;
                if (citizen.IsHero) info.Heroes++;
            }
            info.Victory = state.VictoryDay > 0;
            return info;
        }

        public static string Latest()
        {
            string best = null;
            var bestTime = DateTime.MinValue;
            foreach (var slot in AllSlots())
            {
                var path = PathOf(slot);
                if (!File.Exists(path)) continue;
                var time = File.GetLastWriteTime(path);
                if (time <= bestTime) continue;
                bestTime = time;
                best = slot;
            }
            return best;
        }
    }
}
