using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.AttachedProperties
{
   

    /// <summary>
    /// This class contains a few useful extenders for the ListBox
    /// </summary>
    public class DataGridHorizontalScroll : DependencyObject
    {
        public static readonly DependencyProperty ScrollLeftProperty = DependencyProperty.RegisterAttached("ScrollLeft", typeof(bool), typeof(DataGridHorizontalScroll), new UIPropertyMetadata(default(bool), OnScrollLeftChanged));
        public static readonly DependencyProperty ScrollToColumnProperty = DependencyProperty.RegisterAttached("ScrollToColumn", typeof(int), typeof(DataGridHorizontalScroll), new UIPropertyMetadata(default(int)));
        public static bool GetScrollLeft(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollLeftProperty);
        }
        public static void SetScrollLeft(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollLeftProperty, value);
        }

        public static int GetScrollToColumn(DependencyObject obj)
        {
            return (int)obj.GetValue(ScrollToColumnProperty);
        }
        public static void SetScrollToColumn(DependencyObject obj, int value)
        {
            obj.SetValue(ScrollToColumnProperty, value);
        }

        /// <summary>
        /// This method will be called when the AutoScrollToEnd
        /// property was changed
        /// </summary>
        /// <param name="s">The sender (the ListBox)</param>
        /// <param name="e">Some additional information</param>
        public static void OnScrollLeftChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            if (!(s is DataGrid dataGrid)) return;
            var col = GetScrollToColumn(s);
            dataGrid.ScrollIntoView(dataGrid.SelectedItem, dataGrid.Columns[col]);
        }
    }
}
