using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DaxStudio.Common;
using DaxStudio.UI.Validation;
using Serilog;

namespace DaxStudio.UI.Utils
{
    public class InputBindings
    {
        private readonly InputBindingCollection _inputBindings;
        private readonly Stack<KeyBinding> _stash;

        public InputBindings(Window bindingsOwner)
        {
            _inputBindings = bindingsOwner.InputBindings;
            _stash = new Stack<KeyBinding>();
        }

        public void RegisterCommands(IEnumerable<InputBindingCommand> inputBindingCommands)
        {
            foreach (var inputBindingCommand in inputBindingCommands)
            {
                if (!inputBindingCommand.IsValidHotkey)
                {
                    Log.Warning(Constants.LogMessageTemplate, nameof(InputBindings), nameof(RegisterCommands),
                        $"Skipping invalid hotkey '{inputBindingCommand.HotkeyText}': {inputBindingCommand.ValidationError}");
                    continue;
                }

                if (!HotkeyBindingValidator.IsSupportedForRegistration(inputBindingCommand.GestureKey, inputBindingCommand.GestureModifier))
                {
                    // Key.None means "no hotkey assigned", so this is expected when a hotkey is cleared.
                    if (inputBindingCommand.GestureKey != Key.None)
                    {
                        Log.Warning(Constants.LogMessageTemplate, nameof(InputBindings), nameof(RegisterCommands),
                            $"Skipping unsupported hotkey '{inputBindingCommand.GestureModifier} + {inputBindingCommand.GestureKey}'");
                    }
                    continue;
                }

                try
                {
                    var binding = new KeyBinding(inputBindingCommand, inputBindingCommand.GestureKey, inputBindingCommand.GestureModifier);

                    _stash.Push(binding);
                    _inputBindings.Add(binding);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, Constants.LogMessageTemplate, nameof(InputBindings), nameof(RegisterCommands),
                        $"Skipping hotkey '{inputBindingCommand.GestureModifier} + {inputBindingCommand.GestureKey}' due to registration error");
                }
            }
        }

        public void DeregisterCommands()
        {
            if (_inputBindings == null)
                return;

            foreach (var keyBinding in _stash)
                _inputBindings.Remove(keyBinding);

            _stash.Clear();
        }
    }
}
