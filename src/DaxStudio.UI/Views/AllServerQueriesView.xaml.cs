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
    /// Interaction logic for ServerTimesView.xaml
    /// </summary>
    public partial class AllServerQueriesView : ZoomableUserControl
    {
        public AllServerQueriesView()
        {
            InitializeComponent();
        }

        private void DataGrid_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

    }
}
