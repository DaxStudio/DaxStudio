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
    }
}
