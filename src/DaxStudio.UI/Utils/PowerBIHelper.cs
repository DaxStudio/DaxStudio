using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;
using DaxStudio.UI.Extensions;
using System.Security.Principal;
using DaxStudio.Common;

namespace DaxStudio.UI.Utils
{
    public enum EmbeddedSSASIcon
    {
        PowerBI,
        Devenv,
        PowerBIReportServer,
        Loading,
        None
    }
    public class PowerBIInstance
    {
        public PowerBIInstance(string name, int port, EmbeddedSSASIcon icon)
        {
            Port = port;
            Icon = icon;
            try
            {
                var dashPos = name.LastIndexOf(" - ");
                if (dashPos >= 0)
                { Name = name.Substring(0, dashPos); }  // Strip "Power BI Designer" or "Power BI Desktop" off the end of the string
                else
                {
                    if (port != -1)
                    {
                        Log.Warning("{class} {method} {message} {dashPos}", "PowerBIInstance", "ctor", $"Unable to find ' - ' in Power BI title '{name}'", dashPos);
                    }
                    Name = name; 
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "PowerBIInstance", "ctor", ex.Message, ex.StackTrace);
                Name = name;
            }
        }
        public int Port { get; private set; }
        public string Name { get; private set; }

        public EmbeddedSSASIcon Icon { get; private set; }
    }

    public class PowerBIHelper
    {
    
        public static List<PowerBIInstance> GetLocalInstances(bool includePBIRS)
        {
            List<PowerBIInstance> _instances = new List<PowerBIInstance>();

            _instances.Clear();

            var dict = ManagedIpHelper.GetExtendedTcpDictionary();
            var msmdsrvProcesses = Process.GetProcessesByName("msmdsrv");
            foreach (var proc in msmdsrvProcesses)
            { 
                int _port = 0;
                string parentTitle = $"localhost:{_port}";
                EmbeddedSSASIcon _icon = EmbeddedSSASIcon.PowerBI;
                var parent = proc.GetParent();
                
                if (parent != null)
                {
                    // exit here if the parent == "services" then this is a SSAS instance
                    if (parent.ProcessName.Equals("services", StringComparison.OrdinalIgnoreCase)) continue;

                    // exit here if the parent == "RSHostingService" then this is a SSAS instance
                    if (parent.ProcessName.Equals("RSHostingService", StringComparison.OrdinalIgnoreCase))
                    {
                        // only show PBI Report Server if we are running as admin
                        // otherwise we won't have any access to the models
                        if (IsAdministrator() && includePBIRS)
                            _icon = EmbeddedSSASIcon.PowerBIReportServer;
                        else
                            continue;
                    }

                    // if the process was launched from Visual Studio change the icon
                    if (parent.ProcessName.Equals("devenv", StringComparison.OrdinalIgnoreCase)) _icon = EmbeddedSSASIcon.Devenv;

                    // get the window title so that we can parse out the file name
                    parentTitle = parent.MainWindowTitle;
                    
                    if (parentTitle.Length == 0)
                    {
                        // for minimized windows we need to use some Win32 api calls to get the title
                        //parentTitle = WindowTitle.GetWindowTitleTimeout( parent.Id, 300);
                        parentTitle = WindowTitle.GetWindowTitle(parent.Id);
                    }
                }
                // try and get the tcp port from the Win32 TcpTable API
                try
                {
                    TcpRow tcpRow = null;
                    dict.TryGetValue(proc.Id, out tcpRow);
                    if (tcpRow != null)
                    {
                        _port = tcpRow.LocalEndPoint.Port;
                        _instances.Add(new PowerBIInstance(parentTitle, _port, _icon));
                        Log.Debug("{class} {method} PowerBI found on port: {port}", "PowerBIHelper", "Refresh", _port);
                    }
                    else
                    {
                        Log.Debug("{class} {method} PowerBI port not found for process: {processName} PID: {pid}", "PowerBIHelper", "Refresh", proc.ProcessName, proc.Id);
                    }
                    
                }
                catch (Exception ex)
                {
                    Log.Error("{class} {Method} {Error} {StackTrace}", "PowerBIHelper", "Refresh", ex.Message, ex.StackTrace);
                }

            }
            return _instances;    
        }

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        

    }
}
