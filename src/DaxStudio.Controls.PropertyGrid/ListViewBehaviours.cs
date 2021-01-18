using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.Controls.PropertyGrid
{
    // Class implementing handy behaviors for the ListView control
    public static class ListViewBehaviors
    {
        // Technique for updating column widths of a ListView's GridView manually
        public static void UpdateColumnWidths(GridView gridView)
        {
            // For each column...
            foreach (var column in gridView.Columns)
            {
                // If this is an "auto width" column...
                if (double.IsNaN(column.Width))
                {
                    // Set its Width back to NaN to auto-size again
                    column.Width = 0;
                    column.Width = double.NaN;
                }
            }
        }



        ///
        /// Sets the column widths.
        ///
        private static void SetColumnWidths(ListView listView)
        {
            //Pull the stretch columns fromt the tag property.
            //List<GridViewColumn> columns = (listView.Tag as List<GridViewColumn>);
            double specifiedWidth = 0;
            GridView gridView = listView.View as GridView;
            if (gridView != null)
            {
                //if (columns == null)
                //{
                //    //Instance if its our first run.
                //    columns = new List<GridViewColumn>();
                //    // Get all columns with no width having been set.
                //    foreach (GridViewColumn column in gridView.Columns)
                //    {
                //        if (!(column.Width >= 0))
                //        {
                //            columns.Add(column);
                //        }
                //        else
                //        {
                //            specifiedWidth += column.ActualWidth;
                //        }
                //    }
                //}
                //else
                //{
                //    // Get all columns with no width having been set.
                //    foreach (GridViewColumn column in gridView.Columns)
                //    {
                //        if (!columns.Contains(column))
                //        {
                //            specifiedWidth += column.ActualWidth;
                //        }
                //    }
                //}

                var firstColumn = gridView.Columns[0];
                specifiedWidth = firstColumn.ActualWidth;
                // Set its Width back to NaN to auto-size again
                //firstColumn.Width = 0;
                //firstColumn.Width = double.NaN;



                double newWidth = (listView.ActualWidth - specifiedWidth) / (gridView.Columns.Count - 1);

                // Allocate remaining space after the first column equally.
                for (var i = 1; i < gridView.Columns.Count; i++)
                {
                    GridViewColumn column = gridView.Columns[i];

                    if (newWidth >= 10)
                    {
                        column.Width = newWidth - 10;
                    }
                }

                //Store the columns in the TAG property for later use.
                //listView.Tag = columns;


                //foreach (GridViewColumn c in gridView.Columns)
                //{
                //    if (double.IsNaN(c.Width)) {
                //        c.Width = c.ActualWidth;
                //    }
                //    c.Width = double.NaN;
                //}
            }
        }


        // Definition of the IsAutoUpdatingColumnWidthsProperty attached DependencyProperty
        public static readonly DependencyProperty IsAutoUpdatingColumnWidthsProperty =
            DependencyProperty.RegisterAttached(
                "IsAutoUpdatingColumnWidths",
                typeof(bool),
                typeof(ListViewBehaviors),
                new UIPropertyMetadata(false, OnIsAutoUpdatingColumnWidthsChanged));

        // Get/set methods for the attached DependencyProperty
        public static bool GetIsAutoUpdatingColumnWidths(ListView listView)
        {
            return (bool)listView.GetValue(IsAutoUpdatingColumnWidthsProperty);
        }

        public static void SetIsAutoUpdatingColumnWidths(ListView listView, bool value)
        {
            listView.SetValue(IsAutoUpdatingColumnWidthsProperty, value);
        }

        // Change handler for the attached DependencyProperty
        private static void OnIsAutoUpdatingColumnWidthsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            // Get the ListView instance and new bool value
            var listView = o as ListView;
            if ((null != listView) && (e.NewValue is bool))
            {
                // Get a descriptor for the ListView's ItemsSource property
                var descriptor = DependencyPropertyDescriptor.FromProperty(ListView.ItemsSourceProperty, typeof(ListView));
                var catFilter = DependencyPropertyDescriptor.FromProperty(PropertyList.CategoryFilterProperty, typeof(PropertyList));
                var searchFilter = DependencyPropertyDescriptor.FromProperty(PropertyList.SearchTextProperty, typeof(PropertyList));
                if ((bool)e.NewValue)
                {
                    // Enabling the feature, so add the change handler
                    descriptor.AddValueChanged(listView, OnListViewItemsSourceValueChanged);
                    catFilter.AddValueChanged(listView, OnListViewItemsSourceValueChanged);
                    searchFilter.AddValueChanged(listView, OnListViewItemsSourceValueChanged);
                    listView.SizeChanged += ListView_SizeChanged;
                }
                else
                {
                    // Disabling the feature, so remove the change handler
                    descriptor.RemoveValueChanged(listView, OnListViewItemsSourceValueChanged);
                    catFilter.RemoveValueChanged(listView, OnListViewItemsSourceValueChanged);
                    searchFilter.RemoveValueChanged(listView, OnListViewItemsSourceValueChanged);
                    listView.SizeChanged -= ListView_SizeChanged;
                }
            }
        }

        private static void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            OnListViewItemsSourceValueChanged(sender, e);
        }

        // Handler for changes to the ListView's ItemsSource updates the column widths
        private static void OnListViewItemsSourceValueChanged(object sender, EventArgs e)
        {
            // Get a reference to the ListView's GridView...
            var listView = sender as ListView;
            if (null != listView)
            {
                SetColumnWidths(listView);
                //var gridView = listView.View as GridView;
                //if (null != gridView)
                //{
                //    // And update its column widths
                //    UpdateColumnWidths(gridView);
                //}
            }
        }
    }
}
