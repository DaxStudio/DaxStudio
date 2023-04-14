using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class TraceProcessing
    {
        [TestMethod]
        public void TestMashupExtraction()
        {

            var input = @"Finished processing partition '<ccon>RandomNumbers-17762961-46a3-401d-ae1b-000e76b909f9</ccon>' of table '<ccon>RandomNumbers</ccon>'. (TableTMID='12', PartitionTMID='25');

[MashupCPUTime: 23093 ms, MashupPeakMemory: 587728 KB]";

            var te = new Microsoft.AnalysisServices.TraceEventArgs();
            te[Microsoft.AnalysisServices.TraceColumn.EventClass] = ((int)Microsoft.AnalysisServices.TraceEventClass.ProgressReportEnd).ToString();
            te[Microsoft.AnalysisServices.TraceColumn.EventSubclass] = "59";  // TabularRefresh
            te[Microsoft.AnalysisServices.TraceColumn.TextData] = input;
            te[Microsoft.AnalysisServices.TraceColumn.CurrentTime] = "2023-04-01T11:21:22";
            List<int> columns = new List<int>() {
                (int)Microsoft.AnalysisServices.TraceColumn.EventClass,
                (int)Microsoft.AnalysisServices.TraceColumn.EventSubclass,
                (int)Microsoft.AnalysisServices.TraceColumn.TextData
            };

            var dsEv = new DaxStudio.QueryTrace.DaxStudioTraceEventArgs(te, "", columns);

            Assert.AreEqual(12, dsEv.TableID, "Failed to parse TableID");
            Assert.AreEqual(25, dsEv.PartitionID, "Failed to parse PartitionID");
            Assert.AreEqual(23093, dsEv.MashupCPUTimeMs);
            Assert.AreEqual(587728, dsEv.MashupPeakMemoryKb);
        }
    }
}
