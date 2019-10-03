using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using Serilog;
using System.Diagnostics;

namespace DaxStudio
{
    public class DaxStudioClient
    {
        public DaxStudioClient(Process process, int port)
        {
            Process = process;
            Port = port;
        }
        public Process Process { get; private set; }
        public int Port { get; private set; }

    }

    public class DaxStudioStandalone
    {
        public static DaxStudioClient GetClient()
        {
            var port = 0;

            using (ManagementClass mgmtClass = new ManagementClass("Win32_Process"))
            {
                foreach (ManagementObject process in mgmtClass.GetInstances())
                {

                    string processName = process["Name"].ToString().ToLower();
                    if (processName == "daxstudio.exe")
                    {
                        UInt32 pid32 = (UInt32)process["ProcessId"];
                        int pid = Convert.ToInt32(pid32);
                        // Get the command line - can be null if we don't have permissions
                        // but should have permission for DaxStudio as it should have been
                        // launched by the current user.
                        string cmdLine = null;
                        if (process["CommandLine"] != null)
                        {
                            cmdLine = process["CommandLine"].ToString();
                            try
                            {
                                var rex = new System.Text.RegularExpressions.Regex("((?:\\\".*\\\"\\s)(?<port>\\d*))");
                                var m = rex.Matches(cmdLine);
                                if (m.Count == 0) continue;
                                int.TryParse(m[0].Groups["port"].Captures[0].Value, out port);

                                Log.Debug("{class} {method} DaxStudio standalone found listening on port: {port}", "DaxStudioStandalone", "GetPort", port);
                                return new DaxStudioClient(Process.GetProcessById(pid), port);
                            }
                            catch (Exception ex)
                            {
                                Log.Error("{class} {Method} {Error}", "DaxStudioStandalone", "GetPort", ex.Message);
                            }

                        }

                    }
                }
                return null;
            }
        }

    }
}
