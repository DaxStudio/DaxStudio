using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interactivity;

namespace DaxStudio.UI.Behaviours
{
    public class InitialColumnFilterBehavior : Behavior<Xceed.Wpf.DataGrid.Column>
    {
        private bool _itemsSourceChangedEventHooked = false;
        protected override void OnAttached()
        {
            if (AssociatedObject.DataGridControl != null)
            {
                AssociatedObject.DataGridControl.ItemsSourceChangeCompleted += OnItemsSourceChangeCompleted;
            }
            AssociatedObject.Changed += AssociatedObject_Changed;
        }

        void AssociatedObject_Changed(object sender, EventArgs e)
        {
            //Console.WriteLine("DataGridControl: {0}", this.AssociatedObject.DataGridControl == null);
            var col = sender as Xceed.Wpf.DataGrid.Column;
            if (col == null) return;
            if (col.DataGridControl == null) return;
            if (_itemsSourceChangedEventHooked) return;
            col.DataGridControl.ItemsSourceChangeCompleted += OnItemsSourceChangeCompleted;
        }

        private void OnItemsSourceChangeCompleted(object sender, EventArgs e)
        {
            UpdateFilter(this.AssociatedObject, InitialFilter);
            
        }

        private static void UpdateFilter(Xceed.Wpf.DataGrid.Column column, string initialFilter)
        {
            var dgcv = column.DataGridControl.ItemsSource as Xceed.Wpf.DataGrid.DataGridCollectionView;
            if (dgcv == null) return;
            dgcv.AutoFilterValues[column.FieldName].Clear();
            dgcv.AutoFilterValues[column.FieldName].Add(initialFilter);
        }

        public string InitialFilter
        {
            get { return (string)GetValue(InitialFilterProperty); }
            set { SetValue(InitialFilterProperty, value); }
        }

        public static readonly DependencyProperty InitialFilterProperty =
               DependencyProperty.Register("InitialFilter", 
               typeof(string),
               typeof(InitialColumnFilterBehavior), 
               new PropertyMetadata(OnPropertyChanged));

        public static void OnPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var col = ((InitialColumnFilterBehavior)obj).AssociatedObject as Xceed.Wpf.DataGrid.Column;
            var filter = args.NewValue as string;
            if (col == null | filter == null) return;
            UpdateFilter(col,filter);
        }
    }
}
