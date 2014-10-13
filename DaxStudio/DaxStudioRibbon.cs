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

namespace DaxStudio
{
    
    public partial class DaxStudioRibbon
    {
        
        //private static Thread _launcherThread;
//        private static CancellationTokenSource _cancelToken;
//        private static AutoResetEvent _showWindow;
        //private Application _application;
//        private static AutoResetEvent _shutdownSync;
        private IDisposable webapp;
        private Process _client;
        private int _port;

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

    /*
        public CancellationTokenSource CancelToken
        {
            get { return _cancelToken; }
        }

        public AutoResetEvent ShutDownSync
        {
            get { return _shutdownSync; }
        }
      */
        private void Ribbon1Load(object sender, RibbonUIEventArgs e)
        {
     //       _showWindow = new AutoResetEvent(false);
     //       _cancelToken = new CancellationTokenSource();
     //       _shutdownSync = new AutoResetEvent(false);

//            _launcherThread = new Thread(() => ShowWindow(_showWindow, _cancelToken));
//            _launcherThread.SetApartmentState(ApartmentState.STA);
 //           _launcherThread.Start();
        }
        /*
        private void ShowWindow(AutoResetEvent showMe, CancellationTokenSource cancelMe)
        {
            var waits = new WaitHandle[2];
            waits[0] = showMe;
            waits[1] = cancelMe.Token.WaitHandle;
            //ShellViewModel shell;
            _application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            while (true)
            {
                WaitHandle.WaitAny(waits); //wait until show or cancel is requested

                if (cancelMe.IsCancellationRequested)
                {
                    //Dispatcher.CurrentDispatcher.InvokeShutdown();
                    
                    break; // exit loop and thread if cancelled (on addin shutdown)
                }

                var bootstrapper = new AppBootstrapper(Assembly.GetAssembly(typeof(DaxStudioExcelHost)), false);   
                
                bootstrapper.Start();
                var windowManager = IoC.Get<IWindowManager>();
                var eventAggregator = IoC.Get<IEventAggregator>();
                var ribbonViewModel = IoC.Get<RibbonViewModel>();
                var statusBarViewModel = IoC.Get<StatusBarViewModel>();
                var conductor = IoC.Get<IConductor>();
                var host = IoC.Get<IDaxStudioHost>();
                var shell = new ShellViewModel(windowManager, eventAggregator, ribbonViewModel, statusBarViewModel, conductor,host);
                //var win = ((Window) shell.GetView());
                //win.Closed += (sender2, e2) => win.Dispatcher.InvokeShutdown();
                // use WindowInteropHelper to set the Owner of our WPF window to the Excel application window
                //var hwndOwner = new IntPtr(Globals.ThisAddIn.Application.Hwnd);
                //var hwndHelper = new System.Windows.Interop.WindowInteropHelper(win);
                //hwndHelper.Owner = hwndOwner;
                windowManager.ShowDialog(shell);
                conductor.CloseItem(shell);
                shell.TryClose();

                //Dispatcher.CurrentDispatcher.InvokeShutdown();

                showMe.Reset();
                //var win = ((Window) shell.GetView());
                //win.Closed += (sender2, e2) => win.Dispatcher.InvokeShutdown();

                //Dispatcher.Run();
            }
            Dispatcher.CurrentDispatcher.InvokeShutdown();
            
            //_application.Dispatcher.InvokeShutdown();
            ShutDownSync.Set();
        }
        */

        //private AutoResetEvent _shutDownSync;
        //public AutoResetEvent ShutDownSync { get { return _shutDownSync; } set { _shutDownSync = value; } }

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
            /*
            PresentationTraceSources.Refresh();
            PresentationTraceSources.MarkupSource.Switch.Level = SourceLevels.All;
            PresentationTraceSources.MarkupSource.Listeners.Add(new DefaultTraceListener());  
             */
            //ShowWpfForm();
            
            // check if web host is running
            if (webapp == null)
            {
                // find free port
                _port = GetOpenPort(9000,9999);

                Log.Information("DaxStudio Host started on port {port}", _port);
                System.Diagnostics.EventLog appLog =new System.Diagnostics.EventLog();
                appLog.Source = "DaxStudio";
                appLog.WriteEntry( string.Format("DaxStudio Excel Add-in Listening on port {0}",_port),EventLogEntryType.Information);

                //if not Find free port and start it
                StartWebHost(_port);
            }

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
            // try to get path from LocalMachine registry
            path = (string)Registry.LocalMachine.GetValue("Software\\DaxStudio");
            Log.Verbose("{Class} {Method} HKLM Value1: {Value}", "DaxStudioRibbon", "BtnDaxClick", path);
            if (string.IsNullOrWhiteSpace(path))
            {
                path = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\Software\\DaxStudio", "Path", null);
                Log.Verbose("{Class} {Method} HKLM Value2: {Value}", "DaxStudioRibbon", "BtnDaxClick", path);
            }
            // otherwise get path from HKCU registry
            if (string.IsNullOrWhiteSpace(path))
            {
                path = (string)Registry.GetValue("HKEY_CURRENT_USER\\Software\\DaxStudio", "Path", "");
                Log.Verbose("{Class} {Method} HKCU Value: {Value}", "DaxStudioRibbon", "BtnDaxClick", path);
            }

            Log.Debug("About to launch DaxStudio on path: {path}", path);
            // start Dax Studio process
            _client = Process.Start(new ProcessStartInfo(path, _port.ToString()));
            

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

        private void StartWebHost(int port)
        {
            string baseAddress = string.Format("http://localhost:{0}/",port); 

            // Start OWIN host 
            //StartOptions so = new StartOptions(baseAddress);
            //so.Settings.Add("location", ExcelHelper.ActiveWorkbookLocation);
            //webapp = WebApp.Start<Xmla.Startup>(so);
            webapp = WebApp.Start<Xmla.Startup>(url: baseAddress );
            
            //using (WebApp.Start<Xmla.Startup>(url: baseAddress)) 
            //{
                // Create HttpCient and make a request to api/values 
                //HttpClient client = new HttpClient(); 

                //var response = client.GetAsync(baseAddress + "api/xmla").Result; 

                //Debug.WriteLine(response); 
                //Console.WriteLine(response.Content.ReadAsStringAsync().Result); 
            //    System.Windows.MessageBox.Show("WebHost started");
            //}
        }

        /// <summary>
        /// Finds an available TCP/IP port in the range between the Start and End params
        /// </summary>
        /// <param name="portStartIndex">Port Number to start searching from</param>
        /// <param name="portEndIndex">Port Number to finish searching at</param>
        /// <returns>an available tcp/ip port that does not currently have an active listener</returns>
        private int GetOpenPort(int portStartIndex, int portEndIndex)
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpEndPoints = properties.GetActiveTcpListeners();

            List<int> usedPorts = tcpEndPoints.Select(p => p.Port).ToList<int>();
            int unusedPort = 0;

            for (int port = portStartIndex; port < portEndIndex; port++)
            {
                if (!usedPorts.Contains(port))
                {
                    unusedPort = port;
                    break;
                }
            }
            return unusedPort;
        }

        private void DaxStudioRibbon_Close(object sender, EventArgs e)
        {
            if (webapp != null)
            {
                webapp.Dispose();
            }
            if (_client != null)
            {
                if (!_client.HasExited)
                {
                    SetForegroundWindow(_client.MainWindowHandle);
                    _client.CloseMainWindow();
                }
            }
 //           _cancelToken.Cancel();
 //           _shutdownSync.Dispose();
 //           _showWindow.Dispose();
 //           _cancelToken.Dispose();
        }

    }
}
