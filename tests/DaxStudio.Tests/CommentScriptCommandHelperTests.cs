using System.Collections.Generic;
using ADOTabular;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.UI.Utils;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class CommentScriptCommandHelperTests
    {
        private static DatabaseDetails Db(string name, string caption = null)
            => new DatabaseDetails(name, name, caption ?? name, string.Empty, string.Empty, string.Empty, string.Empty);

        #region NormalizeDatabaseName

        [TestMethod]
        public void NormalizeDatabaseName_TrimsWhitespace()
        {
            Assert.AreEqual("Adventure Works", CommentScriptCommandHelper.NormalizeDatabaseName("  Adventure Works  "));
        }

        [TestMethod]
        public void NormalizeDatabaseName_StripsWrappingQuotes()
        {
            Assert.AreEqual("Adventure Works", CommentScriptCommandHelper.NormalizeDatabaseName("\"Adventure Works\""));
        }

        [TestMethod]
        public void NormalizeDatabaseName_StripsQuotesAndSurroundingWhitespace()
        {
            Assert.AreEqual("Adventure Works", CommentScriptCommandHelper.NormalizeDatabaseName("  \"Adventure Works\"  "));
        }

        [TestMethod]
        public void NormalizeDatabaseName_NullReturnsNull()
        {
            Assert.IsNull(CommentScriptCommandHelper.NormalizeDatabaseName(null));
        }

        #endregion

        #region ResolveDatabase

        [TestMethod]
        public void ResolveDatabase_MatchesOnName()
        {
            var dbs = new List<DatabaseDetails> { Db("SalesModel"), Db("AdventureWorks") };

            var match = CommentScriptCommandHelper.ResolveDatabase(dbs, "AdventureWorks");

            Assert.IsNotNull(match);
            Assert.AreEqual("AdventureWorks", match.Name);
        }

        [TestMethod]
        public void ResolveDatabase_MatchesOnCaption()
        {
            // the internal name and the friendly caption shown in the dropdown often differ (e.g. PBIX)
            var dbs = new List<DatabaseDetails> { Db("a1b2c3-guid", "Sales Report") };

            var match = CommentScriptCommandHelper.ResolveDatabase(dbs, "Sales Report");

            Assert.IsNotNull(match);
            Assert.AreEqual("a1b2c3-guid", match.Name);
        }

        [TestMethod]
        public void ResolveDatabase_IsCaseInsensitive()
        {
            var dbs = new List<DatabaseDetails> { Db("AdventureWorks") };

            var match = CommentScriptCommandHelper.ResolveDatabase(dbs, "adventureworks");

            Assert.IsNotNull(match);
            Assert.AreEqual("AdventureWorks", match.Name);
        }

        [TestMethod]
        public void ResolveDatabase_StripsQuotesBeforeMatching()
        {
            var dbs = new List<DatabaseDetails> { Db("Adventure Works") };

            var match = CommentScriptCommandHelper.ResolveDatabase(dbs, "\"Adventure Works\"");

            Assert.IsNotNull(match);
            Assert.AreEqual("Adventure Works", match.Name);
        }

        [TestMethod]
        public void ResolveDatabase_NotFoundReturnsNull()
        {
            var dbs = new List<DatabaseDetails> { Db("SalesModel") };

            Assert.IsNull(CommentScriptCommandHelper.ResolveDatabase(dbs, "DoesNotExist"));
        }

        [TestMethod]
        public void ResolveDatabase_BlankNameReturnsNull()
        {
            var dbs = new List<DatabaseDetails> { Db("SalesModel") };

            Assert.IsNull(CommentScriptCommandHelper.ResolveDatabase(dbs, "   "));
        }

        [TestMethod]
        public void ResolveDatabase_NullListReturnsNull()
        {
            Assert.IsNull(CommentScriptCommandHelper.ResolveDatabase(null, "SalesModel"));
        }

        #endregion

        #region GetTraceWatcherType

        [TestMethod]
        public void GetTraceWatcherType_ServerTimings()
        {
            Assert.AreEqual(typeof(ServerTimesViewModel), CommentScriptCommandHelper.GetTraceWatcherType(TraceType.ServerTimings));
        }

        [TestMethod]
        public void GetTraceWatcherType_QueryPlan()
        {
            Assert.AreEqual(typeof(QueryPlanTraceViewModel), CommentScriptCommandHelper.GetTraceWatcherType(TraceType.QueryPlan));
        }

        [TestMethod]
        public void GetTraceWatcherType_AllQueries()
        {
            Assert.AreEqual(typeof(AllServerQueriesViewModel), CommentScriptCommandHelper.GetTraceWatcherType(TraceType.AllQueries));
        }

        #endregion

        #region ShouldClearResultsWhenAlreadyRunning

        [TestMethod]
        public void ShouldClearResultsWhenAlreadyRunning_ServerTimings()
        {
            Assert.IsTrue(CommentScriptCommandHelper.ShouldClearResultsWhenAlreadyRunning(TraceType.ServerTimings));
        }

        [TestMethod]
        public void ShouldClearResultsWhenAlreadyRunning_QueryPlan()
        {
            Assert.IsTrue(CommentScriptCommandHelper.ShouldClearResultsWhenAlreadyRunning(TraceType.QueryPlan));
        }

        [TestMethod]
        public void ShouldClearResultsWhenAlreadyRunning_AllQueries()
        {
            Assert.IsFalse(CommentScriptCommandHelper.ShouldClearResultsWhenAlreadyRunning(TraceType.AllQueries));
        }

        #endregion

        #region TryGetAutoConnectCommand

        private static ScriptBatch BatchWith(params ScriptCommand[] commands)
        {
            var batch = new ScriptBatch();
            batch.Commands.AddRange(commands);
            return batch;
        }

        [TestMethod]
        public void TryGetAutoConnectCommand_FirstBatchHasConnect_ReturnsConnect()
        {
            var batches = new List<ScriptBatch>
            {
                BatchWith(new ConnectCommand("SERVER", "localhost\\tab19"))
            };

            var result = CommentScriptCommandHelper.TryGetAutoConnectCommand(batches, out var connect, out var db);

            Assert.IsTrue(result);
            Assert.IsNotNull(connect);
            Assert.AreEqual(ConnectionType.SERVER, connect.ConnectionType);
            Assert.AreEqual("localhost\\tab19", connect.ConnectionName);
            Assert.IsNull(db);
        }

        [TestMethod]
        public void TryGetAutoConnectCommand_ConnectWithUse_ReturnsTargetDatabase()
        {
            var batches = new List<ScriptBatch>
            {
                BatchWith(
                    new ConnectCommand("SERVER", "localhost\\tab19"),
                    new UseCommand("\"Adventure Works\""))
            };

            var result = CommentScriptCommandHelper.TryGetAutoConnectCommand(batches, out var connect, out var db);

            Assert.IsTrue(result);
            Assert.IsNotNull(connect);
            // the database name from the USE command is normalized (quotes/whitespace stripped)
            Assert.AreEqual("Adventure Works", db);
        }

        [TestMethod]
        public void TryGetAutoConnectCommand_MultipleUse_UsesLastUseInBatch()
        {
            var batches = new List<ScriptBatch>
            {
                BatchWith(
                    new ConnectCommand("SERVER", "localhost\\tab19"),
                    new UseCommand("First"),
                    new UseCommand("Second"))
            };

            var result = CommentScriptCommandHelper.TryGetAutoConnectCommand(batches, out _, out var db);

            Assert.IsTrue(result);
            Assert.AreEqual("Second", db);
        }

        [TestMethod]
        public void TryGetAutoConnectCommand_NoConnectInFirstBatch_ReturnsFalse()
        {
            var batches = new List<ScriptBatch>
            {
                BatchWith(new UseCommand("Adventure Works"))
            };

            var result = CommentScriptCommandHelper.TryGetAutoConnectCommand(batches, out var connect, out var db);

            Assert.IsFalse(result);
            Assert.IsNull(connect);
            Assert.IsNull(db);
        }

        [TestMethod]
        public void TryGetAutoConnectCommand_ConnectOnlyInLaterBatch_ReturnsFalse()
        {
            // Only the first batch is inspected - a CONNECT in a later batch must not auto-connect.
            var batches = new List<ScriptBatch>
            {
                BatchWith(),
                BatchWith(new ConnectCommand("SERVER", "localhost\\tab19"))
            };

            var result = CommentScriptCommandHelper.TryGetAutoConnectCommand(batches, out var connect, out _);

            Assert.IsFalse(result);
            Assert.IsNull(connect);
        }

        [TestMethod]
        public void TryGetAutoConnectCommand_EmptyBatchList_ReturnsFalse()
        {
            var result = CommentScriptCommandHelper.TryGetAutoConnectCommand(new List<ScriptBatch>(), out var connect, out var db);

            Assert.IsFalse(result);
            Assert.IsNull(connect);
            Assert.IsNull(db);
        }

        [TestMethod]
        public void TryGetAutoConnectCommand_NullBatches_ReturnsFalse()
        {
            var result = CommentScriptCommandHelper.TryGetAutoConnectCommand(null, out var connect, out var db);

            Assert.IsFalse(result);
            Assert.IsNull(connect);
            Assert.IsNull(db);
        }

        #endregion
    }
}
