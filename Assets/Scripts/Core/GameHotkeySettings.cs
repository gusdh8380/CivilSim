using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CivilSim.Core
{
    public enum GameHotkeyAction
    {
        ToggleBuildingPanel,
        ToggleRoadMode,
        ToggleFoundationMode,
        ToggleZoneMode,
        ZoneResidential,
        ZoneCommercial,
        ZoneIndustrial,
        ZoneClear,
        RotateBuilding
    }

    /// <summary>
    /// 런타임 단축키 설정 저장소.
    /// PlayerPrefs를 사용해 키 바인딩을 저장한다.
    /// </summary>
    public static class GameHotkeySettings
    {
        private const string PrefPrefix = "CivilSim.Hotkey.";

        public static event Action Changed;

        public static Key GetKey(GameHotkeyAction action)
        {
            Key fallback = GetDefaultKey(action);
            int raw = PlayerPrefs.GetInt(GetPrefKey(action), (int)fallback);
            if (!Enum.IsDefined(typeof(Key), raw))
                return fallback;
            return (Key)raw;
        }

        public static void SetKey(GameHotkeyAction action, Key key)
        {
            if (key == Key.None) return;
            PlayerPrefs.SetInt(GetPrefKey(action), (int)key);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            foreach (GameHotkeyAction action in Enum.GetValues(typeof(GameHotkeyAction)))
                PlayerPrefs.SetInt(GetPrefKey(action), (int)GetDefaultKey(action));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool WasPressedThisFrame(Keyboard keyboard, GameHotkeyAction action)
        {
            return WasPressedThisFrame(keyboard, GetKey(action));
        }

        public static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            if (keyboard == null || key == Key.None) return false;
            KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        public static bool TryGetPressedKey(Keyboard keyboard, out Key pressed)
        {
            pressed = Key.None;
            if (keyboard == null) return false;

            foreach (Key key in Enum.GetValues(typeof(Key)))
            {
                if (key == Key.None) continue;
                KeyControl control = keyboard[key];
                if (control != null && control.wasPressedThisFrame)
                {
                    pressed = key;
                    return true;
                }
            }

            return false;
        }

        public static string ToDisplayString(Key key)
        {
            return key.ToString().ToUpperInvariant();
        }

        private static string GetPrefKey(GameHotkeyAction action)
        {
            return PrefPrefix + action;
        }

        private static Key GetDefaultKey(GameHotkeyAction action)
        {
            switch (action)
            {
                case GameHotkeyAction.ToggleBuildingPanel: return Key.B;
                case GameHotkeyAction.ToggleRoadMode: return Key.F;
                case GameHotkeyAction.ToggleFoundationMode: return Key.G;
                case GameHotkeyAction.ToggleZoneMode: return Key.Z;
                case GameHotkeyAction.ZoneResidential: return Key.R;
                case GameHotkeyAction.ZoneCommercial: return Key.C;
                case GameHotkeyAction.ZoneIndustrial: return Key.I;
                case GameHotkeyAction.ZoneClear: return Key.X;
                case GameHotkeyAction.RotateBuilding: return Key.R;
                default: return Key.None;
            }
        }
    }
}
