using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.Utils
{
    [TestClass]
    public class SqlProfilerHelperTests
    {
        // ---- ExtractExecutablePath ----------------------------------------

        [TestMethod]
        public void ExtractExecutablePath_StripsSurroundingQuotesAndDropsArgs()
        {
            const string registryValue = "\"C:\\Program Files (x86)\\Microsoft SQL Server Management Studio 20\\Common7\\IDE\\profiler.exe\" /F \"%1\"";
            var exe = SqlProfilerHelper.ExtractExecutablePath(registryValue);
            Assert.AreEqual(@"C:\Program Files (x86)\Microsoft SQL Server Management Studio 20\Common7\IDE\profiler.exe", exe);
        }

        [TestMethod]
        public void ExtractExecutablePath_HandlesUnquotedShortPath()
        {
            const string registryValue = @"C:\PROGRA~1\SSMS\profiler.exe /F %1";
            var exe = SqlProfilerHelper.ExtractExecutablePath(registryValue);
            Assert.AreEqual(@"C:\PROGRA~1\SSMS\profiler.exe", exe);
        }

        [TestMethod]
        public void ExtractExecutablePath_DoesNotChopForwardSlashesInPath()
        {
            // Forward-slash separators in a quoted path must survive (the legacy
            // implementation split on '/' and corrupted these).
            const string registryValue = "\"C:/Tools/SSMS/profiler.exe\" /F \"%1\"";
            var exe = SqlProfilerHelper.ExtractExecutablePath(registryValue);
            Assert.AreEqual("C:/Tools/SSMS/profiler.exe", exe);
        }

        [TestMethod]
        public void ExtractExecutablePath_EmptyOrNullReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SqlProfilerHelper.ExtractExecutablePath(null));
            Assert.AreEqual(string.Empty, SqlProfilerHelper.ExtractExecutablePath(string.Empty));
            Assert.AreEqual(string.Empty, SqlProfilerHelper.ExtractExecutablePath("   "));
        }

        // ---- QuoteArgument ------------------------------------------------

        [TestMethod]
        public void QuoteArgument_DoesNotQuoteSimpleArgument()
        {
            Assert.AreEqual("localhost", SqlProfilerHelper.QuoteArgument("localhost"));
            Assert.AreEqual("powerbi://api.powerbi.com/v1.0/myorg/MyWorkspace",
                SqlProfilerHelper.QuoteArgument("powerbi://api.powerbi.com/v1.0/myorg/MyWorkspace"));
        }

        [TestMethod]
        public void QuoteArgument_QuotesArgumentWithSpaces()
        {
            const string uri = "powerbi://api.powerbi.com/v1.0/myorg/My Workspace";
            Assert.AreEqual("\"powerbi://api.powerbi.com/v1.0/myorg/My Workspace\"",
                SqlProfilerHelper.QuoteArgument(uri));
        }

        [TestMethod]
        public void QuoteArgument_EscapesEmbeddedQuotes()
        {
            Assert.AreEqual("\"weird\\\"name\"", SqlProfilerHelper.QuoteArgument("weird\"name"));
        }

        [TestMethod]
        public void QuoteArgument_EmptyOrNullReturnsEmptyQuotedString()
        {
            Assert.AreEqual("\"\"", SqlProfilerHelper.QuoteArgument(null));
            Assert.AreEqual("\"\"", SqlProfilerHelper.QuoteArgument(string.Empty));
        }

        [TestMethod]
        public void QuoteArgument_QuotesArgumentWithTabs()
        {
            Assert.AreEqual("\"a\tb\"", SqlProfilerHelper.QuoteArgument("a\tb"));
        }
    }
}
