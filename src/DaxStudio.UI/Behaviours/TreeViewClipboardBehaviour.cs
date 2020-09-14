using ADOTabular;
using ADOTabular.Interfaces;
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
    public class TreeViewClipboardBehavior : Behavior<TreeView>
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
                var btn = sender as Button;
            if (btn != null && keyboardFocus != null)
            {
            btn.CommandTarget = keyboardFocus;
            }

        }

        private void CopyCommandExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var item = AssociatedObject.SelectedItem as IADOTabularObject;
            if (item != null ) //&& item.CanCopyToClipboard)
            {
                System.Windows.Clipboard.SetText(item.DaxName);
                //item.CopyToClipboard();
                e.Handled = true;
            }
        }

        private void CopyCommandCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            var item = AssociatedObject.SelectedItem as IADOTabularObject;
            if (item != null)
            {
                //e.CanExecute = item.CanCopyToClipboard;
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void CutCommandExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var item = AssociatedObject.SelectedItem as IADOTabularObject;
            if (item != null ) //&& item.CanCutToClipboard)
            {
                //item.CutToClipboard();
                e.Handled = true;
            }
        }

        private void CutCommandCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            var item = AssociatedObject.SelectedItem as TreeViewItem;
            if (item != null)
            {
                // disable cut for metadata
                e.CanExecute = false;
                e.Handled = true;
            }
        }


        private void PasteCommandExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var item = AssociatedObject.SelectedItem as TreeViewItem;
            if (item != null) // && item.CanPasteFromClipboard)
            {
                //item.PasteFromClipboard();
                e.Handled = true;
            }
        }

        private void PasteCommandCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            var item = AssociatedObject.SelectedItem as TreeViewItem;
            if (item != null)
            {
                // paste is not supported
                e.CanExecute = false;
                e.Handled = true;
            }
        }
    }
}
