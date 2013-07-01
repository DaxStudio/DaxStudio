using System;
using System.Windows;
using System.Windows.Controls;
using ADOTabular;
using DaxStudio.Interfaces;
//using System.Windows.Forms;
//using Microsoft.Office.Interop.Excel;
//using MessageBox = System.Windows.Forms.MessageBox;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ConnectionDialogView : UserControl
    {


        public ConnectionDialogView()
        {
            InitializeComponent();
        }

        //private void rdoTabularServer_Checked(object sender, RoutedEventArgs e)
        //{
        //    lblPowerPivotUnavailable.Visibility = Visibility.Hidden;
        //}
       
        private readonly string _connectionString = "";
        //private readonly ExcelHelper _xlHelper;
        //private bool _serverModeSelected;
        private IDaxStudioHost _host;
        public ConnectionDialogView(IDaxStudioHost host, string currentConnection)
        {
            InitializeComponent();
        
            _host = host;
            _connectionString = currentConnection;
        }

        
        public ADOTabularConnection Connection { get; private set; }


        private void BtnCancelClick(object sender, EventArgs e)
        {
            
        }

        
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
        //    DialogResult = true;
        //    this.Close();
        }

        private void ConnectionDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //if (Connection == null) e.Cancel = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
        //    this.Close();
        }

    }
}
