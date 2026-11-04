using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using DaxStudio.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class ManagedIpHelperTests
    {
        // The state Windows 11 26200.9818 / 26300.9818 (KB5127936) report for the msmdsrv listeners
        private const TcpState State0 = TcpState.Unknown;

        private const int EnginePid = 30992;
        private const int DesktopPid = 30364;

        // Builds the row from the native struct, so that the port and address decoding of TcpRow is covered too
        private static TcpRow Row(int pid, string local, int localPort, string remote, int remotePort, TcpState state)
        {
            return new TcpRow(new IpHelper.TcpRow
            {
                state = state,
                localAddr = BitConverter.ToUInt32(IPAddress.Parse(local).GetAddressBytes(), 0),
                localPort1 = (byte)(localPort >> 8),
                localPort2 = (byte)localPort,
                remoteAddr = BitConverter.ToUInt32(IPAddress.Parse(remote).GetAddressBytes(), 0),
                remotePort1 = (byte)(remotePort >> 8),
                remotePort2 = (byte)remotePort,
                owningPid = pid
            });
        }

        private static Dictionary<int, int> SelectPorts(IEnumerable<TcpRow> rows)
        {
            return ManagedIpHelper.SelectListenerRows(rows).ToDictionary(e => e.Key, e => e.Value.LocalEndPoint.Port);
        }

        [TestMethod]
        public void SelectListenerRows_ListenerReportedInState0_IsSelected()
        {
            // The IPv4 rows an affected build reported for a Power BI Desktop engine
            var rows = new[]
            {
                Row(EnginePid, "127.0.0.1", 57316, "0.0.0.0", 0, State0),
                Row(DesktopPid, "127.0.0.1", 61001, "127.0.0.1", 57316, TcpState.Established),
            };

            var ports = SelectPorts(rows);

            Assert.AreEqual(1, ports.Count);
            Assert.AreEqual(57316, ports[EnginePid]);
        }

        [TestMethod]
        public void SelectListenerRows_UnconnectedRowOnUnspecifiedAddress_IsSelected()
        {
            var ports = SelectPorts(new[] { Row(EnginePid, "0.0.0.0", 2383, "0.0.0.0", 0, State0) });

            Assert.AreEqual(2383, ports[EnginePid]);
        }

        [TestMethod]
        public void SelectListenerRows_ListenRow_WinsOverUnconnectedRow_InEitherOrder()
        {
            var rows = new[]
            {
                Row(EnginePid, "127.0.0.1", 50000, "0.0.0.0", 0, State0),
                Row(EnginePid, "127.0.0.1", 57316, "0.0.0.0", 0, TcpState.Listen),
            };

            Assert.AreEqual(57316, SelectPorts(rows)[EnginePid]);
            Assert.AreEqual(57316, SelectPorts(rows.Reverse())[EnginePid]);
        }

        [TestMethod]
        public void SelectListenerRows_SeveralListenRows_FirstInTableOrderWins()
        {
            var rows = new[]
            {
                Row(EnginePid, "0.0.0.0", 2383, "0.0.0.0", 0, TcpState.Listen),
                Row(EnginePid, "127.0.0.1", 2390, "0.0.0.0", 0, TcpState.Listen),
            };

            Assert.AreEqual(2383, SelectPorts(rows)[EnginePid]);
        }

        [TestMethod]
        public void SelectListenerRows_ListenRowOnNetworkAddress_IsStillSelected()
        {
            var ports = SelectPorts(new[] { Row(EnginePid, "192.168.1.10", 2383, "0.0.0.0", 0, TcpState.Listen) });

            Assert.AreEqual(2383, ports[EnginePid]);
        }

        [TestMethod]
        public void SelectListenerRows_ConnectedRows_AreNotSelected()
        {
            var rows = new[]
            {
                Row(EnginePid, "127.0.0.1", 57316, "127.0.0.1", 61001, TcpState.Established),
                Row(EnginePid, "127.0.0.1", 57317, "127.0.0.1", 61002, State0),
                Row(EnginePid, "127.0.0.1", 57318, "127.0.0.1", 61003, TcpState.TimeWait),
            };

            Assert.AreEqual(0, SelectPorts(rows).Count);
        }

        [TestMethod]
        public void SelectListenerRows_UnconnectedRowOnNetworkAddress_IsNotSelected()
        {
            var ports = SelectPorts(new[] { Row(EnginePid, "192.168.1.10", 57316, "0.0.0.0", 0, State0) });

            Assert.AreEqual(0, ports.Count);
        }

        [TestMethod]
        public void SelectListenerRows_UnconnectedRowWithRemotePort_IsNotSelected()
        {
            var ports = SelectPorts(new[] { Row(EnginePid, "127.0.0.1", 57316, "0.0.0.0", 443, State0) });

            Assert.AreEqual(0, ports.Count);
        }

        [TestMethod]
        public void SelectListenerRows_EachProcess_GetsItsOwnPort()
        {
            var rows = new[]
            {
                Row(EnginePid, "127.0.0.1", 57316, "0.0.0.0", 0, State0),
                Row(EnginePid + 1, "127.0.0.1", 57400, "0.0.0.0", 0, TcpState.Listen),
            };

            var ports = SelectPorts(rows);

            Assert.AreEqual(57316, ports[EnginePid]);
            Assert.AreEqual(57400, ports[EnginePid + 1]);
        }

        [TestMethod]
        public void GetExtendedTcpDictionary_FindsALoopbackListenerOfThisProcess()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

                var rows = ManagedIpHelper.GetExtendedTcpTable(true).Where(r => r.ProcessId == pid).ToList();

                Assert.IsTrue(rows.Any(r => r.LocalEndPoint.Port == port), "The listener's row was not found in the TCP table.");
                Assert.IsTrue(ManagedIpHelper.GetExtendedTcpDictionary().ContainsKey(pid), "The listener was not selected.");
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
