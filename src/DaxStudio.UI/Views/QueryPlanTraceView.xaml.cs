using DaxStudio.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
    /// Interaction logic for QueryPlanTraceView.xaml
    /// </summary>
    public partial class QueryPlanTraceView : ZoomableUserControl
    {
        public QueryPlanTraceView()
        {
            InitializeComponent();
            // When this view is hosted in an AvalonDock LayoutAnchorable that is
            // toggled into/out of auto-hide, the DataGrids stay attached but their
            // realized containers are not refreshed when they become visible again.
            // Force a CollectionView refresh on each grid as the view becomes
            // visible so it re-reads the current ItemsSource.
            IsVisibleChanged += QueryPlanTraceView_IsVisibleChanged;
        }

        private void QueryPlanTraceView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(e.NewValue is bool visible) || !visible) return;
            PhysicalTreeGrid?.Items?.Refresh();
            LogicalTreeGrid?.Items?.Refresh();
        }

        private void DataGrid_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

    }
}
