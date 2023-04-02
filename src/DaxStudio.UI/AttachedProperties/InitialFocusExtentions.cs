using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DaxStudio.UI.Utils;


namespace DaxStudio.UI.AttachedProperties
{
    public static class InitialFocusExtentions
    {

        public static bool GetSelectAllAndFocus(DependencyObject obj)
        {
            return (bool)obj.GetValue(SelectAllAndFocusProperty);
        }

        public static void SetSelectAllAndFocus(DependencyObject obj, bool value)
        {
            obj.SetValue(SelectAllAndFocusProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectAllAndFocus.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectAllAndFocusProperty =
            DependencyProperty.RegisterAttached("SelectAllAndFocus", typeof(bool), typeof(InitialFocusExtentions), new UIPropertyMetadata(OnSelectAllAndFocusChanged));


        public static bool GetSelectAllWhenFocused(DependencyObject obj)
        {
            return (bool)obj.GetValue(SelectAllWhenFocusedProperty);
        }

        public static void SetSelectAllWhenFocused(DependencyObject obj, bool value)
        {
            obj.SetValue(SelectAllWhenFocusedProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectAllAndFocus.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectAllWhenFocusedProperty =
            DependencyProperty.RegisterAttached("SelectAllWhenFocused", typeof(bool), typeof(InitialFocusExtentions), new UIPropertyMetadata(OnSelectAllWhenFocusedChanged));


        public static void OnSelectAllAndFocusChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            bool SetProperty = (bool)args.NewValue;

            if (!SetProperty) return;

            
            if (obj is ComboBox)
            {

                var comboBox = obj as ComboBox;
                if (comboBox == null) return;

                if (SetProperty)
                {
                    comboBox.Focus();
                    GotFocused(comboBox, null);
                }
            }


            if (obj is TextBox)
            {
                var textBox = obj as TextBox;
                if (textBox == null) return;
                if (SetProperty)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Task.Delay(500);
                        textBox.Focus();
                        Debug.Write($"ap:IsKeyboardFocused: {textBox.IsKeyboardFocused} IsFocused: {textBox.IsFocused}");
                        GotFocused(textBox, null);
                    });
                }
            }
        }

        public static void GotFocused(ComboBox sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            if (comboBox.Items.Count == 0) return; // if there are no items do not attempt to do a selecte all

            var textBox = comboBox.FindChild("PART_EditableTextBox", typeof(TextBox)) as TextBox;
            if (textBox == null) return;

            textBox.SelectAll();
        }

        public static void GotFocused(TextBox sender, RoutedEventArgs e)
        {
            sender.SelectAll();
        }

        public static void OnSelectAllWhenFocusedChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            bool SetProperty = (bool)args.NewValue;

            if (obj is ComboBox)
            {

                var comboBox = obj as ComboBox;
                if (comboBox == null) return;

                if (SetProperty)
                {
                    comboBox.GotFocus += ComboBox_GotFocus;
                }
            }


            if (obj is TextBox)
            {
                var textBox = obj as TextBox;
                if (textBox == null) return;
                if (SetProperty)
                {
                    textBox.GotFocus += TextBox_GotFocus;
                }
            }
        }

        private static void ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            GotFocused((ComboBox)sender, e);
        }

        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            GotFocused((TextBox)sender, e);
        }
    }
}
