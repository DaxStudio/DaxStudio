using System;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using DaxStudio;
using DaxStudio.UI;
using Microsoft.Office.Tools.Ribbon;

namespace DaxStudio
{
    public partial class DaxStudioRibbon
    {
        private void Ribbon1Load(object sender, RibbonUIEventArgs e)
        {

        }
        /*
        private DaxStudio.UI.DaxStudioForm _ds;
        private void ShowWinForm()
        {
            if (_ds == null || _ds.IsDisposed)
            {
                _ds = new DaxStudioForm(new DaxStudioExcelHost(Globals.ThisAddIn.Application) );
                
                //_ds.Application = Globals.ThisAddIn.Application;
            }
            if (!_ds.Visible)
                _ds.Show();
            else
                _ds.Activate();
        }
        */
        private void BtnDaxClick(object sender, RibbonControlEventArgs e)
        {
        ShowWpfForm();
            /*
            if (Control.ModifierKeys == Keys.Shift)
            {
                ShowWpfForm();
            }
            else
            {
                ShowWinForm();
            }
             * */
        }
// TODO - WPF Window
        DaxStudioWindow _wpfWindow;
        private void ShowWpfForm()
        {
                
            if (_wpfWindow != null )
            {
                _wpfWindow.Close();
                _wpfWindow = null;
            }
            //_wpfWindow = new DaxStudioWindow(Globals.ThisAddIn.Application);
                //_wpfWindow.Application = Globals.ThisAddIn.Application;
                // use WindowInteropHelper to set the Owner of our WPF window to the Excel application window
                var hwndOwner = new IntPtr(Globals.ThisAddIn.Application.Hwnd);
               // var hwndHelper = new System.Windows.Interop.WindowInteropHelper(_wpfWindow);
               // hwndHelper.Owner = hwndOwner;
                //var hook = new System.Windows.Interop.HwndSourceHook()
            

            // show our window
            UserForm.ShowUserForm(new DaxStudioExcelHost(Globals.ThisAddIn.Application)); //, hwndOwner);
            //_wpfWindow.Show();
            
        }

        
    }
}
