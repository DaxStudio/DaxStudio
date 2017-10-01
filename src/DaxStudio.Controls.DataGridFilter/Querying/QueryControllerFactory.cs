using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaxStudio.Controls.DataGridFilter.Support;
using System.Collections;

namespace DaxStudio.Controls.DataGridFilter.Querying
{
    public class QueryControllerFactory
    {
        public static QueryController 
            GetQueryController(
            System.Windows.Controls.DataGrid dataGrid,
            FilterData filterData, IEnumerable itemsSource)
        {
            QueryController query;

            query = DataGridExtensions.GetDataGridFilterQueryController(dataGrid);

            if (query == null)
            {
                //clear the filter if exisits begin
                System.ComponentModel.ICollectionView view
                    = System.Windows.Data.CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
                if (view != null) view.Filter = null;
                //clear the filter if exisits end

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
