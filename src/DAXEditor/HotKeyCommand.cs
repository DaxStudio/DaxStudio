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
        
        public event EventHandler CanExecuteChanged;
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
