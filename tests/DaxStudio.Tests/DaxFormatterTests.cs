using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading.Tasks;
using Caliburn.Micro;
using DaxStudio.Tests.Mocks;
using Moq;
using DaxStudio.Interfaces;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxFormatterTests
    {
        

        [TestMethod,Ignore]
        public void TestValidDax()
        {
            var uri = "http://www.daxformatter.com/api/daxformatter/DaxFormat";
            //var uri2 = "http://daxtest02.azurewebsites.net/api/daxformatter/daxformat";
            var data = "{Dax:'evaluate FILTER(tatatata, blah[x]=1) ', ListSeparator:',', DecimalSeparator:'.'}";
            var enc = System.Text.Encoding.UTF8;
            var data1 = enc.GetBytes(data);
            string redirectUrl = null;

            var proxy = System.Net.WebRequest.GetSystemWebProxy();
            proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;

            if (redirectUrl == null)
            {
                // www.daxformatter.com redirects request to another site.  HttpWebRequest does redirect with GET.  It fails, since the web service works only with POST
                // The following 2 requests are doing manual POST re-direct
                var redirectRequest = System.Net.HttpWebRequest.Create(uri) as HttpWebRequest;
                redirectRequest.AllowAutoRedirect = false;
                redirectRequest.Proxy = proxy;
                var redirectResponse = (HttpWebResponse)redirectRequest.GetResponse();
                redirectUrl = redirectResponse.Headers["Location"];
            }

            var wr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(redirectUrl);

            wr.ContentType = "application/json";
            wr.Method = "POST";
            wr.Accept = "application/json, text/javascript, */*; q=0.01";
            wr.Headers.Add("Accept-Encoding", "gzip,deflate");
            wr.Headers.Add("Accept-Language", "en-US,en;q=0.8");
            wr.ContentType = "application/json; charset=UTF-8";
            wr.AutomaticDecompression = DecompressionMethods.GZip;
            wr.Proxy = proxy;
            var strm = wr.GetRequestStream();
            strm.Write(data1, 0, data1.Length);

            var resp = wr.GetResponse();
            //var outStrm = new System.IO.Compression.GZipStream(resp.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
            var outStrm = resp.GetResponseStream();
            var reader = new System.IO.StreamReader(outStrm);
            var output = reader.ReadToEnd();

            Assert.AreEqual("\"EVALUATE\\r\\nFILTER ( tatatata, blah[x] = 1 )\\r\\n\"", output);
        }

        [TestMethod,Ignore]
        public void TestFormatInvalidDax()
        {
            //var uri = "http://www.daxformatter.com/api/daxformatter/DaxFormat";
            var uri2 = "http://daxtest02.azurewebsites.net/api/daxformatter/daxformat";
            var data = "{Dax:'evaluate values(tatatata', ListSeparator:',', DecimalSeparator:'.'}";
            var enc = System.Text.Encoding.UTF8;
            var data1 = enc.GetBytes(data);

            var wr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri2);
            wr.ContentType = "application/json";
            wr.Method = "POST";
            wr.Accept = "application/json, text/javascript, */*; q=0.01";
            wr.Headers.Add("Accept-Encoding", "gzip,deflate");
            wr.Headers.Add("Accept-Language", "en-US,en;q=0.8");
            wr.ContentType = "application/json; charset=UTF-8";
            wr.AutomaticDecompression = DecompressionMethods.GZip;
            var strm = wr.GetRequestStream();
            strm.Write(data1, 0, data1.Length);

            var resp = wr.GetResponse();
            //var outStrm = new System.IO.Compression.GZipStream(resp.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
            var reader = new System.IO.StreamReader(resp.GetResponseStream());
            var output = reader.ReadToEnd();

            Assert.AreNotEqual("\"\"", output);
        }

        [TestMethod,Ignore]
        public void TestFormatInvalidDaxVerbose()
        {
            var uri = "http://www.daxformatter.com/api/daxformatter/DaxrichFormatverbose";
            
            var data = "{Dax:'evaluate values(tatatata ', ListSeparator:',', DecimalSeparator:'.'}";
            var enc = System.Text.Encoding.UTF8;
            var data1 = enc.GetBytes(data);
            string redirectUrl = null;
            Uri redirectUri;
            redirectUrl = null;// "http://daxtest02.azurewebsites.net/api/daxformatter/daxrichformatverbose";

            var proxy = System.Net.WebRequest.GetSystemWebProxy();
            proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
            
            if (redirectUrl == null)
            {
                // www.daxformatter.com redirects request to another site.  HttpWebRequest does redirect with GET.  It fails, since the web service works only with POST
                // The following 2 requests are doing manual POST re-direct
                var redirectRequest = System.Net.HttpWebRequest.Create(uri) as HttpWebRequest;
                redirectRequest.AllowAutoRedirect = false;
                //redirectRequest.Proxy = proxy;
                using (var redirectResponse = (HttpWebResponse)redirectRequest.GetResponse())
                {
                    redirectUrl = redirectResponse.Headers["Location"];
                    redirectUri = new Uri(redirectUrl);
                    System.Diagnostics.Debug.WriteLine("Host: " + redirectUri.Host);
                }
                //redirectUrl = string.Format("http://{0}/api/daxformatter/daxrichformatverbose", redirectUri.Host);
            }

            var wr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(redirectUrl);
            
            wr.ContentType = "application/json";
            wr.Method = "POST";
            wr.Accept = "application/json, text/javascript, */*; q=0.01";
            wr.Headers.Add("Accept-Encoding", "gzip,deflate");
            wr.Headers.Add("Accept-Language", "en-US,en;q=0.8");
            wr.ContentType = "application/json; charset=UTF-8";
            wr.AutomaticDecompression = DecompressionMethods.GZip;
            
            //wr.Proxy = proxy;
            string output = "";
            using (var strm = wr.GetRequestStream())
            {
                strm.Write(data1, 0, data1.Length);

                using (var resp = wr.GetResponse())
                {
                    //var outStrm = new System.IO.Compression.GZipStream(resp.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                    var outStrm = resp.GetResponseStream();
                    using (var reader = new System.IO.StreamReader(outStrm))
                    {
                        output = reader.ReadToEnd();
                    }
                }
            }
            var expected = "\"{\\r\\n    \\\"formatted\\\":\\r\\n        [\\r\\n        ],\\r\\n    \\\"errors\\\":\\r\\n        [\\r\\n            { \\\"line\\\" :0, \\\"column\\\" :25, \\\"message\\\" :\\\"Syntax error, expected: columnId, )\\\"}\\r\\n        ]\\r\\n}\\r\\n\"";
            Assert.AreEqual(expected, output);
        }

        [TestMethod,Ignore]
        public void TestDaxFormatterProxyWithInvalidQuery()
        {
            var qry = "evaluate values(tatatata ";
            //var req = new DaxStudio.UI.Model.DaxFormatterRequest();
            //req.Dax = qry;
            var opt = new Mock<IGlobalOptions>();
            opt.SetupGet(o => o.DaxFormatterRequestTimeout).Returns(10);
            //var opt = new MockGlobalOptions() { DaxFormatterRequestTimeout = 10 };
            var t = DaxStudio.UI.Model.DaxFormatterProxy.FormatDaxAsync(qry, null, opt.Object, new MockEventAggregator(), false );
            t.Wait();
            DaxStudio.UI.Model.DaxFormatterResult res = t.Result;
            Assert.AreEqual(0, res.FormattedDax.Length);
            Assert.AreEqual(1, res.errors.Count);
        }

        [TestMethod,Ignore]
        public async Task TestDaxFormatterProxyWithLongQuery()
        {
            var qry = @"
EVALUATE
CALCULATETABLE(
ADDCOLUMNS (
    GENERATE (
        GENERATE (
            VALUES ( 'SalesTerritory'[SalesTerritory Country] ),
            VALUES ( 'Product'[Colour] )
        ),
        VALUES ( 'Reseller'[BusinessType] )
    ),
    ""Sales Amt"", [Sale Amt]
), 
'Date'[Calendar Year] = 2006,
FILTER(VALUES('Product'[Colour]), 
PATHCONTAINS(""BLACK|Blue|Multi"", 'Product'[Colour]))
)
ORDER BY 'SalesTerritory'[SalesTerritory Country] desc, 'Product'[Colour]
";

            var formattedQry = @"EVALUATE
CALCULATETABLE (
    ADDCOLUMNS (
        GENERATE (
            GENERATE (
                VALUES ( 'SalesTerritory'[SalesTerritory Country] ),
                VALUES ( 'Product'[Colour] )
            ),
            VALUES ( 'Reseller'[BusinessType] )
        ),
        ""Sales Amt"", [Sale Amt]
    ),
    'Date'[Calendar Year] = 2006,
    FILTER (
        VALUES ( 'Product'[Colour] ),
        PATHCONTAINS ( ""BLACK|Blue|Multi"", 'Product'[Colour] )
    )
)
ORDER BY
    'SalesTerritory'[SalesTerritory Country] DESC,
    'Product'[Colour]";
            var opt = new Mock<IGlobalOptions>();
            opt.SetupGet(o => o.DaxFormatterRequestTimeout).Returns(10);
            DaxStudio.UI.Model.DaxFormatterResult res = await DaxStudio.UI.Model.DaxFormatterProxy.FormatDaxAsync(qry, null, opt.Object, new MockEventAggregator(), false );
            Assert.AreEqual(569, res.FormattedDax.Length, "Query length does not match");
            Assert.AreEqual(formattedQry, res.FormattedDax, "Formatted Query does not match expected format");
            Assert.IsNull(res.errors);
            
        }

        [TestMethod,Ignore]
        public async Task TestBackslashEscaping()
        {
            var mockGlobalOptions = new Mock<IGlobalOptions>();
            mockGlobalOptions.SetupGet(o => o.ProxyUseSystem).Returns(true);
            //var mockGlobalOptions = new MockGlobalOptions() { ProxyUseSystem = true };
            var mockEventAggregator = new MockEventAggregator();
            //var webReqFac = new UI.Utils.WebRequestFactory(mockGlobalOptions, mockEventAggregator);
            var webReqFac = await UI.Utils.WebRequestFactory.CreateAsync(mockGlobalOptions.Object, mockEventAggregator);
            //var daxFmtProxy = IoC.BuildUp(webReqFac);
            var qry = "EVALUATE FILTER(Customer, Customer[Username] = \"Test\\User\")" ;
            var expectedQry = "EVALUATE\r\nFILTER ( Customer, Customer[Username] = \"Test\\User\" )";
            var opt = new Mock<IGlobalOptions>();
            opt.SetupGet(o => o.DaxFormatterRequestTimeout).Returns(10);
            DaxStudio.UI.Model.DaxFormatterResult res = await DaxStudio.UI.Model.DaxFormatterProxy.FormatDaxAsync(qry, null, opt.Object, new MockEventAggregator(), false);
            Assert.AreEqual(expectedQry, res.FormattedDax);
        }
    }
}
