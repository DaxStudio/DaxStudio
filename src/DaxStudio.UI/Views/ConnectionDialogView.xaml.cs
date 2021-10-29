using System;
using System.Windows;
using System.Windows.Controls;
using ADOTabular;
using DaxStudio.Interfaces;
using System.Windows.Input;
using ModernWpf.Controls;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ConnectionDialogView : DaxStudio.UI.Controls.DaxStudioDialog
    {


        public ConnectionDialogView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            cboServers.Focusable = true;
            cboServers.Focus();
            Keyboard.Focus(cboServers);
        }

    }
}
