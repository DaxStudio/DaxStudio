using Antlr4.Runtime;
using DaxStudio.Parsers.StorageEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class XmSqlErrorListenerTests
    {
        [DataTestMethod]
        [DataRow("formatter", true)]
        [DataRow("analysis", true)]
        [DataRow("fingerprint", true)]
        [DataRow("formatter", false)]
        [DataRow("analysis", false)]
        [DataRow("fingerprint", false)]
        public void EntryPoints_LogParserErrorsAndKeepRecovery(string entryPoint, bool malformed)
        {
            var sink = new RecordingSink();
            var original = Log.Logger;
            using (var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger())
            {
                Log.Logger = logger;
                try
                {
                    var query = "SELECT\r\n" + (malformed ? "? " : "") + "'Sales'[Amount]\r\nFROM 'Sales';";
                    switch (entryPoint)
                    {
                        case "formatter":
                            Assert.IsNotNull(AntlrXmSqlFormatter.Format(query, true, true, out _, out _, out _));
                            break;
                        case "analysis":
                            Assert.IsTrue(new AntlrXmSqlParser().ParseQuery(query, new XmSqlAnalysis()));
                            break;
                        case "fingerprint":
                            Assert.IsNotNull(new XmSqlQueryGrouper().ComputeFingerprint(query));
                            break;
                    }

                    var diagnostics = sink.Events.Where(e => e.Properties.ContainsKey("Diagnostic")).ToList();
                    if (!malformed)
                    {
                        Assert.AreEqual(0, diagnostics.Count);
                        return;
                    }

                    Assert.IsTrue(diagnostics.Count > 0, "Recoverable syntax errors must be logged.");
                    var diagnostic = diagnostics[0];
                    Assert.AreEqual(LogEventLevel.Debug, diagnostic.Level);
                    Assert.AreEqual("parser", ((ScalarValue)diagnostic.Properties["Stage"]).Value);
                    Assert.AreEqual(2, ((ScalarValue)diagnostic.Properties["Line"]).Value);
                    Assert.AreEqual(1, ((ScalarValue)diagnostic.Properties["Column"]).Value);
                    StringAssert.Contains((string)((ScalarValue)diagnostic.Properties["Diagnostic"]).Value, "?");
                }
                finally
                {
                    Log.Logger = original;
                }
            }
        }

        [DataTestMethod]
        [DataRow(LogEventLevel.Debug, 1)]
        [DataRow(LogEventLevel.Information, 0)]
        public void LexerDiagnostics_RespectDebugLevel(LogEventLevel minimumLevel, int expectedEvents)
        {
            var sink = new RecordingSink();
            var original = Log.Logger;
            using (var logger = new LoggerConfiguration().MinimumLevel.Is(minimumLevel).WriteTo.Sink(sink).CreateLogger())
            {
                Log.Logger = logger;
                try
                {
                    // ANY_CHAR normally routes unknown xmSQL characters to parser recovery.
                    // Exercise the lexer callback explicitly to verify its diagnostics too.
                    IAntlrErrorListener<int> listener = XmSqlErrorListener.Instance;
                    listener.SyntaxError(TextWriter.Null, null, 0, 3, 4, "test lexer diagnostic", null);
                    Assert.AreEqual(expectedEvents, sink.Events.Count);
                    if (expectedEvents > 0)
                    {
                        var diagnostic = sink.Events[0];
                        Assert.AreEqual(LogEventLevel.Debug, diagnostic.Level);
                        Assert.AreEqual("lexer", ((ScalarValue)diagnostic.Properties["Stage"]).Value);
                        Assert.AreEqual(3, ((ScalarValue)diagnostic.Properties["Line"]).Value);
                        Assert.AreEqual(5, ((ScalarValue)diagnostic.Properties["Column"]).Value);
                        Assert.AreEqual("test lexer diagnostic", ((ScalarValue)diagnostic.Properties["Diagnostic"]).Value);
                    }
                }
                finally
                {
                    Log.Logger = original;
                }
            }
        }

        private sealed class RecordingSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();

            public void Emit(LogEvent logEvent)
            {
                Events.Add(logEvent);
            }
        }
    }
}
