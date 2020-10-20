using System;
using System.Windows;
using DaxStudio.Controls.DataGridFilter.Querying;

namespace DaxStudio.Controls.DataGridFilter
{
    public sealed class DataGridExtensions
    {
        public static readonly DependencyProperty DataGridFilterQueryControllerProperty =
            DependencyProperty.RegisterAttached("DataGridFilterQueryController",
                typeof(QueryController), typeof(DataGridExtensions));

        public static QueryController GetDataGridFilterQueryController(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (QueryController)target.GetValue(DataGridFilterQueryControllerProperty);
        }

        public static void SetDataGridFilterQueryController(DependencyObject target, QueryController value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(DataGridFilterQueryControllerProperty, value);
        }

        public static readonly DependencyProperty ClearFilterCommandProperty =
            DependencyProperty.RegisterAttached("ClearFilterCommand",
                typeof(DataGridFilterCommand), typeof(DataGridExtensions));

        public static DataGridFilterCommand GetClearFilterCommand(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (DataGridFilterCommand)target.GetValue(ClearFilterCommandProperty);
        }

        public static void SetClearFilterCommand(DependencyObject target, DataGridFilterCommand value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(ClearFilterCommandProperty, value);
        }

        public static DependencyProperty IsFilterVisibleProperty =
            DependencyProperty.RegisterAttached("IsFilterVisible",
                typeof(bool?), typeof(DataGridExtensions),
                  new FrameworkPropertyMetadata(true));

        public static bool? GetIsFilterVisible(
            DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(IsFilterVisibleProperty);
        }

        public static void SetIsFilterVisible(
            DependencyObject target, bool? value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(IsFilterVisibleProperty, value);
        }

        public static readonly DependencyProperty UseBackgroundWorkerForFilteringProperty =
            DependencyProperty.RegisterAttached("UseBackgroundWorkerForFiltering",
                typeof(bool), typeof(DataGridExtensions),
                  new FrameworkPropertyMetadata(false));

        public static bool GetUseBackgroundWorkerForFiltering(
            DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(UseBackgroundWorkerForFilteringProperty);
        }

        public static void SetUseBackgroundWorkerForFiltering(
            DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(UseBackgroundWorkerForFilteringProperty, value);
        }

        public static readonly DependencyProperty IsClearButtonVisibleProperty =
            DependencyProperty.RegisterAttached("IsClearButtonVisible",
                typeof(bool), typeof(DataGridExtensions),
                  new FrameworkPropertyMetadata(true));

        public static bool GetIsClearButtonVisible(
            DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return (bool)target.GetValue(IsClearButtonVisibleProperty);
        }

        public static void SetIsClearButtonVisible(
            DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.SetValue(IsClearButtonVisibleProperty, value);
        }

    }
}
