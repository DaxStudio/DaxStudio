﻿using System;
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
        public static readonly string[] PBIDesktopMainWindowTitleSuffixes = new string[]
        {
            // Different characters are used as a separator in the PBIDesktop window title depending on the current UI culture/localization
            // See https://github.com/sql-bi/Bravo/issues/476

            " \u002D Power BI Desktop", // Dash Punctuation - minus hyphen
            " \u2212 Power BI Desktop", // Math Symbol - minus sign
            " \u2011 Power BI Desktop", // Dash Punctuation - non-breaking hyphen
            " \u2013 Power BI Desktop", // Dash Punctuation - en dash
            " \u2014 Power BI Desktop", // Dash Punctuation - em dash
            " \u2015 Power BI Desktop", // Dash Punctuation - horizontal bar
        };

        public PowerBIInstance(string windowTitle, int port, EmbeddedSSASIcon icon)
        {
            Port = port;
            Icon = icon;
            try
            {
                // Strip "Power BI Designer" or "Power BI Desktop" off the end of the string
                foreach (var suffix in PBIDesktopMainWindowTitleSuffixes)
                {
                    var index = windowTitle.LastIndexOf(suffix);
                    if (index >= 1)
                    {
                        Name = windowTitle.Substring(0,index).Trim();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(Name))
                {
                    if (port != -1)
                    {
                        Log.Verbose(Constants.LogMessageTemplate, nameof(PowerBIInstance), "ctor", $"Unable to find ' - Power BI Desktop' in Power BI title '{windowTitle}'");
                    }
                    Name = windowTitle; 
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex,Constants.LogMessageTemplate, nameof(PowerBIInstance), "ctor", ex.Message);
                Name = windowTitle;
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
                string parentTitle = string.Empty; // $"localhost:{_port}";
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
                        Log.Debug("{class} {method} PowerBI found on port: {port}", nameof(PowerBIHelper), nameof(GetLocalInstances), _port);
                    }
                    else
                    {
                        Log.Debug("{class} {method} PowerBI port not found for process: {processName} PID: {pid}", nameof(PowerBIHelper), nameof(GetLocalInstances), proc.ProcessName, proc.Id);
                    }
                    
                }
                catch (Exception ex)
                {
                    Log.Error("{class} {Method} {Error} {StackTrace}", nameof(PowerBIHelper), nameof(GetLocalInstances), ex.Message, ex.StackTrace);
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
