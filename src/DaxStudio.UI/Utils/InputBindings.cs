using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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
                var binding = new KeyBinding(inputBindingCommand, inputBindingCommand.GestureKey, inputBindingCommand.GestureModifier);

                _stash.Push(binding);
                _inputBindings.Add(binding);
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
