using DaxStudio.UI.Model;
using System;
using System.Windows.Input;

namespace DaxStudio.UI.Validation
{
    public static class HotkeyBindingValidator
    {
        public static bool TryValidate(string hotkeyText, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(hotkeyText)) return true;

            Hotkey hotkey;
            try
            {
                hotkey = new Hotkey(hotkeyText);
            }
            catch
            {
                message = $"Cannot set a hotkey for '{hotkeyText}'";
                return false;
            }

            return TryValidate(hotkey, hotkeyText, out message);
        }

        public static bool IsSupportedForRegistration(Key key, ModifierKeys modifiers)
        {
            if (key == Key.None) return false;
            if (modifiers == ModifierKeys.None) return IsFunctionKey(key);
            if (modifiers == ModifierKeys.Shift && IsLetterKey(key)) return false;
            return true;
        }

        private static bool TryValidate(Hotkey hotkey, string displayValue, out string message)
        {
            message = string.Empty;
            if (hotkey == null) return true;

            if (hotkey.Key == Key.None)
            {
                message = $"Cannot set a hotkey for '{displayValue}'";
                return false;
            }

            if (hotkey.Modifiers == ModifierKeys.None && !IsFunctionKey(hotkey.Key))
            {
                message = $"Cannot set a single character Hotkey '{displayValue}'";
                return false;
            }

            if (hotkey.Modifiers == ModifierKeys.Shift && IsLetterKey(hotkey.Key))
            {
                message = $"Cannot set a hotkey for '{displayValue}'";
                return false;
            }

            return true;
        }

        private static bool IsFunctionKey(Key key)
        {
            return key >= Key.F1 && key <= Key.F24;
        }

        private static bool IsLetterKey(Key key)
        {
            return key >= Key.A && key <= Key.Z;
        }
    }
}
