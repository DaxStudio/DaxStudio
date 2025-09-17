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
            string formattedxmSql = @"|~K~|SET|~E~| |~K~|DC_KIND|~E~|=""|~K~|DENSE|~E~|"";
|~K~|SELECT|~E~|
	'Product'[Color],
	'Product'[Class],
	'Product'[Product Category Name],
	|~K~|COUNT|~E~| () 
|~K~|FROM|~E~| 'Product'
|~K~|WHERE|~E~|
	 ( |~K~|COALESCE|~E~| ( [|~S~|CallbackDataID|~E~||~F~| ( SEARCH ( ""R"", 'Product'[Color], 1, 0 ) )|~E~| ] ( |~K~|PFDATAID|~E~| ( 'Product'[Color] ) ) ) = |~K~|COALESCE|~E~| ( 1 ) ) ;";
            var options = Substitute.For<IGlobalOptions>();
            options.HighlightXmSqlCallbacks.Returns(true);
            var args = new DaxStudioTraceEventArgs();
            args.EventClassName = "VertiPaqSEQueryEnd";
            args.TextData = xmSql;
            var evnt = new TraceStorageEngineEvent( args, 1, options, null, null);
            
            //evnt.QueryRichText = xmSql;
            Assert.AreEqual(formattedxmSql, evnt.QueryRichText);

        }
    }
}
