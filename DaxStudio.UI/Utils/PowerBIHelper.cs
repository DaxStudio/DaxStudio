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
    public class PowerBIHelper
    {
        private static int _port = 0;
        private static bool _portSet = false;

        public static void Refresh()
        {
            _port = 0;
            _portSet = false;

            ManagementClass mgmtClass = new ManagementClass("Win32_Process");
            foreach (ManagementObject process in mgmtClass.GetInstances())
            {

                string processName = process["Name"].ToString().ToLower();
                if (processName == "msmdsrv.exe")
                {
                    
                    // get the process pid
                    System.UInt32 pid = (System.UInt32)process["ProcessId"];

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
                                _port = int.Parse(sPort);
                                _portSet = true;
                                Log.Debug("{class} {method} PowerBI found on port: {port}", "PowerBIHelper", "Refresh", _port);
                                break;
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

        public static int Port { 
            get {
                if (!_portSet) { Refresh();}
                return _port;
            }
        }
            
        
    }
}
