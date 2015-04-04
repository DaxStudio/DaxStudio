using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Serilog;
using System.Diagnostics;

namespace DaxStudio
{
    public class WebHost
    {
        private static IDisposable webApp;
        private static int _port;

        public static int Start()
        {
            return Start(0);
        }
        public static int Start(int port)
        {
            if (webApp != null) return _port;   // exit here if we are already running
            if (IsPortUsed(port) && port != 0) return port; // exit here if the specified port is in use (assume that another instance is running)
            
            if (port == 0) { port = GetOpenPort(9000,9999); } // find a free port if one was not specified
            _port = port;
            string baseAddress = string.Format("http://localhost:{0}/", port);

            Log.Information("DaxStudio Host started on port {port}", port);
            System.Diagnostics.EventLog appLog = new System.Diagnostics.EventLog();
            appLog.Source = "DaxStudio";
            appLog.WriteEntry(string.Format("DaxStudio Excel Add-in Listening on port {0}", port), EventLogEntryType.Information);

            // Start OWIN host 
            webApp = WebApp.Start<DaxStudio.ExcelAddin.Xmla.Startup>(url: baseAddress);
            return port;
        }

        /// <summary>
        /// Finds an available TCP/IP port in the range between the Start and End params
        /// </summary>
        /// <param name="portStartIndex">Port Number to start searching from</param>
        /// <param name="portEndIndex">Port Number to finish searching at</param>
        /// <returns>an available tcp/ip port that does not currently have an active listener</returns>
        private static int GetOpenPort(int portStartIndex, int portEndIndex)
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

        public static bool IsPortUsed(int port)
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpEndPoints = properties.GetActiveTcpListeners();
            List<int> usedPorts = tcpEndPoints.Select(p => p.Port).ToList<int>();
            
            return usedPorts.Contains(port);    
        }

        public static void Stop()
        {
            if (webApp != null)
            {
                Log.Information("DaxStudio Host stopped listening on port {port}", _port);
                webApp.Dispose();
                webApp = null;
            }
        }
    }
}
