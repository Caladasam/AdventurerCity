using System;
using System.IO;
using AdventurerCity.Simulation;
using UnityEngine;

namespace AdventurerCity.View
{
    public static class SaveSystem
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "city.json");

        public static void Save(World world)
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(world.State));
        }

        public static World TryLoad(ContentLibrary content)
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                var state = JsonUtility.FromJson<WorldState>(File.ReadAllText(FilePath));
                if (state == null || state.Format != WorldState.CurrentFormat) return null;
                if (state.Stock == null || state.Stock.Length > ResourceInfo.Count || state.Width <= 0) return null;
                return new World(state, content);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Не удалось загрузить сохранение {FilePath}: {e.Message}");
                return null;
            }
        }
    }
}
