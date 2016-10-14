using System;
using Microsoft.Office.Tools.Ribbon;
using Microsoft.Owin.Hosting;
using System.Diagnostics;
using Microsoft.Win32;
using System.Net;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Serilog;
using DaxStudio.Interfaces;

namespace DaxStudio.ExcelAddin
{
    
    public partial class DaxStudioRibbon :IDaxStudioLauncher
    {
        private Process _client;
        private int _port;

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        private void Ribbon1Load(object sender, RibbonUIEventArgs e)
        {
            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "RibbonLoad", "Start");
            try {
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
            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "RibbonLoad", "Finish");
        }
        
        private void BtnDaxClick(object sender, RibbonControlEventArgs e)
        {
            try {
                Launch();
            }
            catch (Exception ex)
            {
                
                Log.Error("{Class} {method} {exception} {stacktrace}", "DaxStudioRibbon", "BtnDaxClick", ex.Message, ex.StackTrace);
            }
        }

        public void Launch()
        {
            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "Launch", "Entering Launch()");
            // Find free port and start the web host
            _port = WebHost.Start();
            
            
            //todo - can I search for DaxStudio.exe and set _client if found (would need to send it a message with the port number) ??

            // activate DAX Studio if it's already running
            if (_client != null)
            {
                if (!_client.HasExited)
                {
                    SetForegroundWindow(_client.MainWindowHandle);
                    return;
                }
            }

            var path = "";
            // look for daxstudio.exe in the same folder as daxstudio.dll
            path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daxstudio.exe");

            Log.Debug("{class} {method} About to launch DaxStudio on path: {path} port: {port}", "DaxStudioRibbon", "BtnDaxClick", path, _port);
            // start Dax Studio process
            _client = Process.Start(new ProcessStartInfo(path, _port.ToString()));

            Log.Debug("{class} {method} {message}", "DaxStudioRibbon", "Launch", "Exiting Launch()");
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
}
