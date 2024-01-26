﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
//using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows;
using System.Data;

namespace DaxStudio.UI.AttachedProperties
{
    public static class DataGridExtension
    {

        #region Columns Property
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
            if(typeof(DataGrid).IsAssignableFrom(d.GetType())) 
            //if (d.GetType() == typeof(DataGrid))
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
        #endregion


        #region HeaderTags property
        public static DataTable GetHeaderTags(DependencyObject obj)
        {
            return (DataTable)obj.GetValue(HeaderTagsProperty);
        }

        public static void SetHeaderTags(DependencyObject obj, DataTable value)
        {
            obj.SetValue(HeaderTagsProperty, value);
        }

        public static readonly DependencyProperty HeaderTagsProperty =
               DependencyProperty.RegisterAttached("HeaderTags",
               typeof(DataTable),
               typeof(DataGridExtension),
               new UIPropertyMetadata(new DataTable(), OnDataGridHeaderTagsPropertyChanged));

        private static void OnDataGridHeaderTagsPropertyChanged(
               DependencyObject d,
               DependencyPropertyChangedEventArgs e)
        {
            if (typeof(DataGrid).IsAssignableFrom(d.GetType()))
            {
                DataGrid myGrid = d as DataGrid;

                DataTable table =
                     (DataTable)e.NewValue;

                if (table != null)
                {
                    if (table.Columns.Count == myGrid.Columns.Count)
                    {
                        System.Diagnostics.Debug.WriteLine("cols");
                        for (int i = 0; i < myGrid.Columns.Count; i++)
                        {
                            var myCol = myGrid.Columns[i];
                            var source = table.Columns[i].ExtendedProperties["ColumnSource"];
                            var style = myGrid.FindResource($"{source}HeaderStyle") as Style;

                            myCol.HeaderStyle = style;
                        }
                    }

                    

                    //Columns.CollectionChanged += delegate (object sender,
                    //                 NotifyCollectionChangedEventArgs args)
                    //{
                    //    if (args.NewItems != null)
                    //    {
                    //        foreach (DataGridColumn column
                    //             in args.NewItems.Cast<DataGridColumn>())
                    //        {
                    //            myGrid.Columns.Add(column);
                    //        }
                    //    }

                    //    if (args.OldItems != null)
                    //    {
                    //        foreach (DataGridColumn column
                    //                in args.OldItems.Cast<DataGridColumn>())
                    //        {
                    //            myGrid.Columns.Remove(column);
                    //        }
                    //    }
                    //};
                }
            }
        }

        #endregion

    }
}