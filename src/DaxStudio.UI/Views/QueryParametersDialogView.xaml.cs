using DaxStudio.UI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for SaveDialogView.xaml
    /// </summary>
    public partial class QueryParametersDialogView : UserControl
    {
        bool focusSet;
        public QueryParametersDialogView()
        {
            InitializeComponent();
            ParameterGrid.LoadingRow += DataGrid_LoadingRow;
            //Loaded += (sender, e) =>
            //    SendKeys.Send(Key.Tab);
        }

        // makes the Enter key send Tab instead so that the last row 
        // goes to the next control in the tab order
        private void OnDataGridPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SendKeys.Send(Key.Tab);
            }
        }

        void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // only process the first row
            if (!focusSet)
            {
                focusSet = true;
                e.Row.Loaded += Row_Loaded;
            }
        }

        void Row_Loaded(object sender, RoutedEventArgs e)
        {
            var row = (DataGridRow)sender;
            row.Loaded -= Row_Loaded;
            DataGridCell cell = GetCell(ParameterGrid, row, 1);
            TextBox box = cell.FindChild("ValueBox", typeof(TextBox)) as TextBox;
            //if (cell != null) cell.Focus();
            if (box != null) box.Focus();
            //ParameterGrid.BeginEdit();
        }

        static DataGridCell GetCell(DataGrid dataGrid, DataGridRow row, int column)
        {
            if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));

            DataGridCellsPresenter presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
            {
                row.ApplyTemplate();
                presenter = FindVisualChild<DataGridCellsPresenter>(row);
            }
            if (presenter != null)
            {
                var cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
                if (cell == null)
                {
                    dataGrid.ScrollIntoView(row, dataGrid.Columns[column]);
                    cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
                }
                return cell;
            }
            return null;
        }

        static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                var visualChild = child as T;
                if (visualChild != null)
                    return visualChild;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }


    }
}
