using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace DaxStudio.Tests
{
    [TestClass]
    public  class xmSqlProcessing
    {

        [TestMethod]
        public void TestCallbackDataIdHighlight()
        {
            string xmSql = @"SET DC_KIND=""DENSE"";
SELECT
    'Product'[Color],
    'Product'[Class],
    'Product'[Product Category Name],
    COUNT () 
FROM 'Product'
WHERE
     ( COALESCE ( [CallbackDataID ( SEARCH ( ""R"", 'Product'[Color], 1, 0 ) ) ] ( PFDATAID ( 'Product'[Color] ) ) ) = COALESCE ( 1 ) ) ;";
            // With AvalonEdit syntax highlighting, QueryRichText stores plain text (no marker codes)
            var options = Substitute.For<IGlobalOptions>();
            options.HighlightXmSqlCallbacks.Returns(true);
            var args = new DaxStudioTraceEventArgs();
            args.EventClassName = "VertiPaqSEQueryEnd";
            args.TextData = xmSql;
            var evnt = new TraceStorageEngineEvent( args, 1, options, null, null);
            
            // QueryRichText should now contain the query without marker codes
            Assert.AreEqual(xmSql, evnt.QueryRichText);
            // HighlightQuery should be true since the query contains CallbackDataID
            Assert.IsTrue(evnt.HighlightQuery);

        }

        [TestMethod]
        public void TestFormatEventWithCallback()
        {
            string xmSql = @"SET DC_KIND=""AUTO"";
SELECT
[Period Definition  SISO  (139)].[Period (1438)] AS [Period Definition  SISO  (139)$Period (1438)],
MAX([MinMaxColumnPositionCallback](PFDATAID( [Period Definition  SISO  (139)].[Previous Period (1439)] ))) AS [$Measure0]
FROM [Period Definition  SISO  (139)]
WHERE
	[Period Definition  SISO  (139)].[Period (1438)] IN ('YTD', 'L13W', 'LW', 'L4W');


[Estimated size (volume, marshalling bytes): 37, 592]";

            // ANTLR formatter expected output
            string expectedAntlr = @"SET DC_KIND=""AUTO"";
SELECT
    'Period Definition  SISO'[Period],
    MAX ( MinMaxColumnPositionCallback ( PFDATAID ( 'Period Definition  SISO'[Previous Period] ) ) )
FROM 'Period Definition  SISO'
WHERE
    'Period Definition  SISO'[Period] IN ( 'YTD', 'L13W', 'LW', 'L4W' ) ;


Estimated size: rows = 37  bytes = 592
";

            // With AvalonEdit syntax highlighting, QueryRichText stores plain text (no marker codes)
            var options = Substitute.For<IGlobalOptions>();
            options.HighlightXmSqlCallbacks.Returns(true);
            options.SimplifyXmSqlSyntax.Returns(true);
            options.FormatXmSql.Returns(true);
            options.UseAntlrParser.Returns(true);

            var args = new DaxStudioTraceEventArgs();
            args.EventClassName = "VertiPaqSEQueryEnd";
            args.TextData = xmSql;
            var evnt = new TraceStorageEngineEvent(args, 1, options, null, null);

            // QueryRichText should now contain the query without marker codes
            Assert.AreEqual(expectedAntlr, evnt.QueryRichText);
            // HighlightQuery should be true since the query contains MinMaxColumnPositionCallback
            Assert.IsTrue(evnt.HighlightQuery);

            // Regex formatter: verify it also uses spaces (not tabs) and doesn't throw
            options.UseAntlrParser.Returns(false);
            var args2 = new DaxStudioTraceEventArgs();
            args2.EventClassName = "VertiPaqSEQueryEnd";
            args2.TextData = xmSql;
            var evnt2 = new TraceStorageEngineEvent(args2, 1, options, null, null);

            Assert.IsFalse(evnt2.QueryRichText.Contains("\t"), "Regex formatter should not use tab characters");
            Assert.IsTrue(evnt2.QueryRichText.Contains("    "), "Regex formatter should use 4-space indentation");
            Assert.IsTrue(evnt2.HighlightQuery, "HighlightQuery should be true for callback queries");


        }
    }
}
