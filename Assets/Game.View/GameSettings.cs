using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdventurerCity.View
{
    public enum GameKey
    {
        CameraUp,
        CameraDown,
        CameraLeft,
        CameraRight,
        Rotate,
        Economy,
        Map,
        Pause,
        Speed1,
        Speed2,
        Speed3,
        QuickSave,
        QuickLoad
    }

    public static class GameSettings
    {
        static readonly Dictionary<GameKey, KeyCode> defaults = new Dictionary<GameKey, KeyCode>
        {
            [GameKey.CameraUp] = KeyCode.W,
            [GameKey.CameraDown] = KeyCode.S,
            [GameKey.CameraLeft] = KeyCode.A,
            [GameKey.CameraRight] = KeyCode.D,
            [GameKey.Rotate] = KeyCode.R,
            [GameKey.Economy] = KeyCode.Tab,
            [GameKey.Map] = KeyCode.M,
            [GameKey.Pause] = KeyCode.Space,
            [GameKey.Speed1] = KeyCode.Alpha1,
            [GameKey.Speed2] = KeyCode.Alpha2,
            [GameKey.Speed3] = KeyCode.Alpha3,
            [GameKey.QuickSave] = KeyCode.F5,
            [GameKey.QuickLoad] = KeyCode.F9
        };

        static readonly Dictionary<GameKey, KeyCode> keys = new Dictionary<GameKey, KeyCode>();
        static bool loaded;

        public static event Action Changed;

        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static bool Muted { get; private set; }
        public static float UiScale { get; private set; } = 1f;

        public static string KeyName(GameKey key)
        {
            switch (key)
            {
                case GameKey.CameraUp: return "Камера вверх";
                case GameKey.CameraDown: return "Камера вниз";
                case GameKey.CameraLeft: return "Камера влево";
                case GameKey.CameraRight: return "Камера вправо";
                case GameKey.Rotate: return "Повернуть здание";
                case GameKey.Economy: return "Экономика";
                case GameKey.Map: return "Карта региона";
                case GameKey.Pause: return "Пауза";
                case GameKey.Speed1: return "Скорость x1";
                case GameKey.Speed2: return "Скорость x2";
                case GameKey.Speed3: return "Скорость x3";
                case GameKey.QuickSave: return "Быстрое сохранение";
                default: return "Быстрая загрузка";
            }
        }

        public static string CodeName(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Space: return "Пробел";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Return: return "Enter";
                case KeyCode.Alpha0: case KeyCode.Alpha1: case KeyCode.Alpha2: case KeyCode.Alpha3: case KeyCode.Alpha4:
                case KeyCode.Alpha5: case KeyCode.Alpha6: case KeyCode.Alpha7: case KeyCode.Alpha8: case KeyCode.Alpha9:
                    return ((int)code - (int)KeyCode.Alpha0).ToString();
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                default: return code.ToString();
            }
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            MasterVolume = PlayerPrefs.GetFloat("volume.master", 1f);
            MusicVolume = PlayerPrefs.GetFloat("volume.music", 1f);
            SfxVolume = PlayerPrefs.GetFloat("volume.sfx", 1f);
            Muted = PlayerPrefs.GetInt("muted", 0) == 1;
            UiScale = PlayerPrefs.GetFloat("ui.scale", 1f);
            foreach (var pair in defaults)
                keys[pair.Key] = (KeyCode)PlayerPrefs.GetInt("key." + pair.Key, (int)pair.Value);
            Apply();
        }

        public static KeyCode Get(GameKey key)
        {
            Load();
            return keys[key];
        }

        public static bool Down(GameKey key) => Input.GetKeyDown(Get(key));

        public static bool Held(GameKey key) => Input.GetKey(Get(key));

        public static void Bind(GameKey key, KeyCode code)
        {
            Load();
            foreach (var other in new List<GameKey>(keys.Keys))
                if (other != key && keys[other] == code)
                {
                    keys[other] = keys[key];
                    PlayerPrefs.SetInt("key." + other, (int)keys[other]);
                }
            keys[key] = code;
            PlayerPrefs.SetInt("key." + key, (int)code);
            PlayerPrefs.Save();
        }

        public static void ResetKeys()
        {
            foreach (var pair in defaults)
            {
                keys[pair.Key] = pair.Value;
                PlayerPrefs.DeleteKey("key." + pair.Key);
            }
            PlayerPrefs.Save();
        }

        public static void SetVolumes(float master, float music, float sfx)
        {
            MasterVolume = Mathf.Clamp01(master);
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);
            PlayerPrefs.SetFloat("volume.master", MasterVolume);
            PlayerPrefs.SetFloat("volume.music", MusicVolume);
            PlayerPrefs.SetFloat("volume.sfx", SfxVolume);
            Apply();
        }

        public static void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt("muted", muted ? 1 : 0);
            Apply();
        }

        public static void SetUiScale(float scale)
        {
            UiScale = Mathf.Clamp(Mathf.Round(scale * 10f) / 10f, 0.7f, 1.5f);
            PlayerPrefs.SetFloat("ui.scale", UiScale);
            Apply();
        }

        public static IReadOnlyList<Resolution> Resolutions()
        {
            var list = new List<Resolution>();
            foreach (var resolution in Screen.resolutions)
                if (!list.Exists(r => r.width == resolution.width && r.height == resolution.height))
                    list.Add(resolution);
            return list;
        }

        public static void SetResolution(int width, int height, bool fullscreen)
        {
            Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerPrefs.SetInt("screen.width", width);
            PlayerPrefs.SetInt("screen.height", height);
            PlayerPrefs.SetInt("screen.fullscreen", fullscreen ? 1 : 0);
        }

        static void Apply()
        {
            AudioListener.volume = Muted ? 0f : MasterVolume;
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
