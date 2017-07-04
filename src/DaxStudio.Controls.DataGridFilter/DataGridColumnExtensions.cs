using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.Controls.DataGridFilter
{
    public class DataGridColumnExtensions:DependencyObject
    {
        #region CustomBindingPath Dependency Property
        public static DependencyProperty CustomBindingPathProperty =
            DependencyProperty.RegisterAttached("CustomBindingPath",
                typeof(string), typeof(DataGridColumn));

        public static string GetCustomBindingPath(DependencyObject target)
        {
            return (string)target.GetValue(CustomBindingPathProperty);
        }

        public static void SetCustomBindingPath(DependencyObject target, string value)
        {
            target.SetValue(CustomBindingPathProperty, value);
        }
        #endregion

        #region IsContainsTextSearch Dependency Property
        public static DependencyProperty IsContainsTextSearchProperty =
            DependencyProperty.RegisterAttached("IsContainsTextSearch",
                typeof(bool), typeof(DataGridColumn));

        public static bool GetIsContainsTextSearch(DependencyObject target)
        {
            return (bool)target.GetValue(IsContainsTextSearchProperty);
        }

        public static void SetIsContainsTextSearch(DependencyObject target, bool value)
        {
            target.SetValue(IsContainsTextSearchProperty, value);
        }
        #endregion

        #region IsCaseSensitiveSearch Dependency Property
        public static DependencyProperty IsCaseSensitiveSearchProperty =
            DependencyProperty.RegisterAttached("IsCaseSensitiveSearch",
                typeof(bool), typeof(DataGridColumn));

        public static bool GetIsCaseSensitiveSearch(DependencyObject target)
        {
            return (bool)target.GetValue(IsCaseSensitiveSearchProperty);
        }

        public static void SetIsCaseSensitiveSearch(DependencyObject target, bool value)
        {
            target.SetValue(IsCaseSensitiveSearchProperty, value);
        }
        #endregion

        #region IsBetweenFilter Dependency Property
        public static DependencyProperty IsBetweenFilterControlProperty =
            DependencyProperty.RegisterAttached("IsBetweenFilterControl",
                typeof(bool), typeof(DataGridColumn));

        public static bool GetIsBetweenFilterControl(DependencyObject target)
        {
            return (bool)target.GetValue(IsBetweenFilterControlProperty);
        }

        public static void SetIsBetweenFilterControl(DependencyObject target, bool value)
        {
            target.SetValue(IsBetweenFilterControlProperty, value);
        }
        #endregion

        #region DoNotGenerateFilter Dependency Property
        public static DependencyProperty DoNotGenerateFilterControlProperty =
            DependencyProperty.RegisterAttached("DoNotGenerateFilterControl",
                typeof(bool), typeof(DataGridColumn), new PropertyMetadata(false));

        public static bool GetDoNotGenerateFilterControl(DependencyObject target)
        {
            return (bool)target.GetValue(DoNotGenerateFilterControlProperty);
        }

        public static void SetDoNotGenerateFilterControl(DependencyObject target, bool value)
        {
            target.SetValue(DoNotGenerateFilterControlProperty, value);
        }
        #endregion
    }
}
