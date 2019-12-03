using DaxStudio.UI.Model;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Input;

namespace DaxStudio.UI.Utils
{


    public class InputBindingCommand : ICommand
    {
        
        // This event handler is not supported as part of the custom InputBinding
        public event EventHandler CanExecuteChanged
        {

            add { throw new NotSupportedException(); }

            remove { }

        }

        private readonly Action<object> _executeDelegate;
        private Func<object, bool> _canExecutePredicate;

        public Key GestureKey { get; set; }
        public ModifierKeys GestureModifier { get; set; }
        public MouseAction MouseGesture { get; set; }

        public bool CanExecute(object parameter)
        {
            return _canExecutePredicate(parameter);
        }

        public InputBindingCommand(object target, string methodName, string hotKey)
        {
            MethodInfo _methodInfo = target.GetType().GetMethod(methodName);
            Action myAction = (System.Action)Delegate.CreateDelegate(typeof(System.Action), target, _methodInfo);
            _executeDelegate = x => myAction();
            _canExecutePredicate = x => true;

            var hotkey = new Hotkey(hotKey);
            GestureKey = hotkey.Key;
            GestureModifier = hotkey.Modifiers;

            //ParseKeyString(hotKey);
        }

        public InputBindingCommand(Action executeDelegate)
        {
            _executeDelegate = x => _executeDelegate(x);
            //_canExecutePredicate = x => canExecuteDelegate(x);
            _canExecutePredicate = x => true;
        }

        public InputBindingCommand(Action<object> executeDelegate)
        {
            _executeDelegate = executeDelegate;
            _canExecutePredicate = x => true;
        }

        public void Execute(object parameter)
        {
            _executeDelegate(parameter);
        }

        public InputBindingCommand If(Func<bool> canExecutePredicate)
        {
            _canExecutePredicate = x => canExecutePredicate();

            return this;
        }

        public InputBindingCommand If(Func<object, bool> canExecutePredicate)
        {
            _canExecutePredicate = canExecutePredicate;

            return this;
        }

        //private  void ParseKeyString(string hotkey)
        //{
        //    string ksc = hotkey.ToLower();

        //    if (ksc.Contains("alt"))
        //        GestureModifier = ModifierKeys.Alt;
        //    if (ksc.Contains("shift"))
        //        GestureModifier |= ModifierKeys.Shift;
        //    if (ksc.Contains("ctrl") || ksc.Contains("ctl"))
        //        GestureModifier |= ModifierKeys.Control;

        //    string key =
        //        ksc.Replace("+", "")
        //            .Replace("-", "")
        //            .Replace("_", "")
        //            .Replace(" ", "")
        //            .Replace("alt", "")
        //            .Replace("shift", "")
        //            .Replace("ctrl", "")
        //            .Replace("ctl", "");

        //    key = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key);
        //    if (!string.IsNullOrEmpty(key))
        //    {
        //        KeyConverter k = new KeyConverter();
        //        GestureKey = (Key)k.ConvertFromString(key);
        //    }
        //}


    }
    
}
