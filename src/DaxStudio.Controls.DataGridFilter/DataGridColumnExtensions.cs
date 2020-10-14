using System;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.Controls.DataGridFilter
{
    public sealed class DataGridColumnExtensions//:DependencyObject
    {
        #region CustomBindingPath Dependency Property
        public static readonly DependencyProperty CustomBindingPathProperty =
            DependencyProperty.RegisterAttached("CustomBindingPath",
                typeof(string), typeof(DataGridColumn), new PropertyMetadata(string.Empty));

        public static string GetCustomBindingPath(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (string)target.GetValue(CustomBindingPathProperty);
        }

        public static void SetCustomBindingPath(DependencyObject target, string value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(CustomBindingPathProperty, value);
        }
        #endregion

        #region IsContainsTextSearch Dependency Property
        public static readonly DependencyProperty IsContainsTextSearchProperty =
            DependencyProperty.RegisterAttached("IsContainsTextSearch",
                typeof(bool), typeof(DataGridColumn));

        public static bool GetIsContainsTextSearch(DependencyObject target)
        {
            if (target == null) throw  new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(IsContainsTextSearchProperty);
        }

        public static void SetIsContainsTextSearch(DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(IsContainsTextSearchProperty, value);
        }
        #endregion

        #region IsCaseSensitiveSearch Dependency Property
        public static readonly DependencyProperty IsCaseSensitiveSearchProperty =
            DependencyProperty.RegisterAttached("IsCaseSensitiveSearch",
                typeof(bool), typeof(DataGridColumn));

        public static bool GetIsCaseSensitiveSearch(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(IsCaseSensitiveSearchProperty);
        }

        public static void SetIsCaseSensitiveSearch(DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(IsCaseSensitiveSearchProperty, value);
        }
        #endregion

        #region IsBetweenFilter Dependency Property
        public static readonly DependencyProperty IsBetweenFilterControlProperty =
            DependencyProperty.RegisterAttached("IsBetweenFilterControl",
                typeof(bool), typeof(DataGridColumn));

        public static bool GetIsBetweenFilterControl(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(IsBetweenFilterControlProperty);
        }

        public static void SetIsBetweenFilterControl(DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(IsBetweenFilterControlProperty, value);
        }
        #endregion

        #region DoNotGenerateFilter Dependency Property
        public static readonly DependencyProperty DoNotGenerateFilterControlProperty =
            DependencyProperty.RegisterAttached("DoNotGenerateFilterControl",
                typeof(bool), typeof(DataGridColumn), new PropertyMetadata(false));

        public static bool GetDoNotGenerateFilterControl(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(DoNotGenerateFilterControlProperty);
        }

        public static void SetDoNotGenerateFilterControl(DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(DoNotGenerateFilterControlProperty, value);
        }
        #endregion

    }
}
