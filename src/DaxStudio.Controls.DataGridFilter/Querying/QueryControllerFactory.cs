using System;
using DaxStudio.Controls.DataGridFilter.Support;
using System.Collections;

namespace DaxStudio.Controls.DataGridFilter.Querying
{
    public static class QueryControllerFactory
    {
        public static QueryController 
            GetQueryController(
            System.Windows.Controls.DataGrid dataGrid,
            FilterData filterData, IEnumerable itemsSource)
        {
            if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));

            var query = DataGridExtensions.GetDataGridFilterQueryController(dataGrid);

            if (query == null)
            {
                //clear the filter if exists begin
                System.ComponentModel.ICollectionView view
                    = System.Windows.Data.CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
                if (view != null) view.Filter = null;
                //clear the filter if exists end

                query = new QueryController();
                DataGridExtensions.SetDataGridFilterQueryController(dataGrid, query);
            }

            query.ColumnFilterData        = filterData;
            query.ItemsSource             = itemsSource;
            query.CallingThreadDispatcher = dataGrid.Dispatcher;
            query.UseBackgroundWorker     = DataGridExtensions.GetUseBackgroundWorkerForFiltering(dataGrid);

            return query;
        }
    }
}
