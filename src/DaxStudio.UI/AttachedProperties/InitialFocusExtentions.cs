using System.Windows;
using System.Windows.Controls;
using DaxStudio.UI.Utils;
using WatermarkControlsLib.Controls;

namespace DaxStudio.UI.AttachedProperties
{
    public static class InitialFocusExtentions
    {

        public static bool GetSelectWhenFocused(DependencyObject obj)
        {
            return (bool)obj.GetValue(SelectWhenFocusedProperty);
        }

        public static void SetSelectWhenFocused(DependencyObject obj, bool value)
        {
            obj.SetValue(SelectWhenFocusedProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectWhenFocused.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectWhenFocusedProperty =
            DependencyProperty.RegisterAttached("SelectWhenFocused", typeof(bool), typeof(InitialFocusExtentions), new UIPropertyMetadata(OnSelectOnFocusedChanged));

        public static void OnSelectOnFocusedChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            bool SetProperty = (bool)args.NewValue;

            if (obj is ComboBox)
            {

                var comboBox = obj as ComboBox;
                if (comboBox == null) return;

                if (SetProperty)
                {
                    GotFocused(comboBox, null);
                }
            }

            if (obj is WatermarkTextBox )
            {
                var watermarkTextBox = obj as WatermarkTextBox;
                if (watermarkTextBox == null) return;
                if (SetProperty)
                {
                    GotFocused(watermarkTextBox, null);
                }
            }

            if (obj is TextBox)
            {
                var textBox = obj as TextBox;
                if (textBox == null) return;
                if (SetProperty)
                {
                    GotFocused(textBox, null);
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

        public static void GotFocused(WatermarkTextBox sender, RoutedEventArgs e)
        {
            sender.SelectAll();
        }

    }
}
