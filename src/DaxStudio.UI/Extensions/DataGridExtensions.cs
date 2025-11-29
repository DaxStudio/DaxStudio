using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DaxStudio.UI.Extensions
{


    public static class DataGridExtensions
    {
        public static bool ScrollToItemOffset(this DataGrid grid, object item, bool autoRealize = true)
        {
            if (grid == null || item == null)
                return false;

            grid.UpdateLayout();

            var row = grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            if (row == null && autoRealize)
            {
                grid.ScrollIntoView(item);
                grid.UpdateLayout();
                row = grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            }

            if (row == null)
                return false;

            var offset = row.TransformToAncestor(grid).Transform(new Point(0, 0)).Y;
            offset = offset < 0 ? 0 : offset;
            var scrollViewer = GetScrollViewer(grid);

            if (scrollViewer == null)
                return false;
            System.Diagnostics.Debug.WriteLine($">> Scrolling TreeGrid to offset: {offset}");
            scrollViewer.ScrollToVerticalOffset(offset);
            return true;
        }

        private static ScrollViewer GetScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer viewer)
                    return viewer;

                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }

}
