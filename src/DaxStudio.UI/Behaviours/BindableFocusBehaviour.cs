using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace DaxStudio.UI.Behaviours
{


    public class BindableFocusBehavior : Behavior<Control>
    {
        public static readonly DependencyProperty HasFocusProperty =
            DependencyProperty.Register("HasFocus", typeof(bool), typeof(BindableFocusBehavior), new PropertyMetadata(default(bool), HasFocusUpdated));

        private static void HasFocusUpdated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BindableFocusBehavior)d).SetFocus();
            
            
        }
        /*
        void AssociatedObject_GotFocus(object sender, RoutedEventArgs e)
        {

            if (AssociatedObject.GetType() == typeof(ComboBox))
            {
                var comboBox = AssociatedObject as ComboBox;
                var textBox = comboBox.FindChild("PART_EditableTextBox", typeof(TextBox)) as TextBox;
                if (textBox == null) return;

                textBox.SelectAll();
            }
        }
        */
        public bool HasFocus
        {
            get { return (bool)GetValue(HasFocusProperty); }
            set { SetValue(HasFocusProperty, value); }
        }

        private void SetFocus()
        {
            if (HasFocus)
            {
                try
                {
                    AssociatedObject?.Focus();
                }
                catch 
                { 
                    // swallow any errors
                }

            }
        }
    }

    
}
