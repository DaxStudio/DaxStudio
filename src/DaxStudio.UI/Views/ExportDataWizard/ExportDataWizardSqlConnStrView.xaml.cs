using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    /// Interaction logic for ExportDataWizardSqlConnStrView.xaml
    /// </summary>
    public partial class ExportDataWizardSqlConnStrView : UserControl
    {
        public ExportDataWizardSqlConnStrView()
        {
            InitializeComponent();
        }

        private void ConnectionString_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
