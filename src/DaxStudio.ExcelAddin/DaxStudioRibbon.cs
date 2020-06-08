using System;
using Microsoft.Office.Tools.Ribbon;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;
using System.Windows;
using DaxStudio.Common;
using System.IO;
using System.Reflection;

namespace DaxStudio.ExcelAddin
{

    public partial class DaxStudioRibbon
    {
        private Process _client;
        private int _port;


        private void Ribbon1Load(object sender, RibbonUIEventArgs e)
        {
            try
            {
                Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "RibbonLoad", "Start");
                
                // look for a running DaxStudio.exe instance and 
                // if we find one try to start a WebHost for it
                var client = DaxStudioStandalone.GetClient();
                var port = 0;
                if (_client == null && client != null)
                {
                    Log.Debug("{class} {method} {message} port: {port}", "DaxStudioRibbon", "Ribbon1Load", "Existing DaxStudio.exe process found", client.Port);
                    _client = client.Process;
                    port = client.Port;
                }
                if (port != 0) { WebHost.Start(port); }
            }
            catch (Exception ex)
            {
                Log.Error("{Class} {method} {exception} {stacktrace}", "DaxStudioRibbon", "RibbonLoad", ex.Message, ex.StackTrace);
            }
            finally
            {
                Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "RibbonLoad", "Finish");
            }
        }
        
        public bool DebugLoggingEnabled { get; set; }

        private void BtnDaxClick(object sender, RibbonControlEventArgs e)
        {
            try {
                //var xl = new ExcelHelper(Globals.ThisAddIn.Application);
                //if (!xl.HasPowerPivotData())
                //{
                //    MessageBox.Show("The Active Workbook must have a PowerPivot in order to launch DAX Studio");
                //    return;
                //}
                    


                RibbonButton btn = (RibbonButton)sender;
                var enableLogging = DebugLoggingEnabled;// (bool)btn.Tag;
                Launch(enableLogging);
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                var inner = ex.InnerException;
                while (inner != null)
                {
                    msg += $"\n{inner.Message}";
                    inner = inner.InnerException;
                }

                MessageBox.Show($"The following Error occurred while trying to launch the DAX Studio User Interface\n{msg}", "DAX Studio Excel Add-in");    
                Log.Error(ex, "{Class} {method} {exception} {stacktrace}", "DaxStudioRibbon", "BtnDaxClick", ex.Message, ex.StackTrace);
            }
        }

        public void Launch(bool enableLogging)
        {
            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "Launch", "Entering Launch()");
            // Find free port and start the web host
            _port = WebHost.Start();


            //todo - can I search for DaxStudio.exe and set _client if found (would need to send it a message with the port number) ??
            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "Launch", "Checking for an existing instance of DaxStudio.exe");
            // activate DAX Studio if it's already running
            if (_client != null)
            {
                if (!_client.HasExited)
                {
                    NativeMethods.SetForegroundWindow(_client.MainWindowHandle);
                    return;
                }
            }

            var path = "";
            // look for daxstudio.exe in the same folder as daxstudio.dll
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daxstudio.exe");
            if (!File.Exists(path))
            {
                // try getting daxstudio.exe from the parent directory
                path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "daxstudio.exe"));
                if (!File.Exists(path)) throw new FileNotFoundException("Excel Addin is unable to launch the DAX Studio User Interface");
            }


            Log.Debug("{class} {method} About to launch DaxStudio on path: {path} port: {port}", "DaxStudioRibbon", "BtnDaxClick", path, _port);
            // start Dax Studio process
            ProcessStartInfo psi = new ProcessStartInfo(path)
            {
                Arguments = $"-port {_port}",
                UseShellExecute = false // this is false as we are executing a .exe file directly
            };
            if (enableLogging) psi.Arguments += " -log";
            _client = Process.Start(psi);

            if (!_client.WaitForInputIdle(Constants.ExcelUIStartupTimeout))
            {
                Log.Error("Launching User Interface from Excel exceeded the {timeout} ms timeout", Constants.ExcelUIStartupTimeout);
                MessageBox.Show("The DAX Studio User Interface is taking a long time to load");
            }
            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "Launch", "Exiting Launch()");
        }

        public void Launch()
        {
            Launch(false);
        }

        private void DaxStudioRibbon_Close(object sender, EventArgs e)
        {
            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "DaxStudioRibbon_Close", "Entering");
            try {
                // stop the web host
                WebHost.Stop();
            }
            catch (Exception ex)
            {
                Log.Error("{Class} {method} {exception} {stacktrace}", "DaxStudioRibbon", "DaxStudioRibbon_Close", ex.Message, ex.StackTrace);
            }

            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "DaxStudioRibbon_Close", "Exiting");

            // TODO - need to find a way to only shut down the UI if 
            //        Dax Studio was launched from Excel

            // tell the DaxStudio.exe client to shutdown
            //if (_client != null)
            //{
            //    if (!_client.HasExited)
            //    {
            //        SetForegroundWindow(_client.MainWindowHandle);
            //        _client.CloseMainWindow();
            //    }
            //}

        }


    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
