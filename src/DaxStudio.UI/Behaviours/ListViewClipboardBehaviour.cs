using DaxStudio.UI.Events;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace DaxStudio.UI.Behaviours
{
    class ListViewClipboardBehaviour:Behavior<ListView>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            CommandBinding CopyCommandBinding = new CommandBinding(
                ApplicationCommands.Copy,
                CopyCommandExecuted,
                CopyCommandCanExecute);
            AssociatedObject.CommandBindings.Add(CopyCommandBinding);
            CopyCommandBinding.PreviewCanExecute += CopyCommandBinding_PreviewCanExecute;

       
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
            var items = AssociatedObject.SelectedItems;
            
            if (items.Count > 0)
            {
                var sb = new StringBuilder();
                foreach(var item in items)
                {
                    var msg = item as OutputMessage;
                    sb.Append(msg.Start);
                    sb.Append('\t');
                    sb.Append(msg.DurationString);
                    sb.Append('\t');
                    sb.Append(msg.Text);
                    sb.Append('\n');
                }
                System.Windows.Clipboard.SetText(sb.ToString());
                e.Handled = true;
            }
                
        }

        private void CopyCommandCanExecute(object target, CanExecuteRoutedEventArgs e)
        {
            
            if (AssociatedObject.SelectedItems.Count >0 )
            {
                //e.CanExecute = item.CanCopyToClipboard;
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        /*
        private void CutCommandExecuted(object target, ExecutedRoutedEventArgs e)
        {
            var items = AssociatedObject.SelectedItems as IObservableCollection<OutputMessage>;
            if (items != null) //&& item.CanCutToClipboard)
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
         */
    }
}
