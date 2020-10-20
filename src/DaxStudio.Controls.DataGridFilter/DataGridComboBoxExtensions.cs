using System;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.Controls.DataGridFilter
{
    public sealed class DataGridComboBoxExtensions
    {
        public static readonly DependencyProperty IsTextFilterProperty =
            DependencyProperty.RegisterAttached("IsTextFilter",
                typeof(bool), typeof(DataGridComboBoxColumn));

        public static bool GetIsTextFilter(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(IsTextFilterProperty);
        }

        public static void SetIsTextFilter(DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(IsTextFilterProperty, value);
        }


        /// <summary>
        ///  if true ComboBox.IsEditable is true and ComboBox.IsReadOnly is false
        ///  otherwise
        ///  ComboBox.IsEditable is false and ComboBox.IsReadOnly is true
        /// </summary>
        public static readonly DependencyProperty UserCanEnterTextProperty =
            DependencyProperty.RegisterAttached("UserCanEnterText",
                typeof(bool), typeof(DataGridComboBoxColumn));

        public static bool GetUserCanEnterText(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(UserCanEnterTextProperty);
        }

        public static void SetUserCanEnterText(DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(UserCanEnterTextProperty, value);
        }

         
    }
}
