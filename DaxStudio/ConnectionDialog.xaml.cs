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
using System.Windows.Shapes;

namespace wpf_testimng
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ConnectionDialog : Window
    {


        public ConnectionDialog()
        {
            InitializeComponent();
        }

        private void rdoTabularServer_Checked(object sender, RoutedEventArgs e)
        {
            lblPowerPivotUnavailable.Visibility = Visibility.Hidden;
        }
    }
}
