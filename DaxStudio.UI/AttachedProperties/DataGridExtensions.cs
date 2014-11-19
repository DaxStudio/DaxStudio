using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
//using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows;

namespace DaxStudio.UI.AttachedProperties
{
    public static class DataGridExtension
    {
        public static ObservableCollection<DataGridColumn> GetColumns(DependencyObject obj)
        {
            return (ObservableCollection<DataGridColumn>)obj.GetValue(ColumnsProperty);
        }

        public static void SetColumns(DependencyObject obj, ObservableCollection<DataGridColumn> value)
        {
            obj.SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty ColumnsProperty =
               DependencyProperty.RegisterAttached("Columns",
               typeof(ObservableCollection<DataGridColumn>),
               typeof(DataGridExtension),
               new UIPropertyMetadata(new ObservableCollection<DataGridColumn>(), OnDataGridColumnsPropertyChanged));

        private static void OnDataGridColumnsPropertyChanged(
               DependencyObject d,
               DependencyPropertyChangedEventArgs e)
        {
            if (d.GetType() == typeof(DataGrid))
            {
                DataGrid myGrid = d as DataGrid;

                ObservableCollection<DataGridColumn> Columns =
                     (ObservableCollection<DataGridColumn>)e.NewValue;

                if (Columns != null)
                {
                    myGrid.Columns.Clear();

                    if (Columns != null && Columns.Count > 0)
                    {
                        foreach (DataGridColumn dataGridColumn in Columns)
                        {
                            myGrid.Columns.Add(dataGridColumn);
                        }
                    }

                    Columns.CollectionChanged += delegate(object sender,
                                     NotifyCollectionChangedEventArgs args)
                         {
                             if (args.NewItems != null)
                             {
                                 foreach (DataGridColumn column
                                      in args.NewItems.Cast<DataGridColumn>())
                                 {
                                     myGrid.Columns.Add(column);
                                 }
                             }

                             if (args.OldItems != null)
                             {
                                 foreach (DataGridColumn column
                                         in args.OldItems.Cast<DataGridColumn>())
                                 {
                                     myGrid.Columns.Remove(column);
                                 }
                             }
                         };
                }
            }
        }
    }
}