using System;
using System.Windows.Forms;
using DaxStudioWPF;
using Microsoft.Office.Tools.Ribbon;

namespace DaxStudio
{
    public partial class DaxStudioRibbon
    {
        private void Ribbon1Load(object sender, RibbonUIEventArgs e)
        {

        }

        private DaxStudioForm _ds;
        private void ShowWinForm()
        {
            if (_ds == null || _ds.IsDisposed)
            {
                _ds = new DaxStudioForm();
                _ds.Application = Globals.ThisAddIn.Application;
            }
            if (!_ds.Visible)
                _ds.Show();
            else
                _ds.Activate();
        }

        private void BtnDaxClick(object sender, RibbonControlEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Shift)
            {
                ShowWpfForm();
            }
            else
            {
                ShowWinForm();
            }
        }

        
        private void ShowWpfForm()
        {
            DaxStudioWindow _wpfWindow;    
            //if (_wpfWindow == null )
            //{
                _wpfWindow = new DaxStudioWindow();
                // use WindowInteropHelper to set the Owner of our WPF window to the Excel application window
                var hwndHelper = new System.Windows.Interop.WindowInteropHelper(_wpfWindow);
                hwndHelper.Owner = new IntPtr(Globals.ThisAddIn.Application.Hwnd);
            //}

            // show our window
            _wpfWindow.ShowDialog();
            
        }


    }
}
