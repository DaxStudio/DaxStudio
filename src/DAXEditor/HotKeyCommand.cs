using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DAXEditorControl
{
    public class HotKeyCommand: ICommand
    {
#pragma warning disable CS0067 // this event is never used, but is required by the ICommand interface
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        Action action;

        /// <summary>
        /// Constructor
        /// </summary>
        public HotKeyCommand(Action hotkeyAction)
        {
            action = hotkeyAction;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            //       MessageBox.Show("HelloWorld");
            action?.Invoke();
        }
        
    }
}
