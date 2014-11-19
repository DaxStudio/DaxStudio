using ADOTabular;
using Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace DaxStudio.UI.Behaviours
{
    public class RibbonClipboardBehavior : Behavior<ToggleButton>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            CommandBinding CopyCommandBinding = new CommandBinding(
                ApplicationCommands.Copy,
                CopyCommandExecuted,
                CopyCommandCanExecute );
            AssociatedObject.CommandBindings.Add(CopyCommandBinding);
            CopyCommandBinding.PreviewCanExecute += CopyCommandBinding_PreviewCanExecute;

            CommandBinding CutCommandBinding = new CommandBinding(
                ApplicationCommands.Cut,
                CutCommandExecuted,
                CutCommandCanExecute);
            AssociatedObject.CommandBindings.Add(CutCommandBinding);

            CommandBinding PasteCommandBinding = new CommandBinding(
                ApplicationCommands.Paste,
                PasteCommandExecuted,
                PasteCommandCanExecute);
            AssociatedObject.CommandBindings.Add(PasteCommandBinding);
        }

        private void CopyCommandBinding_PreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            IInputElement keyboardFocus = Keyboard.FocusedElement;
            var focusWindow = keyboardFocus as Window;
                var btn = sender as ToggleButton;
            if (btn != null && keyboardFocus != null && focusWindow == null)
            {
            //btn.CommandTarget = keyboardFocus;
            }

        }

        private void CopyCommandExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var item = AssociatedObject;
            
            if (item != null ) //&& item.CanCopyToClipboard)
            {
                IInputElement keyboardFocus = Keyboard.FocusedElement;
                ApplicationCommands.Copy.Execute(null, keyboardFocus);
                //item.CopyToClipboard();
                e.Handled = true;
            }
        }

        private void CopyCommandCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            var item = AssociatedObject;
            if (item != null)
            {
                IInputElement keyboardFocus = Keyboard.FocusedElement;
                //e.CanExecute = item.CanCopyToClipboard;
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void CutCommandExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var item = AssociatedObject;
            if (item != null ) //&& item.CanCutToClipboard)
            {
                //item.CutToClipboard();
                e.Handled = true;
            }
        }

        private void CutCommandCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            var item = AssociatedObject;
            if (item != null)
            {
                //e.CanExecute = item.CanCutToClipboard;
                e.Handled = true;
            }
        }


        private void PasteCommandExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var item = AssociatedObject;
            if (item != null) // && item.CanPasteFromClipboard)
            {
                //item.PasteFromClipboard();
                e.Handled = true;
            }
        }

        private void PasteCommandCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            var item = AssociatedObject;
            if (item != null)
            {
                //e.CanExecute = item.CanPasteFromClipboard;
                e.Handled = true;
            }
        }
    }
}
