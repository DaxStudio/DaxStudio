using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ADOTabular;
using DaxStudio;
using DaxStudio.AdomdClientWrappers;
using Microsoft.Office.Interop.Excel;
using MessageBox = System.Windows.Forms.MessageBox;
using Window = System.Windows.Window;

namespace wpf_testing
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

        //private void rdoTabularServer_Checked(object sender, RoutedEventArgs e)
        //{
        //    lblPowerPivotUnavailable.Visibility = Visibility.Hidden;
        //}
       
        private readonly string _connectionString = "";
        private readonly ExcelHelper _xlHelper;
        //private bool _serverModeSelected;

        public ConnectionDialog(Workbook wbk, String currentConnection, ExcelHelper xlHelper)
        {
            //InitializeComponent();
        //    _workbook = wbk;
            _connectionString = currentConnection;
            _xlHelper = xlHelper;
        }

        
        public ADOTabularConnection Connection { get; private set; }

        private void ConnectionDialogClosing(object sender, FormClosingEventArgs e)
        {
            if (Connection == null) e.Cancel = true;
        }

        private void BtnCancelClick(object sender, EventArgs e)
        {
            
        }

        
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

    }
}
