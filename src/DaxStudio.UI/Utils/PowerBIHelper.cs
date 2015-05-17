using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using Serilog;

namespace DaxStudio.UI.Utils
{
    public class PowerBIInstance
    {
        public PowerBIInstance(string name, int port)
        {
            Port = port;
            Name = name.Substring(0, name.IndexOf(" - Power BI Designer"));
        }
        public int Port { get; private set; }
        public string Name { get; private set; }
    }

    public class PowerBIHelper
    {
        private static int _port = 0;
        private static bool _portSet = false;
        private static List<PowerBIInstance> _instances = new List<PowerBIInstance>();
        public static void Refresh()
        {
            _port = 0;
            _portSet = false;
            _instances.Clear();

            ManagementClass mgmtClass = new ManagementClass("Win32_Process");
            foreach (ManagementObject process in mgmtClass.GetInstances())
            {

                string processName = process["Name"].ToString().ToLower();
                if (processName == "msmdsrv.exe")
                {
                    
                    // get the process pid
                    System.UInt32 pid = (System.UInt32)process["ProcessId"];
                    var parentPid = int.Parse(process["ParentProcessId"].ToString());
                    var parentTitle = "";
                    if (parentPid > 0)
                    {
                        parentTitle = Process.GetProcessById(parentPid).MainWindowTitle;
                        if (parentTitle.Length == 0)
                        {
                            // for minimized windows we need to use some Win32 api calls to get the title
                            parentTitle = GetWindowTitle(parentPid);
                        }
                    }
                    // Get the command line - can be null if we don't have permissions
                    // but should have permission for PowerBI msmdsrv as it will have been
                    // launched by the current user.
                    string cmdLine = null;
                    if (process["CommandLine"] != null)
                    {
                        cmdLine = process["CommandLine"].ToString();
                        try
                        {
                            var rex = new System.Text.RegularExpressions.Regex("-s\\s\"(?<path>.*)\"");
                            var m = rex.Matches(cmdLine);
                            if (m.Count == 0) continue;
                            string msmdsrvPath = m[0].Groups["path"].Captures[0].Value;
                            var portFile = string.Format("{0}\\msmdsrv.port.txt", msmdsrvPath);
                            if (System.IO.File.Exists(portFile))
                            {
                                string sPort = System.IO.File.ReadAllText(portFile, Encoding.Unicode);
                                var port = int.Parse(sPort);
                                _port = port;
                                _portSet = true;
                                _instances.Add(new PowerBIInstance(parentTitle, port));
                                Log.Debug("{class} {method} PowerBI found on port: {port}", "PowerBIHelper", "Refresh", _port);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("{class} {Method} {Error}", "PowerBIHelper", "Refresh", ex.Message);
                        }
                    }
                }
            }
        }

        public static List<PowerBIInstance> Instances
        {
            get
            {
                if (!_portSet) { Refresh(); }
                return _instances;
            }
        }

        //public static int Port { 
        //    get {
        //        if (!_portSet) { Refresh();}
        //        return _port;
        //    }
        //}

        #region PInvoke calls to get the window title of a minimize window

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);


        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

        private static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

        private const uint WM_GETTEXT = 0x000D;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam,
            StringBuilder lParam);

        private static string GetWindowTitle(int procId)
        {
            foreach (var handle in EnumerateProcessWindowHandles(procId))
            {
                StringBuilder message = new StringBuilder(1000);
                if (IsWindowVisible(handle))
                {
                    SendMessage(handle, WM_GETTEXT, message.Capacity, message);
                    if (message.Length > 0) return message.ToString();
                }

            }
            return "";
        }



        #endregion


    }
}
