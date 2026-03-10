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
    }
}
